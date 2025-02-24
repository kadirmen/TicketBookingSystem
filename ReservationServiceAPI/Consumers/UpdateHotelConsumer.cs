using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Nest;

public class UpdateHotelConsumer
{
    private readonly IElasticClient _elasticClient;
    private readonly ConnectionFactory _factory;

    public UpdateHotelConsumer(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
        _factory = new ConnectionFactory() { HostName = "localhost" };
    }

    public void StartListening()
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "update_hotel_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var hotel = JsonConvert.DeserializeObject<Hotel>(message);

            Console.WriteLine($" [x] RabbitMQ'dan otel güncelleme mesajı alındı: {message}");

            // Elasticsearch'te oteli güncelle
            var response = await _elasticClient.UpdateAsync<Hotel>(hotel.Id, u => u
                .Index("hotels")
                .Doc(hotel)
            );

            if (response.IsValid)
            {
                Console.WriteLine($" [✓] Elasticsearch'te otel güncellendi: {hotel.Id}");
            }
            else
            {
                Console.WriteLine($" [X] Elasticsearch otel güncelleme hatası: {response.ServerError}");
            }
        };

        channel.BasicConsume(queue: "update_hotel_queue",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine(" [*] UpdateHotelConsumer başlatıldı, mesaj bekleniyor...");
        Console.ReadLine();
    }
}
