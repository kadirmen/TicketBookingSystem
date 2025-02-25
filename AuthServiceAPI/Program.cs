using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using AuthServiceAPI.Data;
using AuthServiceAPI.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using AuthServiceAPI.Dtos;
using AuthServiceAPI.Validators;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// 📌 **JWT Ayarlarını Okuma**
var jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing from configuration");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "DefaultIssuer";
var jwtAudience = configuration["Jwt:Audience"] ?? "DefaultAudience";

// 📌 **PostgreSQL Bağlantısı**
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection") ??
    throw new ArgumentNullException("Connection string is missing")));

// 📌 **Redis Bağlantısını Yapılandırma**
var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton(redis.GetDatabase());

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "AuthService:";
});

// 📌 **Servis Bağımlılıklarını Kaydet**
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenValidator, JwtTokenValidator>();

// 📌 **FluentValidation Entegrasyonu**
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<UserRegisterDtoValidator>();

// 📌 **Swagger için JWT Kimlik Doğrulama Desteği**
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AuthServiceAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token'ınızı girin. Örnek: Bearer {your_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddEndpointsApiExplorer();

// 📌 **JWT Authentication & Redis Blacklist Kontrolü**
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = async context =>
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(token))
                {
                    var cache = redis.GetDatabase();
                    
                    // 📌 **Blacklist Kontrolü**
                    if (await cache.KeyExistsAsync($"blacklist:{token}"))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Token has been revoked.");
                        return;
                    }

                    // 📌 **Token'ı Redis'te Bul ve Kullanıcı Bilgilerini Yükle**
                    var cachedUser = await cache.StringGetAsync($"auth:{token}");
                    if (!cachedUser.IsNullOrEmpty)
                    {
                        var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(cachedUser);
                        var identity = new System.Security.Claims.ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);
                        foreach (var claim in claims)
                        {
                            identity.AddClaim(new System.Security.Claims.Claim(claim.Key, claim.Value));
                        }
                        context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                        context.Success();
                    }
                }
            },
            OnTokenValidated = async context =>
            {
                var cache = redis.GetDatabase();
                var token = context.SecurityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                if (token != null)
                {
                    var claims = token.Claims.ToDictionary(c => c.Type, c => c.Value);
                    await cache.StringSetAsync($"auth:{token.RawData}", System.Text.Json.JsonSerializer.Serialize(claims), TimeSpan.FromMinutes(10));
                }
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// 📌 **Uygulama Başlatma**
var app = builder.Build();

// 📌 **Middleware Sıralaması**
app.UseSwagger();
app.UseSwaggerUI();

// 📌 **Blacklist Middleware**
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
    if (!string.IsNullOrEmpty(token))
    {
        var cache = redis.GetDatabase();
        
        // 📌 **Blacklist Kontrolü**
        if (await cache.KeyExistsAsync($"blacklist:{token}"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Token has been revoked.");
            return;
        }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
