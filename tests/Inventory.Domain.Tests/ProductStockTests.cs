using Inventory.Domain;

namespace Inventory.Domain.Tests;

public class ProductStockTests
{
    private static ProductStock StockWith(params InventoryEvent[] history) => ProductStock.FromHistory("p1", history);

    [Fact]
    public void Adding_stock_increases_what_is_available()
    {
        var stock = StockWith(new StockAdded(10), new StockAdded(5));
        Assert.Equal(15, stock.OnHand);
        Assert.Equal(15, stock.Available);
        Assert.Equal(2, stock.Version);
    }

    [Fact]
    public void An_order_within_stock_is_reserved()
    {
        var stock = StockWith(new StockAdded(10));
        var events = stock.Reserve("o1", 4);
        Assert.Equal([new StockReserved("o1", 4)], events);
    }

    [Fact]
    public void An_order_beyond_stock_is_rejected_and_says_how_much_was_left()
    {
        var stock = StockWith(new StockAdded(10), new StockReserved("o1", 8));
        var events = stock.Reserve("o2", 5);
        Assert.Equal([new ReservationRejected("o2", 5, 2)], events);
    }

    [Fact]
    public void Reserving_the_same_order_twice_does_nothing_the_second_time()
    {
        var stock = StockWith(new StockAdded(10), new StockReserved("o1", 3));
        Assert.Empty(stock.Reserve("o1", 3));
    }

    [Fact]
    public void Releasing_an_order_returns_its_stock()
    {
        var stock = StockWith(new StockAdded(10), new StockReserved("o1", 3));
        var events = stock.Release("o1");
        Assert.Equal([new ReservationReleased("o1", 3)], events);

        stock.Apply(events[0]);
        Assert.Equal(10, stock.Available);
        Assert.Equal(0, stock.Reserved);
    }

    [Fact]
    public void Releasing_an_unknown_order_does_nothing()
    {
        Assert.Empty(StockWith(new StockAdded(10)).Release("nope"));
    }

    [Fact]
    public void A_rejection_changes_nothing_but_the_version()
    {
        var stock = StockWith(new StockAdded(1), new ReservationRejected("o1", 5, 1));
        Assert.Equal(1, stock.Available);
        Assert.Equal(2, stock.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Quantities_must_be_positive(int quantity)
    {
        var stock = StockWith(new StockAdded(10));
        Assert.Throws<InvalidCommandException>(() => stock.AddStock(quantity));
        Assert.Throws<InvalidCommandException>(() => stock.Reserve("o1", quantity));
    }

    [Fact]
    public void Events_survive_a_json_round_trip_with_their_type()
    {
        InventoryEvent[] events =
        [
            new StockAdded(3), new StockReserved("o1", 2), new ReservationRejected("o2", 9, 1), new ReservationReleased("o1", 2),
        ];
        foreach (var e in events)
        {
            Assert.Equal(e, EventJson.DeserializeEvent(EventJson.Serialize(e)));
        }

        var envelope = new EventEnvelope("p1", 7, DateTimeOffset.UnixEpoch, new StockAdded(3));
        var copy = EventJson.DeserializeEnvelope(EventJson.Serialize(envelope));
        Assert.Equal(envelope, copy);
    }
}
