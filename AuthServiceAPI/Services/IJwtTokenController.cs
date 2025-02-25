using System.Security.Claims;

public interface IJwtTokenValidator
{
    ClaimsPrincipal? ValidateToken(string token);
}
