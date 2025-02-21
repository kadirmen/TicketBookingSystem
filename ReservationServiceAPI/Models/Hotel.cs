using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Hotel
{
    [Key] // Primary Key olarak belirtiyoruz
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // PostgreSQL'de otomatik ID oluşturması için
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Elasticsearch ve PostgreSQL için ortak ID

    public required string Name { get; set; }

    
    public required string Location { get; set; }

    public double Rating { get; set; } = 0.0;

    // JSON olarak saklamak için (PostgreSQL desteği ile)
   
    [Column(TypeName = "text[]")]
    public List<string> Tags { get; set; } = new();

    [Column(TypeName = "text[]")]
    public List<string> Amenities { get; set; } = new();
}
