using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using AuthServiceAPI.Data;
using AuthServiceAPI.Data.Entities;
using AuthServiceAPI.Dtos;

namespace AuthServiceAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IJwtTokenValidator _jwtTokenValidator;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IDatabase _cache;

        public AuthService(AppDbContext context, IConfiguration config, IConnectionMultiplexer redis, IJwtTokenValidator jwtTokenValidator)
        {
            _context = context;
            _config = config;
            _cache = redis.GetDatabase();
            _jwtTokenValidator = jwtTokenValidator;
        }

        public async Task<UserDto?> GetUserById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null ? null : new UserDto { Username = user.Username };
        }

        public async Task<int?> Register(UserRegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return null;

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user.Id;
        }

        public async Task<AuthResponseDto?> Login(UserLoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpirationDays = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");

            // Eski token'ı Redis'ten silme
            var oldTokenKey = $"access_token:{user.Id}";
            await _cache.KeyDeleteAsync(oldTokenKey);

            var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
            if (existingToken != null)
            {
                existingToken.Token = refreshToken;
                existingToken.Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            }
            else
            {
                _context.RefreshTokens.Add(new RefreshToken
                {
                    Token = refreshToken,
                    Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
                    User = user
                });
            }

            await _context.SaveChangesAsync();
            await StoreAccessTokenInCache(accessToken, user.Id.ToString());

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id
            };
        }


       public async Task<AuthResponseDto?> RefreshToken(string refreshToken)
        {
            // 1️⃣ Gelen refresh token'i veritabanında ara
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User) // User bilgilerini de al
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            // 2️⃣ Token geçersiz mi kontrol et
            if (storedToken == null || storedToken.IsExpired || storedToken.User == null)
            {
                return null; // Refresh token bulunamazsa veya süresi dolmuşsa hata döndür
            }

            var userId = storedToken.User.Id; // 🔥 Burada UserId'yi aldık

            // 3️⃣ Redis’ten eski access_token ve refresh_token'ı temizle
            await _cache.KeyDeleteAsync($"access_token:{userId}");
           

            // 4️⃣ Yeni access token ve refresh token oluştur
            var newAccessToken = GenerateJwtToken(storedToken.User);
            var newRefreshToken = GenerateRefreshToken();

            // 5️⃣ Refresh token süresini belirle ve veritabanında güncelle
            var refreshTokenExpirationDays = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");
            storedToken.Token = newRefreshToken;
            storedToken.Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            
            await _context.SaveChangesAsync();

            // 6️⃣ Yeni tokenları Redis’e kaydet
            await StoreAccessTokenInCache(newAccessToken, userId.ToString());

            // 7️⃣ Kullanıcıya yeni tokenları döndür
            return new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                UserId = userId // 🔥 Burada doğru UserId'yi gönderiyoruz
            };
        }

        private async Task DeleteOldAccessTokenFromCache(string userId)
        {
            var redisKey = $"access_token:{userId}";
            await _cache.KeyDeleteAsync(redisKey);
        }


        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                // Burada user.Id'yi, JWT içerisinde 'sub' (subject) olarak ekliyoruz.
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

                // Kullanıcı adını ve rolünü de ekliyoruz (varsa)
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var jwtKey = _config["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing from configuration");
            var tokenExpirationMinutes = int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(tokenExpirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private async Task StoreAccessTokenInCache(string accessToken, string userId)
        {
            var tokenExpiry = TimeSpan.FromMinutes(int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15"));
            await _cache.StringSetAsync($"access_token:{userId}", accessToken, new TimeSpan(tokenExpiry.Ticks));
        }

    
       public async Task<bool> Logout(string userId, string token)
        {
            // Token'ın süresi dolmuşsa işlem yapmaya gerek yok
            var tokenExpiry = GetTokenExpiry(token);
            if (tokenExpiry == null) return false;

            // Redis’ten access_token:{userId} anahtarını oku
            var storedToken = await _cache.StringGetAsync($"access_token:{userId}");
            Console.WriteLine($"Redis'teki token: {storedToken}");

            // Eğer Redis’te kayıtlı token yoksa veya eşleşmiyorsa çıkışı reddet
            if (storedToken.IsNullOrEmpty || storedToken != token)
            {
                Console.WriteLine("Token geçerli değil veya Redis'te yok.");
                return false;
            }

            // Token eşleşiyorsa, Redis’ten access_token ve auth key’lerini sil
            await _cache.KeyDeleteAsync($"access_token:{userId}");
            await _cache.KeyDeleteAsync($"auth:{token}");

            // Refresh token veritabanında var mı kontrol et ve varsa sil
            var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId.ToString() == userId);
            if (refreshToken != null)
            {
                _context.RefreshTokens.Remove(refreshToken);
                await _context.SaveChangesAsync();
            }

            Console.WriteLine("Çıkış başarılı, token silindi.");
            return true;
        }




        private DateTime? GetTokenExpiry(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            try
            {
                var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
                // JWT token'ı düzgün şekilde çözümlediyse geçerlilik tarihi alınır
                return jwtToken?.ValidTo;
            }
            catch (Exception)
            {
                // Token geçerli değilse veya başka bir hata varsa null döner
                return null;
            }
        }

         public async Task<TokenValidationResponse?> ValidateTokenAsync(string token)
        {
            // Token'ı validate ediyoruz
            var principal = _jwtTokenValidator.ValidateToken(token);

            if (principal == null)
            {
                return null; // Eğer token geçersizse, null döndür
            }

            // Token geçerli ise, 'sub' (userId) ve 'role' gibi bilgileri alıyoruz
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
            {
                return null; // Eğer userId veya role eksikse, geçersiz olarak kabul edebiliriz
            }
            Console.WriteLine( userId, role);
            return new TokenValidationResponse
            {
                UserId = userId,
                Role = role
            };
        }

        
    }
}
