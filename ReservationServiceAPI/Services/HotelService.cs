using Nest;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

public class HotelService : IHotelService
{
    private readonly IElasticClient _elasticClient;
    private readonly AppDbContext _dbContext;
    private const string IndexName = "hotels";

    public HotelService(IElasticClient elasticClient, AppDbContext dbContext)
    {
        _elasticClient = elasticClient;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Otel ekleme işlemi: Önce PostgreSQL'e, sonra Elasticsearch'e kaydedilir.
    /// </summary>
    public async Task<bool> IndexHotelAsync(Hotel hotel)
    {
        try
        {
            // 1️⃣ PostgreSQL'e ekle
            _dbContext.Hotels.Add(hotel);
            await _dbContext.SaveChangesAsync();

            // 2️⃣ Elasticsearch'e ekle
            var response = await _elasticClient.IndexAsync(hotel, idx => idx.Index(IndexName));

            return response.IsValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (IndexHotelAsync): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Elasticsearch'te otel arama işlemi.
    /// </summary>
    public async Task<List<Hotel>> SearchHotelsAsync(string keyword)
    {
        var response = await _elasticClient.SearchAsync<Hotel>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field(h => h.Name)
                        .Field(h => h.Location)
                        .Field(h => h.Tags)
                    )
                    .Query(keyword)
                )
            )
        );

        return response.Documents.ToList();
    }

    /// <summary>
    /// Tüm otelleri getir: PostgreSQL'den çekilir.
    /// </summary>
    public async Task<List<Hotel>> GetAllHotelsAsync()
    {
        return await _dbContext.Hotels.ToListAsync();
    }

    /// <summary>
    /// Otel güncelleme işlemi: PostgreSQL ve Elasticsearch üzerinde güncellenir.
    /// </summary>
    public async Task<bool> UpdateHotelAsync(Hotel hotel)
    {
        try
        {
            // 1️⃣ PostgreSQL'de güncelle
            var existingHotel = await _dbContext.Hotels.FindAsync(hotel.Id);
            if (existingHotel == null) return false;

            existingHotel.Name = hotel.Name;
            existingHotel.Location = hotel.Location;
            existingHotel.Rating = hotel.Rating;
            existingHotel.Tags = hotel.Tags;
            existingHotel.Amenities = hotel.Amenities;

            _dbContext.Hotels.Update(existingHotel);
            await _dbContext.SaveChangesAsync();

            // 2️⃣ Elasticsearch'te güncelle
            var response = await _elasticClient.UpdateAsync<Hotel>(hotel.Id, u => u
                .Index(IndexName)
                .Doc(hotel)
            );

            return response.IsValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (UpdateHotelAsync): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Otel silme işlemi: PostgreSQL ve Elasticsearch üzerinden kaldırılır.
    /// </summary>
    public async Task<bool> DeleteHotelAsync(string id)
    {
        try
        {
            // 1️⃣ PostgreSQL'den sil
            var hotel = await _dbContext.Hotels.FindAsync(id);
            if (hotel == null) return false;

            _dbContext.Hotels.Remove(hotel);
            await _dbContext.SaveChangesAsync();

            // 2️⃣ Elasticsearch'ten sil
            var response = await _elasticClient.DeleteAsync<Hotel>(id, d => d.Index(IndexName));

            return response.IsValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (DeleteHotelAsync): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Elasticsearch'ten PostgreSQL'e veri taşıma.
    /// </summary>
    public async Task<bool> MigrateHotelsToPostgres()
    {
        try
        {
            Console.WriteLine("Elasticsearch'ten veriler alınıyor...");
            
            // 1️⃣ Elasticsearch'ten tüm otelleri çek
            var response = await _elasticClient.SearchAsync<Hotel>(s => s
                .Index(IndexName)
                .Query(q => q.MatchAll())
                .Size(1000) 
            );

            var hotels = response.Documents.ToList();

            if (!hotels.Any())
            {
                Console.WriteLine("Elasticsearch'te hiç veri bulunamadı.");
                return false;
            }

            Console.WriteLine($"{hotels.Count} otel bulundu, PostgreSQL'e ekleniyor...");

            foreach (var hotel in hotels)
            {
                var existingHotel = await _dbContext.Hotels.FindAsync(hotel.Id);
                if (existingHotel == null)
                {
                    // PostgreSQL için List<string> olarak dönüştürüyoruz
                    hotel.Tags = hotel.Tags ?? new List<string>();
                    hotel.Amenities = hotel.Amenities ?? new List<string>();

                    _dbContext.Hotels.Add(hotel);
                }
                else
                {
                    existingHotel.Name = hotel.Name;
                    existingHotel.Location = hotel.Location;
                    existingHotel.Rating = hotel.Rating;
                    existingHotel.Tags = hotel.Tags ?? new List<string>();
                    existingHotel.Amenities = hotel.Amenities ?? new List<string>();
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine("Tüm oteller PostgreSQL'e başarıyla aktarıldı!");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (MigrateHotelsToPostgres): {ex.Message}");
            return false;
        }
    }

}
