using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AuthServiceAPI.Middleware
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDatabase _cache;
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public TokenValidationMiddleware(RequestDelegate next, IConnectionMultiplexer redis, IConfiguration configuration)
        {
            _next = next;
            _cache = redis.GetDatabase();
            _jwtKey = configuration["Jwt:Key"];
            _jwtIssuer = configuration["Jwt:Issuer"];
            _jwtAudience = configuration["Jwt:Audience"];
        }

        public async Task Invoke(HttpContext context)
        {
            // Authorization başlığından token'ı alıyoruz
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token))
            {
                // Token'ı doğrulama
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

                    // Token geçerliyse, doğrulama işlemini yapıyoruz
                    var tokenHandler = new JwtSecurityTokenHandler();
                    try
                    {
                        var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

                        var claimsIdentity = new System.Security.Claims.ClaimsIdentity(jwtToken?.Claims);
                        context.User = new System.Security.Claims.ClaimsPrincipal(claimsIdentity);
                    }
                    catch (Exception)
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Invalid token.");
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid token.");
                    return;
                }
            }

            // Token geçerli ise, işlemi devam ettiriyoruz
            await _next(context);
        }

        // Token'dan userId'yi çıkaran yardımcı metod
        private string GetUserIdFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
                var userId = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
                return userId;
            }
            catch
            {
                return null; // Eğer token geçersizse null döndür
            }
        }
    }
}
