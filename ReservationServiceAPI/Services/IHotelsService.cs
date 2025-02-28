using System.Collections.Generic;
using System.Threading.Tasks;


public interface IHotelsService
{
    Task<bool> IndexHotelAsync(Hotel hotel);
    Task<bool> IndexHotelsAsync(List<Hotel> hotels);
    Task<List<Hotel>> SearchHotelsAsync(string keyword,int page,int pageSize);
    Task<List<Hotel>> GetAllHotelsAsync();
    Task<Hotel?> GetHotelByIdAsync(string id); // Yeni metod
    Task<bool> UpdateHotelAsync(Hotel hotel);
    Task<bool> DeleteHotelAsync(string id);
    Task<bool> MigrateHotelsToPostgres();
    Task<bool> MigrateHotelsToElasticSearch();

    Task<bool> SaveHotelToPostgresAsync(Hotel hotel);

}
