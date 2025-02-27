using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;

namespace AuthServiceAPI.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDatabase _cache;

        public JwtMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _cache = redis.GetDatabase();
        }

        public async Task Invoke(HttpContext context)
        {
            // Authorization başlığından token'ı alıyoruz
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token))
            {
                // JWT token'ı çözümleyip userId'yi çıkarıyoruz
                var userId = GetUserIdFromToken(token);

                if (userId != null)
                {
                    // Redis'teki token'ı kontrol et
                    var tokenExists = await _cache.KeyExistsAsync($"access_token:{userId}");

                    // Eğer token Redis'te yoksa, yani geçersizse, 401 Unauthorized döner
                    if (!tokenExists)
                    {
                        context.Response.StatusCode = 401; // Unauthorized
                        await context.Response.WriteAsync("Token has been revoked or is invalid.");
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync("Invalid token.");
                    return;
                }
            }

            // Token geçerliyse, işlemi devam ettiriyoruz
            await _next(context);
        }

        // Token'dan userId'yi çıkaran yardımcı metod
        private string GetUserIdFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
                var userId = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value; // 'sub' genellikle userId'yi taşır
                return userId;
            }
            catch
            {
                return null; // Eğer token geçersizse null döndür
            }
        }
    }
}
