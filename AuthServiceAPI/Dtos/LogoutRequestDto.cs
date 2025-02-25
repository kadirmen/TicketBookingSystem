public class LogoutRequestDto
{
    public string? UserId { get; set; }  // Kullanıcı ID'si
    public string? Token { get; set; }   // Geçerli JWT token
}
