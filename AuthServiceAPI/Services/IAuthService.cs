using AuthServiceAPI.Dtos;

namespace AuthServiceAPI.Services
{
    public interface IAuthService
    {
        Task<int?> Register(UserRegisterDto dto);
        Task<AuthResponseDto?> Login(UserLoginDto dto);
        Task<AuthResponseDto?> RefreshToken(string token);
        Task<UserDto?> GetUserById(int id);
    }
}