using Inventory.Api;
using Inventory.Domain;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Inventory.IntegrationTests;

// Runs against a real Postgres in a throwaway container.
public sealed class EventStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private NpgsqlDataSource db = null!;
    private EventStore store = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        db = NpgsqlDataSource.Create(postgres.GetConnectionString());
        store = new EventStore(db);
        await store.CreateSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Appended_events_load_back_in_order()
    {
        await store.AppendAsync("p1", 0, [new StockAdded(5), new StockReserved("o1", 2)]);
        await store.AppendAsync("p1", 2, [new ReservationReleased("o1", 2)]);

        var events = await store.LoadAsync("p1");

        Assert.Equal([1, 2, 3], events.Select(e => e.Version));
        Assert.Equal(new ReservationReleased("o1", 2), events[2].Event);
        Assert.Empty(await store.LoadAsync("other"));
    }

    [Fact]
    public async Task A_stale_append_is_rejected_and_leaves_nothing_behind()
    {
        await store.AppendAsync("p1", 0, [new StockAdded(5)]);

        // Someone else already wrote version 1.
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.AppendAsync("p1", 0, [new StockAdded(1), new StockAdded(2)]));

        Assert.Single(await store.LoadAsync("p1"));
        Assert.Equal(1L, await Scalar("SELECT count(*) FROM outbox"));
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell()
    {
        var commands = new InventoryCommands(store);
        await commands.HandleAsync("p1", stock => stock.AddStock(5));

        // Ten orders of one at the same time, for five in stock.
        var results = await Task.WhenAll(Enumerable.Range(1, 10).Select(i =>
            commands.HandleAsync("p1", stock => stock.Reserve($"o{i}", 1))));

        Assert.Equal(5, results.Count(r => r.Single() is StockReserved));
        Assert.Equal(5, results.Count(r => r.Single() is ReservationRejected));

        var stock = ProductStock.FromHistory("p1", (await store.LoadAsync("p1")).Select(e => e.Event));
        Assert.Equal(0, stock.Available);
    }

    private async Task<long> Scalar(string sql)
    {
        await using var command = db.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
