using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nest;
using RabbitMQ.Client;
using System;

// 🔹 Uygulama oluştur
var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// 📌 JWT Ayarlarını Okuma (Eksik olursa hata verir)
var jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing from configuration");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "DefaultIssuer";
var jwtAudience = configuration["Jwt:Audience"] ?? "DefaultAudience";

// 📌 Authentication & Authorization (JWT Kullanımı)
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserPolicy", policy => policy.RequireRole("User"));
});

// 📌 PostgreSQL Bağlantısı (EF Core Kullanımı)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection") 
    ?? throw new ArgumentNullException("Connection string is missing")));

// 📌 ElasticSearch Client Ayarları
var elasticSearchUrl = configuration["ElasticSearch:Url"] ?? "http://localhost:9200";  // Varsayılan URL
var elasticSettings = new ConnectionSettings(new Uri(elasticSearchUrl))
    .DefaultIndex(configuration["ElasticSearch:Index"] ?? "hotels"); // Varsayılan Index

var elasticClient = new ElasticClient(elasticSettings);
builder.Services.AddSingleton<IElasticClient>(elasticClient);

// 📌 RabbitMQ Bağlantısı
var rabbitMQFactory = new ConnectionFactory() { HostName = "localhost" };
var rabbitMQConnection = rabbitMQFactory.CreateConnection();
builder.Services.AddSingleton(rabbitMQConnection);

// 📌 Servis Bağımlılıklarını (DI) Kaydetme
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<RabbitMQPublisher>();

// 📌 Swagger'a JWT Authorization Desteği Ekleme
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ReservationServiceAPI", Version = "v1" });

    // 📌 Bearer Token Kullanımı İçin Yetkilendirme Seçenekleri
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token kullanarak yetkilendirme yapın. 'Bearer {token}' formatında girin."
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

// 📌 Controller Desteği ve API Endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// 📌 Swagger ve UI Ayarları (Sadece Development Ortamında Açık)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 📌 Kimlik Doğrulama ve Yetkilendirme Middleware
app.UseAuthentication();
app.UseAuthorization();

// 📌 Controller'ları Haritalandır
app.MapControllers();

// 📌 RabbitMQ Consumer'ı Başlat
var rabbitMQConsumer = new RabbitMQConsumer(elasticClient);
Task.Run(() => rabbitMQConsumer.StartListening());

// 📌 Uygulamayı Çalıştır
app.Run();
