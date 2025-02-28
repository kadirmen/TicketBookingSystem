using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json;

namespace ReservationServiceAPI.Middleware
{
    public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JwtMiddleware> _logger;

    public JwtMiddleware(RequestDelegate next, IConnectionMultiplexer redis, IDatabase cache, HttpClient httpClient, ILogger<JwtMiddleware> logger)
    {
        _next = next;
        _cache = redis.GetDatabase();
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
       var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        Console.WriteLine("1 çözümledik");

        if (!string.IsNullOrEmpty(token))
        {
            var userId = GetUserIdFromToken(token);
            Console.WriteLine("2 çözümledik");

            if (userId != null)
            {
                // Redis’te access_token:{userId} anahtarını oku
                var storedToken = await _cache.StringGetAsync($"access_token:{userId}");
                Console.WriteLine($"Redis'teki token: {storedToken}");

                // Eğer Redis’te kayıtlı token yoksa veya eşleşmiyorsa, erişimi engelle
                if (storedToken.IsNullOrEmpty || storedToken != token)
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync("Token has been revoked or is invalid.");
                    Console.WriteLine("Token geçerli değil veya Redis'te yok.");

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
        Console.WriteLine("Token geçerliyse işlem devam ediyor");

        try
        {
            // 2️⃣ Redis'te token varsa, doğrulama için AuthServiceAPI'ye istek gönder
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5192/api/auth/validate", new { Token = token });
                    Console.WriteLine(" Rediste token vardı ve Bu tokeni authservice a gömnderdik. yanıt: ");
                    Console.WriteLine(response);


            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token.");
                return;
            }

            var validationResponse = await response.Content.ReadFromJsonAsync<TokenValidationResponse>();

            if (validationResponse == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token.");
                return;
            }

            // 4️⃣ Redis Cache'e doğrulama sonucunu kaydediyoruz (örn. 10 dakika boyunca geçerli)
           

            // 5️⃣ Kullanıcı bilgilerini HttpContext'e ekliyoruz
            context.Items["UserId"] = validationResponse.UserId;
            context.Items["UserRole"] = validationResponse.Role;

            Console.WriteLine($"UserId: {context.Items["UserId"]}");
            Console.WriteLine($"UserRole: {context.Items["UserRole"]}");

            // 6️⃣ İşleme devam ediyoruz
            //authantication zaten yapılı sadece autharization yapıyoruz program.cs
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError($"AuthService connection error: {ex.Message}");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Authentication service unavailable.");
        }
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
