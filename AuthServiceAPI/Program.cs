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
using AuthServiceAPI.Middleware;

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

// 📌 **JWT Authentication**
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

var app = builder.Build();

// 📌 **Middleware Sıralaması**
// Swagger arayüzünü kullanabilmek için önce Swagger'ı açıyoruz
app.UseSwagger();
app.UseSwaggerUI();

// 📌 **Token Validation Middleware** - Token doğrulaması yapılacak
app.UseMiddleware<TokenValidationMiddleware>();

// 📌 **Blacklist Middleware** - Redis'teki geçerliliği kontrol edilecek
app.UseMiddleware<JwtMiddleware>();

// Kimlik doğrulaması ve yetkilendirme işlemleri
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
