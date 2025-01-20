using Confluent.Kafka;
using DataReader.models;
using Newtonsoft.Json;

namespace DataReader.KafkaImpl;

public class InventoryEventProducer
{
    private readonly IProducer<string, string> _producer;
    private const string TOPIC = "inventory-events";
    private ProducerConfig _config = new()
    {
        BootstrapServers = "localhost:9092",
        ClientId = "inventory-service",
        Acks = Acks.All,
        MessageSendMaxRetries = 3,
        EnableIdempotence = true
    };


    public InventoryEventProducer()
    {
            _producer = new ProducerBuilder<string, string>(
                                  _config
                              ).Build();
    }

    public async Task PublishInventoryEventAsync(InventoryEvent evt)
    {
        var message = new Message<string, string>
        {
            Key = evt.ProductId,
            Value = JsonConvert.SerializeObject(evt)
        };

        await _producer.ProduceAsync(TOPIC, message);
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}