using AuthServiceAPI.Dtos;

namespace AuthServiceAPI.Services
{
    public interface IAuthService
    {
        Task<int?> Register(UserRegisterDto dto); // Kullanıcı kaydı
        Task<AuthResponseDto?> Login(UserLoginDto dto); // Kullanıcı girişi
        Task<AuthResponseDto?> RefreshToken(string token); // Refresh Token ile Access Token yenileme
        Task<UserDto?> GetUserById(int id); // Kullanıcı bilgilerini ID ile alma
        
        // Logout işlemi: Kullanıcı çıkışı yapacak ve Redis'teki token geçersiz kılınacak
        Task<bool> Logout(string userId,string token); 
    }
}
