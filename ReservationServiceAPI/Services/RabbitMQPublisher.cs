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

    //her yerde yeni bağlantı oluşturma ** program.cs te uygulama yağa kalkarken configre edicez. singleton vb (DI).


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
    //masstransit bak ****

    public void PublishAddHotelEvent(Hotel hotel)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "add_hotel_queue",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

        // Otel nesnesini JSON formatına çeviriyoruz.
        var message = JsonConvert.SerializeObject(hotel);
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "",
                            routingKey: "add_hotel_queue",
                            basicProperties: null,
                            body: body);

        Console.WriteLine($" [x] RabbitMQ'ya otel ekleme mesajı gönderildi: {message}");
    }
    public void PublishUpdateHotelEvent(Hotel hotel)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "update_hotel_queue",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

        // Güncellenmiş otel nesnesini JSON formatına çeviriyoruz.
        var message = JsonConvert.SerializeObject(hotel);
        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(exchange: "",
                            routingKey: "update_hotel_queue",
                            basicProperties: null,
                            body: body);

        Console.WriteLine($" [x] RabbitMQ'ya otel güncelleme mesajı gönderildi: {message}");
    }


}
