using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed; // Redis için gerekli
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer; // JWT doğrulama için gerekli
using Microsoft.Extensions.Primitives; // Header'ları almak için gerekli


public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            await _next(context);
            return;
        }

        // Redis'ten geçersiz token kontrolü
        var invalidToken = await cache.GetStringAsync($"invalid_token:{token}");
        if (invalidToken != null)
        {
            // Geçersiz token ise 401 Unauthorized döndürüyoruz
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token is invalid or expired.");
            return;
        }

        // Geçerli token ise devam et
        await _next(context);
    }
}


