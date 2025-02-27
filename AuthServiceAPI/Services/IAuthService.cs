using System.Threading.Tasks;
using AuthServiceAPI.Dtos;

namespace AuthServiceAPI.Services
{
    public interface IAuthService
    {
        Task<int?> Register(UserRegisterDto dto);

        Task<AuthResponseDto?> Login(UserLoginDto dto);

        Task<AuthResponseDto?> RefreshToken(string token);

        Task<UserDto?> GetUserById(int id);

        Task<bool> Logout(string userId, string token);

        Task<TokenValidationResponse?> ValidateTokenAsync(string token); // Token doğrulama metodu ekleniyor
    }
}
