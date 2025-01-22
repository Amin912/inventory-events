using Inventory.Api;
using Inventory.Domain;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Postgres")!));
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<InventoryCommands>();

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

// The raw event stream of a product: everything that ever happened to it.
app.MapGet("/products/{productId}/events", async (string productId, EventStore store) =>
    Results.Ok(await store.LoadAsync(productId)));

await app.Services.GetRequiredService<EventStore>().CreateSchemaAsync();
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
