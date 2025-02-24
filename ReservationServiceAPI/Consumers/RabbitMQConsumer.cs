using System;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Nest;

public class RabbitMQConsumer
{
    private readonly IElasticClient _elasticClient;
    private readonly ConnectionFactory _factory;

    public RabbitMQConsumer(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
        _factory = new ConnectionFactory() { HostName = "localhost" };
    }

    public void StartListening()
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "delete_hotel_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var hotelData = JsonConvert.DeserializeObject<HotelDeleteEvent>(message);

            Console.WriteLine($" [x] RabbitMQ'dan mesaj alındı: {message}");

            //  ElasticSearch’ten oteli sil
            var response = await _elasticClient.DeleteAsync<Hotel>(hotelData.HotelId, d => d.Index("hotels"));
            
            if (response.IsValid)
            {
                Console.WriteLine($" [✓] ElasticSearch’ten otel silindi: {hotelData.HotelId}");
            }
            else
            {
                Console.WriteLine($" [X] ElasticSearch otel silme hatası: {response.ServerError}");
            }
        };

        channel.BasicConsume(queue: "delete_hotel_queue",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine(" [*] RabbitMQ Consumer başlatıldı, mesaj bekleniyor...");
        Console.ReadLine();
    }
}

public class HotelDeleteEvent
{
    public string HotelId { get; set; }
}
