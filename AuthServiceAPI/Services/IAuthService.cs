using System.Threading.Tasks;
using AuthServiceAPI.Dtos;

namespace AuthServiceAPI.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Kullanıcı kaydı yapar.
        /// </summary>
        /// <param name="dto">Kullanıcı kayıt bilgileri</param>
        /// <returns>Kullanıcının ID'si</returns>
        Task<int?> Register(UserRegisterDto dto);

        /// <summary>
        /// Kullanıcı giriş işlemi yapar ve JWT token döner.
        /// </summary>
        /// <param name="dto">Giriş bilgileri</param>
        /// <returns>Access Token ve Refresh Token</returns>
        Task<AuthResponseDto?> Login(UserLoginDto dto);

        /// <summary>
        /// Refresh Token kullanarak yeni bir Access Token üretir.
        /// </summary>
        /// <param name="token">Mevcut Refresh Token</param>
        /// <returns>Yeni Access Token ve Refresh Token</returns>
        Task<AuthResponseDto?> RefreshToken(string token);

        /// <summary>
        /// Kullanıcı ID'sine göre kullanıcı bilgilerini döner.
        /// </summary>
        /// <param name="id">Kullanıcı ID</param>
        /// <returns>Kullanıcı bilgileri</returns>
        Task<UserDto?> GetUserById(int id);

        /// <summary>
        /// Kullanıcının oturumunu kapatır ve token'ı geçersiz kılar.
        /// </summary>
        /// <param name="userId">Kullanıcı ID</param>
        /// <param name="token">Access Token</param>
        /// <returns>Başarı durumunu döner</returns>
        Task<bool> Logout(string userId, string token);

        /// <summary>
        /// Token'ın blacklist'te olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="token">JWT Token</param>
        /// <returns>Blacklist durumunu döner</returns>
        Task<bool> IsTokenBlacklisted(string token);
    }
}
