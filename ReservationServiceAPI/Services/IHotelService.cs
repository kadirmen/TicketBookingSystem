using System.Collections.Generic;
using System.Threading.Tasks;

public interface IHotelService
{
    Task<bool> IndexHotelAsync(Hotel hotel);
    Task<List<Hotel>> SearchHotelsAsync(string keyword);
    Task<List<Hotel>> GetAllHotelsAsync();
    Task<bool> UpdateHotelAsync(Hotel hotel);
    Task<bool> DeleteHotelAsync(string id);
    Task<bool> MigrateHotelsToPostgres();
    Task<bool> MigrateHotelsToElasticSearch();

}
