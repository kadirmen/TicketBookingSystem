using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace ReservationServiceAPI.Middleware
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDatabase _cache;

        public JwtBlacklistMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _cache = redis.GetDatabase();
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token) && await _cache.KeyExistsAsync($"blacklist:{token}"))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("Token has been revoked.");
                return;
            }

            await _next(context);
        }
    }
}
