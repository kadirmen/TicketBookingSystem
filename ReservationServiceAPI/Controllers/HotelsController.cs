using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/hotels")]
[ApiController]
public class HotelsController : ControllerBase
{
    private readonly IHotelsService _hotelsService;

    public HotelsController(IHotelsService hotelsService)
    {
        _hotelsService = hotelsService;
    }

    /// <summary>
    /// Yeni bir otel ekler (sadece PostgreSQL'e kaydeder).
    /// </summary>
    [HttpPost("only-postgresql-save")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateHotel([FromBody] Hotel hotel)
    {
        var success = await _hotelsService.SaveHotelToPostgresAsync(hotel);
        return success ? CreatedAtAction(nameof(GetHotelById), new { id = hotel.Id }, hotel) : BadRequest("Otel PostgreSQL'e kaydedilemedi.");
    }

    /// <summary>
    /// Bir oteli hem PostgreSQL hem de ElasticSearch'e kaydeder.
    /// </summary>
    [HttpPost("postgresql-and-elastic-save")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateHotelWithElastic([FromBody] Hotel hotel)
    {
        var success = await _hotelsService.IndexHotelAsync(hotel);
        return success ? CreatedAtAction(nameof(GetHotelById), new { id = hotel.Id }, hotel) : BadRequest("Otel kaydedilemedi.");
    }

    /// <summary>
    /// PostgreSQL'deki tüm otelleri getirir.
    /// </summary>
    [HttpGet("postgresql")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetAllHotels()
    {
        var hotels = await _hotelsService.GetAllHotelsAsync();
        return Ok(hotels);
    }

    /// <summary>
    /// Belirtilen oteli ID'ye göre getirir.
    /// </summary>
    [HttpGet("{id}")]
    //[Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetHotelById(string id)
    {
        var hotel = await _hotelsService.GetHotelByIdAsync(id);
        return hotel != null ? Ok(hotel) : NotFound("Otel bulunamadı.");
    }


    /// <summary>
    /// ElasticSearch üzerinde otel araması yapar.
    /// </summary>
    [HttpGet("search-with-elastic")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> SearchHotels([FromQuery] string keyword, int? page, int? pageSize)
        {
           int size = pageSize ?? 2;
           int pages = page ?? 1;

            var hotels = await _hotelsService.SearchHotelsAsync(keyword, pages, size);
             if (hotels == null || !hotels.Any()) 
                {
                    return NotFound("No hotels found matching the search criteria.");
                }
            return Ok(hotels); // HTTP 200 OK döndür ve otel listesini JSON olarak gönder
        }


    /// <summary>
    /// PostgreSQL'deki tüm otelleri ElasticSearch'e taşır.
    /// </summary>
    [HttpPost("migrate-to-elastic")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateHotelsToElasticSearch()
    {
        var success = await _hotelsService.MigrateHotelsToElasticSearch();
        return success ? Ok("Tüm oteller Elasticsearch'e aktarıldı.") : BadRequest("Veri aktarımı başarısız.");
    }

    /// <summary>
    /// ElasticSearch'teki tüm otelleri PostgreSQL'e taşır.
    /// </summary>
    [HttpPost("migrate-to-postgres")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateHotelsToPostgres()
    {
        var success = await _hotelsService.MigrateHotelsToPostgres();
        return success ? Ok("Tüm oteller PostgreSQL'e taşındı.") : BadRequest("Veri aktarımı başarısız.");
    }

    /// <summary>
    /// Belirtilen otelin bilgilerini günceller.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateHotel(string id, [FromBody] Hotel hotel)
    {
        if (id != hotel.Id)
            return BadRequest("ID uyuşmuyor.");

        var success = await _hotelsService.UpdateHotelAsync(hotel);
        return success ? Ok("Otel güncellendi.") : NotFound("Otel bulunamadı.");
    }

    /// <summary>
    /// Belirtilen oteli siler.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteHotel(string id)
    {
        var success = await _hotelsService.DeleteHotelAsync(id);
        return success ? Ok("Otel silindi.") : NotFound("Otel bulunamadı.");
    }
}
