using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AuthServiceAPI.Data;
using AuthServiceAPI.Data.Entities;
using AuthServiceAPI.Dtos;
using Microsoft.Extensions.Caching.Distributed; // Redis için gerekli using

namespace AuthServiceAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache; // Redis Cache bağımlılığı

        public AuthService(AppDbContext context, IConfiguration config, IDistributedCache cache)
        {
            _context = context;
            _config = config;
            _cache = cache;
        }

        // Kullanıcı bilgilerini ID ile almak
        public async Task<UserDto?> GetUserById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return null;
            }

            return new UserDto { Username = user.Username };
        }

        // Kullanıcı kaydı işlemi
        public async Task<int?> Register(UserRegisterDto dto)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Username == dto.Username);
            if (userExists) return null;

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user.Id;
        }

        // Kullanıcı girişi (JWT ve Refresh Token oluşturma)
        public async Task<AuthResponseDto?> Login(UserLoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return null;

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpirationDays = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");

            var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
            if (existingToken != null)
            {
                existingToken.Token = refreshToken;
                existingToken.Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            }
            else
            {
                _context.RefreshTokens.Add(new RefreshToken { Token = refreshToken, Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays), User = user });
            }

            await _context.SaveChangesAsync();

            await StoreAccessTokenInCache(token, user.Id.ToString());

            return new AuthResponseDto
            {
                AccessToken = token,
                RefreshToken = refreshToken,
                UserId = user.Id
            };
        }

        // Refresh Token ile yeni Access Token almak
        public async Task<AuthResponseDto?> RefreshToken(string token)
        {
            var storedToken = await _context.RefreshTokens.Include(rt => rt.User).FirstOrDefaultAsync(rt => rt.Token == token);
            if (storedToken == null || storedToken.IsExpired) return null;

            var newToken = GenerateJwtToken(storedToken.User);
            var newRefreshToken = GenerateRefreshToken();
            var refreshTokenExpirationDays = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");

            storedToken.Token = newRefreshToken;
            storedToken.Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            await _context.SaveChangesAsync();

            await StoreAccessTokenInCache(newToken, storedToken.User.Id.ToString());

            return new AuthResponseDto { AccessToken = newToken, RefreshToken = newRefreshToken };
        }

        // JWT Token'ı oluşturma
        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
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

        // Refresh Token'ı oluşturma
        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        // Redis'e Access Token'ı kaydetme
        private async Task StoreAccessTokenInCache(string accessToken, string userId)
        {
            var tokenExpiry = TimeSpan.FromMinutes(int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15"));
            await _cache.SetStringAsync(userId, accessToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = tokenExpiry
            });
        }

       public async Task<bool> Logout(string userId, string token)
        {
            // Redis'e token'ı kaydediyoruz, böylece bu token geçersiz sayılacak
            await _cache.SetStringAsync($"invalid_token:{token}", token, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) // Token'ın geçerliliği ne kadar sürede sonlanacaksa, o kadar süre
            });

            // Redis'teki geçersiz token'ı silme
            await _cache.RemoveAsync(userId);  // Kullanıcıya ait token'ı Redis'ten silmek
            return true;
        }


    }
}
