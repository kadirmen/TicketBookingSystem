using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthServiceAPI.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        [HttpGet("profile")]
        [Authorize] // **Giriş yapan herkes erişebilir**
        public IActionResult GetProfile()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(username))
                return Unauthorized("Kullanıcı bilgisi alınamadı.");

            return Ok(new { username, role });
        }

        [HttpGet("all-users")]
        [Authorize(Roles = "Admin")] // **Sadece Admin erişebilir**
        public IActionResult GetAllUsers()
        {
            return Ok("Tüm kullanıcıları listeleme yetkin var!");
        }
    }
}