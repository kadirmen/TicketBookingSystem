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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddHotel([FromBody] Hotel hotel)
    {
        var success = await _hotelService.IndexHotelAsync(hotel);
        return success ? CreatedAtAction(nameof(GetAllHotels), new { id = hotel.Id }, hotel) : BadRequest("Hotel could not be indexed.");
    }

    [HttpPost("migrate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateHotelsToPostgres()
    {
        var success = await _hotelService.MigrateHotelsToPostgres();
        return success ? Ok("Tüm oteller PostgreSQL'e taşındı.") : BadRequest("Veri aktarımı başarısız.");
    }



    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetAllHotels()
    {
        var hotels = await _hotelService.GetAllHotelsAsync();
        return Ok(hotels);
    }

    [HttpGet("search")]
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
