namespace AuthServiceAPI.Data.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public required string Token { get; set; }
        public DateTime Expires { get; set; }
        public bool IsExpired => DateTime.UtcNow >= Expires;
        public int UserId { get; set; }
        public required User User { get; set; }
    }
}