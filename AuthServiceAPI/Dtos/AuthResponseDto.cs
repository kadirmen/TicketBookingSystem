namespace AuthServiceAPI.Dtos
{
    public class AuthResponseDto
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int UserId { get; set; }
    }
}