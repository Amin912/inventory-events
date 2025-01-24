using Confluent.Kafka;
using Npgsql;

namespace Inventory.Api;

// Moves events from the outbox table to Kafka. Rows are locked while they are
// sent (SKIP LOCKED lets several API instances share the work) and marked as
// published only after Kafka has acknowledged them.
//
// If the process dies between sending and marking, the rows are sent again on
// restart. Delivery is therefore at least once, and consumers must cope with
// duplicates; the projector does, by event version.
public sealed class OutboxPublisher(
    NpgsqlDataSource db,
    IProducer<string, string> producer,
    IConfiguration config,
    ILogger<OutboxPublisher> log) : BackgroundService
{
    private const int BatchSize = 100;
    private readonly string topic = config["Kafka:Topic"] ?? "inventory-events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sent = await PublishBatchAsync(stoppingToken);
                if (sent == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Publishing the outbox failed; retrying shortly");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    public async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var rows = new List<(long Id, string ProductId, string Envelope)>();
        await using (var select = new NpgsqlCommand(
            $"SELECT id, product_id, envelope FROM outbox WHERE published_at IS NULL ORDER BY id LIMIT {BatchSize} FOR UPDATE SKIP LOCKED",
            connection, transaction))
        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }
        if (rows.Count == 0)
        {
            return 0;
        }

        // Keyed by product, so all events of a product land on the same
        // partition and are consumed in order.
        foreach (var row in rows)
        {
            await producer.ProduceAsync(topic, new Message<string, string> { Key = row.ProductId, Value = row.Envelope }, ct);
        }

        await using var update = new NpgsqlCommand(
            "UPDATE outbox SET published_at = now() WHERE id = ANY($1)", connection, transaction);
        update.Parameters.AddWithValue(rows.Select(r => r.Id).ToArray());
        await update.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return rows.Count;
    }
}
