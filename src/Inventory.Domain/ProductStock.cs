namespace Inventory.Domain;

// The stock of one product, rebuilt by replaying its events. Commands never
// change it directly: they return the events they would cause, and the caller
// stores those events and applies them.
public sealed class ProductStock
{
    private readonly Dictionary<string, int> reservations = new();

    public ProductStock(string productId)
    {
        ProductId = productId;
    }

    public string ProductId { get; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int Available => OnHand - Reserved;

    // Number of events applied so far, which is also the version of the stream.
    public int Version { get; private set; }

    public static ProductStock FromHistory(string productId, IEnumerable<InventoryEvent> history)
    {
        var stock = new ProductStock(productId);
        foreach (var e in history)
        {
            stock.Apply(e);
        }
        return stock;
    }

    public void Apply(InventoryEvent e)
    {
        switch (e)
        {
            case StockAdded added:
                OnHand += added.Quantity;
                break;
            case StockReserved reserved:
                Reserved += reserved.Quantity;
                reservations[reserved.OrderId] = reserved.Quantity;
                break;
            case ReservationReleased released:
                Reserved -= released.Quantity;
                reservations.Remove(released.OrderId);
                break;
            case ReservationRejected:
                break;
        }
        Version++;
    }

    public IReadOnlyList<InventoryEvent> AddStock(int quantity)
    {
        RequirePositive(quantity);
        return [new StockAdded(quantity)];
    }

    public IReadOnlyList<InventoryEvent> Reserve(string orderId, int quantity)
    {
        RequirePositive(quantity);
        // A retried request for an order that already holds stock changes nothing.
        if (reservations.ContainsKey(orderId))
        {
            return [];
        }
        return quantity <= Available
            ? [new StockReserved(orderId, quantity)]
            : [new ReservationRejected(orderId, quantity, Available)];
    }

    public IReadOnlyList<InventoryEvent> Release(string orderId) =>
        reservations.TryGetValue(orderId, out var quantity)
            ? [new ReservationReleased(orderId, quantity)]
            : [];

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidCommandException("Quantity must be greater than zero.");
        }
    }
}

public sealed class InvalidCommandException(string message) : Exception(message);
