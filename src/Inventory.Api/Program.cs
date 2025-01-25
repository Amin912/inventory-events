using Inventory.Api;
using Inventory.Domain;
using Inventory.ReadModel;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!));
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<InventoryCommands>();
builder.Services.AddSingleton(KafkaSetup.CreateProducer(builder.Configuration));
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:Configuration"]!));
builder.Services.AddSingleton<ProductViews>();

var app = builder.Build();

app.MapPost("/products/{productId}/stock", async (string productId, AddStockRequest request, InventoryCommands commands) =>
    await Handle(async () =>
    {
        await commands.HandleAsync(productId, stock => stock.AddStock(request.Quantity));
        return Results.Ok(new { productId, added = request.Quantity });
    }));

app.MapPost("/orders", async (PlaceOrderRequest request, InventoryCommands commands) =>
    await Handle(async () =>
    {
        var events = await commands.HandleAsync(request.ProductId, stock => stock.Reserve(request.OrderId, request.Quantity));
        return events.FirstOrDefault() switch
        {
            ReservationRejected rejected => Results.Conflict(new { request.OrderId, status = "rejected", rejected.Available }),
            _ => Results.Ok(new { request.OrderId, status = "reserved" }),
        };
    }));

app.MapDelete("/products/{productId}/reservations/{orderId}", async (string productId, string orderId, InventoryCommands commands) =>
{
    var events = await commands.HandleAsync(productId, stock => stock.Release(orderId));
    return events.Count > 0 ? Results.NoContent() : Results.NotFound();
});

// Reads come from the Redis read model, which the projector updates from
// Kafka. It lags the writes by a moment.
app.MapGet("/products", async (ProductViews views) => Results.Ok(await views.ListAsync()));

app.MapGet("/products/{productId}", async (string productId, ProductViews views) =>
    await views.GetAsync(productId) is { } view ? Results.Ok(view) : Results.NotFound());

// The raw event stream of a product: everything that ever happened to it.
app.MapGet("/products/{productId}/events", async (string productId, EventStore store) =>
    Results.Ok(await store.LoadAsync(productId)));

await app.Services.GetRequiredService<EventStore>().CreateSchemaAsync();
await KafkaSetup.EnsureTopicAsync(app.Configuration);
app.Run();

static async Task<IResult> Handle(Func<Task<IResult>> action)
{
    try
    {
        return await action();
    }
    catch (InvalidCommandException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
}

public sealed record AddStockRequest(int Quantity);

public sealed record PlaceOrderRequest(string OrderId, string ProductId, int Quantity);

public partial class Program;
