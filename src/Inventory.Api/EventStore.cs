using Inventory.Domain;
using Npgsql;

namespace Inventory.Api;

public sealed class ConcurrencyException(string productId, int expectedVersion)
    : Exception($"Product {productId} changed after version {expectedVersion}.");

// Append-only store of events in Postgres, one stream per product. Every
// append also writes the events to an outbox table in the same transaction,
// so an event is stored if and only if it will be published.
public sealed class EventStore(NpgsqlDataSource db)
{
    // The columns are `json`, not `jsonb`: jsonb reorders keys, and the event
    // type has to stay the first property for deserialization.
    public async Task CreateSchemaAsync(CancellationToken ct = default)
    {
        await using var command = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS events (
                product_id  text        NOT NULL,
                version     int         NOT NULL,
                data        json        NOT NULL,
                recorded_at timestamptz NOT NULL,
                PRIMARY KEY (product_id, version)
            );
            CREATE TABLE IF NOT EXISTS outbox (
                id           bigserial   PRIMARY KEY,
                product_id   text        NOT NULL,
                envelope     json        NOT NULL,
                published_at timestamptz
            );
            """);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<EventEnvelope>> LoadAsync(string productId, CancellationToken ct = default)
    {
        await using var command = db.CreateCommand(
            "SELECT version, recorded_at, data FROM events WHERE product_id = $1 ORDER BY version");
        command.Parameters.AddWithValue(productId);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var events = new List<EventEnvelope>();
        while (await reader.ReadAsync(ct))
        {
            events.Add(new EventEnvelope(
                productId,
                reader.GetInt32(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                EventJson.DeserializeEvent(reader.GetString(2))));
        }
        return events;
    }

    // Appends events after `expectedVersion`. If another writer appended in the
    // meantime, the primary key rejects the duplicate version and nothing is kept.
    public async Task<IReadOnlyList<EventEnvelope>> AppendAsync(
        string productId, int expectedVersion, IReadOnlyList<InventoryEvent> events, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var envelopes = events
            .Select((e, i) => new EventEnvelope(productId, expectedVersion + i + 1, now, e))
            .ToList();

        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var envelope in envelopes)
            {
                await using var insertEvent = new NpgsqlCommand(
                    "INSERT INTO events (product_id, version, data, recorded_at) VALUES ($1, $2, $3::json, $4)",
                    connection, transaction);
                insertEvent.Parameters.AddWithValue(productId);
                insertEvent.Parameters.AddWithValue(envelope.Version);
                insertEvent.Parameters.AddWithValue(EventJson.Serialize(envelope.Event));
                insertEvent.Parameters.AddWithValue(envelope.RecordedAt);
                await insertEvent.ExecuteNonQueryAsync(ct);

                await using var insertOutbox = new NpgsqlCommand(
                    "INSERT INTO outbox (product_id, envelope) VALUES ($1, $2::json)", connection, transaction);
                insertOutbox.Parameters.AddWithValue(productId);
                insertOutbox.Parameters.AddWithValue(EventJson.Serialize(envelope));
                await insertOutbox.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConcurrencyException(productId, expectedVersion);
        }
        return envelopes;
    }
}
