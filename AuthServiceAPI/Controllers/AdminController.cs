using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServiceAPI.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")] // **Sadece Admin yetkisine sahip kişiler erişebilir**
    public class AdminController : ControllerBase
    {
        [HttpGet("dashboard")]
        public IActionResult GetAdminDashboard()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (role != "Admin")
            {
                return Forbid("Bu işlem sadece Adminler içindir.");
            }

            return Ok("Admin paneline eriştiniz!");
        }
    }
}