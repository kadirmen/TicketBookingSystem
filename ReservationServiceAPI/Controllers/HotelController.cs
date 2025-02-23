using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class HotelController : ControllerBase
{
    private readonly IHotelService _hotelService;

    public HotelController(IHotelService hotelService)
    {
        _hotelService = hotelService;
    }

    [HttpPost("only-postgres-save")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddHotelToPostgres([FromBody] Hotel hotel)
    {
        var success = await _hotelService.SaveHotelToPostgresAsync(hotel);
        return success ? CreatedAtAction(nameof(GetAllHotels), new { id = hotel.Id }, hotel) : BadRequest("Hotel PostgreSQL'e kaydedilemedi.");
    }

    [HttpPost(" PostgreSQL-and-Elastic-Save")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddHotel([FromBody] Hotel hotel)
    {
        var success = await _hotelService.IndexHotelAsync(hotel);
        return success ? CreatedAtAction(nameof(GetAllHotels), new { id = hotel.Id }, hotel) : BadRequest("Hotel could not be indexed.");
    }

    [HttpPost("Elastic-to-PostgreSQL-migrate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateHotelsToPostgres()
    {
        var success = await _hotelService.MigrateHotelsToPostgres();
        return success ? Ok("Tüm oteller PostgreSQL'e taşındı.") : BadRequest("Veri aktarımı başarısız.");
    }
    [HttpPost("Postgre-to-Elastic-Migrate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateHotelsToElasticSearch()
    {
        var success = await _hotelService.MigrateHotelsToElasticSearch();
        return success ? Ok("Tüm oteller Elasticsearch'e aktarıldı.") : BadRequest("Veri aktarımı başarısız.");
    }

 


    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetAllHotels()
    {
        var hotels = await _hotelService.GetAllHotelsAsync();
        return Ok(hotels);
    }

    [HttpGet("search-Elastic")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> SearchHotels([FromQuery] string keyword)
    {
        var hotels = await _hotelService.SearchHotelsAsync(keyword);
        return Ok(hotels);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateHotel([FromBody] Hotel hotel)
    {
        var success = await _hotelService.UpdateHotelAsync(hotel);
        return success ? Ok("Hotel updated.") : NotFound("Hotel not found.");
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteHotel(string id)
    {
        var success = await _hotelService.DeleteHotelAsync(id);
        return success ? Ok("Hotel deleted.") : NotFound("Hotel not found.");
    }
}
