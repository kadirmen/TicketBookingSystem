using Nest;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class HotelService : IHotelService
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "hotels";

    public HotelService(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task<bool> IndexHotelAsync(Hotel hotel)
    {
        var response = await _elasticClient.IndexAsync(hotel, idx => idx.Index(IndexName));
        return response.IsValid;
    }

    public async Task<List<Hotel>> SearchHotelsAsync(string keyword)
    {
        var response = await _elasticClient.SearchAsync<Hotel>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field(h => h.Name)
                        .Field(h => h.Location)
                        .Field(h => h.Tags) // Artık Tags içinde de arama yapıyor!
                    )
                    .Query(keyword)
                )
            )
        );

        return response.Documents.ToList();
    }

    public async Task<List<Hotel>> GetAllHotelsAsync()
    {
        var response = await _elasticClient.SearchAsync<Hotel>(s => s.Index(IndexName).Query(q => q.MatchAll()));
        return response.Documents.ToList();
    }

    public async Task<bool> UpdateHotelAsync(Hotel hotel)
    {
        var response = await _elasticClient.UpdateAsync<Hotel>(hotel.Id, u => u.Index(IndexName).Doc(hotel));
        return response.IsValid;
    }

    public async Task<bool> DeleteHotelAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<Hotel>(id, d => d.Index(IndexName));
        return response.IsValid;
    }
}
