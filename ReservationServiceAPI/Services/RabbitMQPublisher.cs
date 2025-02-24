using System;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

public class RabbitMQPublisher
{
    private readonly ConnectionFactory _factory;

    public RabbitMQPublisher()
    {
        _factory = new ConnectionFactory() { HostName = "localhost" }; // RabbitMQ Docker ile çalışıyor.
    }

    public void PublishDeleteHotelEvent(string hotelId)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "delete_hotel_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var message = JsonConvert.SerializeObject(new { HotelId = hotelId });
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "",
                             routingKey: "delete_hotel_queue",
                             basicProperties: null,
                             body: body);

        Console.WriteLine($" [x] RabbitMQ'ya mesaj gönderildi: {message}");
    }
}
