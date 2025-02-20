public class Hotel
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Location { get; set; }
    public double Rating { get; set; }

    // Yeni: Otel Özelliklerini Liste Olarak Tutuyoruz
    public required List<string> Tags { get; set; } // Örnek: ["İslamik Otel", "Evcil Hayvan Dostu", "Balayı Oteli"]

    public required List<string> Amenities { get; set; } // Örnek: ["Havuz", "Spa", "Ücretsiz WiFi"]
}
