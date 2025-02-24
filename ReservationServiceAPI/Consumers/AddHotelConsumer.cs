using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Nest;

public class AddHotelConsumer
{
    private readonly IElasticClient _elasticClient;
    private readonly ConnectionFactory _factory;

    public AddHotelConsumer(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
        _factory = new ConnectionFactory() { HostName = "localhost" };
    }

    public void StartListening()
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "add_hotel_queue",
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

            Console.WriteLine($" [x] RabbitMQ'dan otel ekleme mesajı alındı: {message}");

            // Elasticsearch'e oteli ekliyoruz.
            var response = await _elasticClient.IndexAsync(hotel, idx => idx.Index("hotels"));
            if (response.IsValid)
            {
                Console.WriteLine($" [✓] Elasticsearch'e otel eklendi: {hotel.Id}");
            }
            else
            {
                Console.WriteLine($" [X] Elasticsearch otel ekleme hatası: {response.ServerError}");
            }
        };

        channel.BasicConsume(queue: "add_hotel_queue",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine(" [*] AddHotelConsumer başlatıldı, mesaj bekleniyor...");
        Console.ReadLine();
    }
}
