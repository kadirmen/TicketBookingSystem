using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthServiceAPI.Dtos;
using AuthServiceAPI.Services;
using System.Security.Claims;
using AuthServiceAPI.Models;

namespace AuthServiceAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtTokenValidator _jwtValidator;

        public AuthController(IAuthService authService, IJwtTokenValidator jwtValidator)
        {
            _authService = authService;
            _jwtValidator = jwtValidator;
        }

        // Kullanıcı kaydı (Register)
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(object))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(object))]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            var result = await _authService.Register(dto);

            if (result == null)
            {
                return Conflict(new { message = "Username already exists" });
            }

            return CreatedAtAction(nameof(GetUserById), new { id = result }, new { userId = result });
        }

        // Kullanıcı bilgilerini ID ile almak (GetUserById)
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

        // Kullanıcı girişi (Login)
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        // Refresh Token ile yeni Access Token almak
        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponseDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            var result = await _authService.RefreshToken(refreshToken);
            if (result == null) return Unauthorized("Invalid or expired refresh token");
            return Ok(result);
        }

        // Kullanıcı çıkış işlemi (Logout)
        // Kullanıcı çıkış işlemi (Logout)
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto)
        {
            var result = await _authService.Logout(dto.UserId, dto.Token);
            if (!result)
            {
                return BadRequest(new { message = "Logout failed" });
            }

            return Ok(new { message = "Successfully logged out" });
        }

         [HttpPost("validate")]
        public IActionResult ValidateToken([FromBody] TokenValidationRequest request)
        {
            var claimsPrincipal = _jwtValidator.ValidateToken(request.Token);
            if (claimsPrincipal == null)
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = claimsPrincipal.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new { UserId = userId, Role = role });
        }

        [HttpGet("is-blacklisted")]
        public async Task<IActionResult> IsTokenBlacklisted([FromQuery] string token)
        {
            bool isBlacklisted = await _authService.IsTokenBlacklisted(token);
            return Ok(isBlacklisted);
        }

    }
}
