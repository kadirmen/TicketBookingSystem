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

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing from configuration");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "DefaultIssuer";
var jwtAudience = configuration["Jwt:Audience"] ?? "DefaultAudience";

// **Veritabanı bağlantısı**
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException("Connection string is missing")));

// **Servisleri ekleme**
builder.Services.AddScoped<IAuthService, AuthService>();

// **FluentValidation entegrasyonu (GÜNCELLENDİ)**
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<UserRegisterDtoValidator>();

// **Swagger için JWT Kimlik Doğrulama Desteği Ekleyelim**
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

// **JWT Authentication ayarları**
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

// **Uygulamayı oluşturma**
var app = builder.Build();

// **Middleware sıralaması**
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
