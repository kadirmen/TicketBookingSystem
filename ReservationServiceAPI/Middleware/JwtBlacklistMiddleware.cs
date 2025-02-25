using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ReservationServiceAPI.Middleware
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public JwtBlacklistMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrEmpty(token))
            {
                var client = _httpClientFactory.CreateClient("AuthService");

                // AuthService’e token geçersiz mi diye sor
                var response = await client.GetAsync($"/api/auth/is-blacklisted?token={token}");

                if (response.IsSuccessStatusCode)
                {
                    var isBlacklisted = bool.Parse(await response.Content.ReadAsStringAsync());
                    if (isBlacklisted)
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Token has been revoked.");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
