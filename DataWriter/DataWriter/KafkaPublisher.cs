using Confluent.Kafka;
using Newtonsoft.Json;

namespace DataWriter;

public class KafkaPublisher
{
    private readonly IProducer<string, string> _producer;
    private string _topic;
    private ProducerConfig _config = new()
    {
        BootstrapServers = "localhost:9092",
        ClientId = "inventory-service",
        Acks = Acks.All,
        MessageSendMaxRetries = 3,
        EnableIdempotence = true
    };

    public KafkaPublisher(string topic)
    {
        _producer = new ProducerBuilder<string, string>(
            _config
        ).Build();
        _topic = topic;
    }

    public async void Publish(Order order)
    {
        Guid orderId = Guid.NewGuid();
        var message = new Message<string, string>
        {
            Key = orderId.ToString(),
            Value = JsonConvert.SerializeObject(order)
        };
         var deliveryResult = await _producer.ProduceAsync(_topic, message);
        Console.WriteLine($"Message sent to Kafka: {deliveryResult.Value}");
    }
}