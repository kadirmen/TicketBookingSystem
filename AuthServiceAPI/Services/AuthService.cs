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
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IDatabase _cache;

        public AuthService(AppDbContext context, IConfiguration config, IConnectionMultiplexer redis)
        {
            _context = context;
            _config = config;
            _cache = redis.GetDatabase();
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

        public async Task<AuthResponseDto?> RefreshToken(string token)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (storedToken == null || storedToken.IsExpired)
                return null;

            var newAccessToken = GenerateJwtToken(storedToken.User);
            var newRefreshToken = GenerateRefreshToken();
            var refreshTokenExpirationDays = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");

            storedToken.Token = newRefreshToken;
            storedToken.Expires = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
            await _context.SaveChangesAsync();

            await StoreAccessTokenInCache(newAccessToken, storedToken.User.Id.ToString());

            return new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

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
            var expiryTime = GetTokenExpiry(token);
            if (expiryTime == null) return false;

            var remainingTime = expiryTime.Value - DateTime.UtcNow;

            await _cache.StringSetAsync($"blacklist:{token}", "true", new TimeSpan(remainingTime.Ticks));

            await _cache.KeyDeleteAsync($"access_token:{userId}");

            var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId.ToString() == userId);
            if (refreshToken != null)
            {
                _context.RefreshTokens.Remove(refreshToken);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> IsTokenBlacklisted(string token)
        {
            return await _cache.KeyExistsAsync($"blacklist:{token}");
        }

        private DateTime? GetTokenExpiry(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            return jwtToken?.ValidTo;
        }
    }
}
