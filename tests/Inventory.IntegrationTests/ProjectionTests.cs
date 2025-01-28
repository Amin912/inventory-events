using Inventory.Domain;
using Inventory.Projector;
using Inventory.ReadModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Inventory.IntegrationTests;

// Runs the projector's handling of messages against a real Redis.
public sealed class ProjectionTests : IAsyncLifetime
{
    private readonly RedisContainer redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
    private ConnectionMultiplexer connection = null!;
    private ProductViews views = null!;
    private ProjectionWorker worker = null!;

    public async Task InitializeAsync()
    {
        await redis.StartAsync();
        connection = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString() + ",allowAdmin=true");
        views = new ProductViews(connection);
        worker = new ProjectionWorker(views, new ConfigurationBuilder().Build(), NullLogger<ProjectionWorker>.Instance);
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
        await redis.DisposeAsync();
    }

    private static string Message(string productId, int version, InventoryEvent e) =>
        EventJson.Serialize(new EventEnvelope(productId, version, DateTimeOffset.UtcNow, e));

    [Fact]
    public async Task Messages_build_the_view_and_duplicates_are_ignored()
    {
        await worker.ProjectAsync(Message("p1", 1, new StockAdded(10)));
        await worker.ProjectAsync(Message("p1", 2, new StockReserved("o1", 4)));
        await worker.ProjectAsync(Message("p1", 2, new StockReserved("o1", 4)));  // redelivered

        var view = await views.GetAsync("p1");
        Assert.Equal(new ProductView("p1", OnHand: 10, Reserved: 4, Version: 2), view);
    }

    [Fact]
    public async Task A_malformed_message_is_skipped()
    {
        await worker.ProjectAsync("not json");
        await worker.ProjectAsync(Message("p1", 1, new StockAdded(3)));

        Assert.Equal(3, (await views.GetAsync("p1"))!.OnHand);
    }

    [Fact]
    public async Task Views_can_be_listed_and_cleared()
    {
        await worker.ProjectAsync(Message("b", 1, new StockAdded(1)));
        await worker.ProjectAsync(Message("a", 1, new StockAdded(2)));

        Assert.Equal(["a", "b"], (await views.ListAsync()).Select(v => v.ProductId));
        Assert.Equal(2, await views.ClearAsync());
        Assert.Empty(await views.ListAsync());
    }
}
