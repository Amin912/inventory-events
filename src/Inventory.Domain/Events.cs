using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventory.Domain;

// Everything that can happen to a product's stock. Events are facts: they are
// stored forever and never changed, and the current state of a product is
// whatever its events add up to.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StockAdded), nameof(StockAdded))]
[JsonDerivedType(typeof(StockReserved), nameof(StockReserved))]
[JsonDerivedType(typeof(ReservationRejected), nameof(ReservationRejected))]
[JsonDerivedType(typeof(ReservationReleased), nameof(ReservationReleased))]
public abstract record InventoryEvent;

public sealed record StockAdded(int Quantity) : InventoryEvent;

public sealed record StockReserved(string OrderId, int Quantity) : InventoryEvent;

// Kept even though it changes nothing, so the log shows every order that was
// turned away and why.
public sealed record ReservationRejected(string OrderId, int Requested, int Available) : InventoryEvent;

public sealed record ReservationReleased(string OrderId, int Quantity) : InventoryEvent;

// An event as it is stored and published: the product (stream) it belongs to
// and its position in that stream, starting at 1.
public sealed record EventEnvelope(string ProductId, int Version, DateTimeOffset RecordedAt, InventoryEvent Event);

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(InventoryEvent e) => JsonSerializer.Serialize(e, Options);

    public static InventoryEvent DeserializeEvent(string json) =>
        JsonSerializer.Deserialize<InventoryEvent>(json, Options)
        ?? throw new JsonException("Empty event");

    public static string Serialize(EventEnvelope envelope) => JsonSerializer.Serialize(envelope, Options);

    public static EventEnvelope DeserializeEnvelope(string json) =>
        JsonSerializer.Deserialize<EventEnvelope>(json, Options)
        ?? throw new JsonException("Empty envelope");
}
