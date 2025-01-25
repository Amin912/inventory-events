using System.Text.Json;
using Confluent.Kafka;
using Inventory.Domain;
using Inventory.ReadModel;

namespace Inventory.Projector;

// Reads the event log from Kafka and keeps the product views in Redis up to
// date. The offset is committed only after a view is saved, so a crash means
// replaying a few events, never missing one; ProductView.Apply skips the
// replays.
public sealed class ProjectionWorker(ProductViews views, IConfiguration config, ILogger<ProjectionWorker> log)
    : BackgroundService
{
    // Consume() blocks, so the loop runs on its own thread instead of holding
    // up the host's startup.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => RunAsync(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken ct)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            GroupId = config["Kafka:GroupId"] ?? "inventory-projector",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(config["Kafka:Topic"] ?? "inventory-events");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(ct);
                    await ProjectAsync(result.Message.Value);
                    consumer.Commit(result);
                }
                catch (ConsumeException e) when (!e.Error.IsFatal)
                {
                    // For example the topic does not exist yet on a fresh start.
                    log.LogWarning("Kafka: {Reason}; retrying", e.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    public async Task ProjectAsync(string message)
    {
        EventEnvelope envelope;
        try
        {
            envelope = EventJson.DeserializeEnvelope(message);
        }
        catch (JsonException e)
        {
            // Retrying a malformed message would block the partition forever.
            log.LogError(e, "Skipping a message that is not a valid event");
            return;
        }

        var next = ProductView.Apply(await views.GetAsync(envelope.ProductId), envelope);
        if (next is not null)
        {
            await views.SaveAsync(next);
        }
    }
}
