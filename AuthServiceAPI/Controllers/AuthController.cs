using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthServiceAPI.Dtos;
using AuthServiceAPI.Services;

namespace AuthServiceAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            var result = await _authService.Register(dto);

            if (result == null) 
            {
                return Conflict(new { message = "Username already exists" }); // 409 Conflict
            }

            return CreatedAtAction(nameof(GetUserById), new { id = result }, new { userId = result });
        }




        [HttpGet("user/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _authService.GetUserById(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
            return Ok(user);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var result = await _authService.Login(dto);
            if (result == null) return Unauthorized(new { message = "Invalid credentials" });

            return Ok(new 
            { 
                accessToken = result.AccessToken, 
                refreshToken = result.RefreshToken, 
                userId = result.UserId 
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            var result = await _authService.RefreshToken(refreshToken);
            if (result == null) return Unauthorized("Invalid or expired refresh token");
            return Ok(result);
        }
    }
}