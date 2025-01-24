using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Inventory.Api;

public static class KafkaSetup
{
    public static IProducer<string, string> CreateProducer(IConfiguration config) =>
        new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            // Retries never duplicate or reorder messages within a partition.
            EnableIdempotence = true,
            Acks = Acks.All,
        }).Build();

    // Creates the topic with a few partitions if it does not exist yet.
    public static async Task EnsureTopicAsync(IConfiguration config, int partitions = 3)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
        }).Build();
        try
        {
            await admin.CreateTopicsAsync([
                new TopicSpecification
                {
                    Name = config["Kafka:Topic"] ?? "inventory-events",
                    NumPartitions = partitions,
                    ReplicationFactor = 1,
                },
            ]);
        }
        catch (CreateTopicsException e) when (e.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }
}
