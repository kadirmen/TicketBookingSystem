using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Hotel> Hotels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // List<string> (Tags) -> PostgreSQL text[] dönüşümü
        modelBuilder.Entity<Hotel>()
            .Property(h => h.Tags)
            .HasConversion(
                v => v.ToArray(),  // List<string> -> string[]
                v => v == null ? new List<string>() : v.ToList() // string[] -> List<string>
            );

        // List<string> (Amenities) -> PostgreSQL text[] dönüşümü
        modelBuilder.Entity<Hotel>()
            .Property(h => h.Amenities)
            .HasConversion(
                v => v.ToArray(),
                v => v == null ? new List<string>() : v.ToList()
            );
    }
}
