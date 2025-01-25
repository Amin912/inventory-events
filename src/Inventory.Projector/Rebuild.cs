using Confluent.Kafka;
using Inventory.ReadModel;

namespace Inventory.Projector;

// Throws away the read model and builds it again from the whole event log.
// This is the point of keeping events: the views can be fixed, changed or
// added to at any time by replaying history. The replay uses a fresh consumer
// group, so it reads from the start of the topic and leaves the live
// projector's position alone.
public static class Rebuild
{
    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var views = services.GetRequiredService<ProductViews>();
        var worker = services.GetServices<IHostedService>().OfType<ProjectionWorker>().Single();
        var log = services.GetRequiredService<ILogger<ProjectionWorker>>();

        var removed = await views.ClearAsync();
        log.LogInformation("Cleared {Count} product views; replaying the log", removed);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            GroupId = $"inventory-rebuild-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            // Report reaching the end of each partition, so we know when we're done.
            EnablePartitionEof = true,
        }).Build();
        consumer.Subscribe(config["Kafka:Topic"] ?? "inventory-events");

        var finished = new HashSet<int>();
        var replayed = 0;
        while (!ct.IsCancellationRequested)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(10));
            if (result is null)
            {
                break;  // nothing at all for a while: an empty topic
            }
            if (result.IsPartitionEOF)
            {
                finished.Add(result.Partition.Value);
                if (consumer.Assignment.Count > 0 && consumer.Assignment.All(p => finished.Contains(p.Partition.Value)))
                {
                    break;
                }
                continue;
            }
            await worker.ProjectAsync(result.Message.Value);
            replayed++;
        }
        consumer.Close();

        var products = (await views.ListAsync()).Count;
        log.LogInformation("Replayed {Events} events into {Products} product views", replayed, products);
        return replayed;
    }
}
