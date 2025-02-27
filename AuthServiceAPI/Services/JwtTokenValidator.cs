using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

public class JwtTokenValidator : IJwtTokenValidator
{
    private readonly IConfiguration _configuration;

    public JwtTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var jwtIssuer = _configuration["Jwt:Issuer"];
        var jwtAudience = _configuration["Jwt:Audience"];

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey);

        try
        {
            // Token doğrulama parametreleri
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            // Token'ı doğruluyoruz
            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            // Kullanıcı ID'sini (sub) ve role bilgisini çıkarıyoruz
            var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = principal?.FindFirst(ClaimTypes.Role)?.Value;

            // Eğer kullanıcı ID'si veya role bilgisi yoksa, geçersiz token
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                return null; // Token geçersiz
            }

            // Geçerli token'ı ve kullanıcı bilgilerini döner
            return principal; // Geçerli token'ın claims'lerini döner
        }
        catch
        {
            return null; // Token geçersizse null döner
        }
    }
}
