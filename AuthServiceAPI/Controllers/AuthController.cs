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

            if (result.Contains("exists"))
            {
                return BadRequest(new { message = result });
            }

            var id = result.Split("ID: ").Last();
            return CreatedAtAction(nameof(Register), new { id }, new { message = "User registered successfully", id });
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