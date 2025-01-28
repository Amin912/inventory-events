using Inventory.Domain;

namespace Inventory.Api;

// Runs a command against a product: load its events, rebuild its stock, let
// the stock decide what happens, and store the resulting events. If another
// request changed the product in between, the append fails and it starts over
// with the fresh history.
public sealed class InventoryCommands(EventStore store)
{
    // Under contention only one writer wins each round, so allow plenty.
    private const int MaxAttempts = 20;

    public async Task<IReadOnlyList<InventoryEvent>> HandleAsync(
        string productId, Func<ProductStock, IReadOnlyList<InventoryEvent>> decide, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var history = await store.LoadAsync(productId, ct);
            var stock = ProductStock.FromHistory(productId, history.Select(e => e.Event));
            var events = decide(stock);
            if (events.Count == 0)
            {
                return events;
            }
            try
            {
                await store.AppendAsync(productId, stock.Version, events, ct);
                return events;
            }
            catch (ConcurrencyException) when (attempt < MaxAttempts)
            {
                // A short random pause, so writers that collided don't all
                // retry at the same moment and collide again.
                await Task.Delay(Random.Shared.Next(1, 5 * attempt), ct);
            }
        }
    }
}
