namespace Inventory.Domain;

// What the read side shows for a product, kept up to date from its events.
public sealed record ProductView(string ProductId, int OnHand, int Reserved, int Version)
{
    public int Available => OnHand - Reserved;

    // Returns the view after `envelope`, or null if the event is already
    // included. Kafka delivers at least once, so the same event can arrive
    // twice; its version tells us.
    public static ProductView? Apply(ProductView? current, EventEnvelope envelope)
    {
        var view = current ?? new ProductView(envelope.ProductId, 0, 0, 0);
        if (envelope.Version <= view.Version)
        {
            return null;
        }
        view = view with { Version = envelope.Version };
        return envelope.Event switch
        {
            StockAdded added => view with { OnHand = view.OnHand + added.Quantity },
            StockReserved reserved => view with { Reserved = view.Reserved + reserved.Quantity },
            ReservationReleased released => view with { Reserved = view.Reserved - released.Quantity },
            _ => view,
        };
    }
}
