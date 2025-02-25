using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Newtonsoft.Json;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HttpClient _httpClient;
    private readonly IDatabase _cache;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, HttpClient httpClient, IConnectionMultiplexer redis, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _httpClient = httpClient;
        _cache = redis.GetDatabase();
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token is required.");
            return;
        }

        // 1️⃣ Önce Redis Cache'de token var mı kontrol et
        var cachedUser = await _cache.StringGetAsync($"auth:{token}");
        if (!string.IsNullOrEmpty(cachedUser))
        {
            var userInfo = JsonConvert.DeserializeObject<TokenValidationResponse>(cachedUser);
            context.Items["UserId"] = userInfo.UserId;
            context.Items["UserRole"] = userInfo.Role;
            await _next(context);
            return;
        }

        try
        {
            // 2️⃣ Redis’te yoksa AuthServiceAPI’ye istekte bulun
            var response = await _httpClient.PostAsJsonAsync("http://authservice/api/auth/validate", new { Token = token });

            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token.");
                return;
            }

            var validationResponse = await response.Content.ReadFromJsonAsync<TokenValidationResponse>();

            // 3️⃣ Redis Cache’e doğrulama sonucunu kaydet (örn. 10 dk)
            await _cache.StringSetAsync($"auth:{token}", JsonConvert.SerializeObject(validationResponse), TimeSpan.FromMinutes(10));

            context.Items["UserId"] = validationResponse.UserId;
            context.Items["UserRole"] = validationResponse.Role;

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError($"AuthService connection error: {ex.Message}");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Authentication service unavailable.");
        }
    }
}

