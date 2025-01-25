using Inventory.Domain;

namespace Inventory.Domain.Tests;

public class ProductViewTests
{
    private static EventEnvelope At(int version, InventoryEvent e) => new("p1", version, DateTimeOffset.UnixEpoch, e);

    private static ProductView? Project(params EventEnvelope[] envelopes)
    {
        ProductView? view = null;
        foreach (var envelope in envelopes)
        {
            view = ProductView.Apply(view, envelope) ?? view;
        }
        return view;
    }

    [Fact]
    public void Follows_stock_and_reservations()
    {
        var view = Project(
            At(1, new StockAdded(10)),
            At(2, new StockReserved("o1", 4)),
            At(3, new ReservationRejected("o2", 9, 6)),
            At(4, new ReservationReleased("o1", 4)),
            At(5, new StockReserved("o3", 2)));

        Assert.Equal(new ProductView("p1", OnHand: 10, Reserved: 2, Version: 5), view);
        Assert.Equal(8, view!.Available);
    }

    [Fact]
    public void An_event_delivered_twice_is_applied_once()
    {
        var first = ProductView.Apply(null, At(1, new StockAdded(10)));
        Assert.Null(ProductView.Apply(first, At(1, new StockAdded(10))));

        var view = Project(At(1, new StockAdded(10)), At(1, new StockAdded(10)), At(2, new StockAdded(1)));
        Assert.Equal(11, view!.OnHand);
    }

    [Fact]
    public void Matches_the_stock_rebuilt_on_the_write_side()
    {
        InventoryEvent[] history =
        [
            new StockAdded(5), new StockReserved("a", 2), new StockAdded(3), new ReservationReleased("a", 2),
            new StockReserved("b", 6), new ReservationRejected("c", 9, 2),
        ];
        var stock = ProductStock.FromHistory("p1", history);
        var view = Project(history.Select((e, i) => At(i + 1, e)).ToArray());

        Assert.Equal(stock.OnHand, view!.OnHand);
        Assert.Equal(stock.Reserved, view.Reserved);
        Assert.Equal(stock.Available, view.Available);
        Assert.Equal(stock.Version, view.Version);
    }
}
