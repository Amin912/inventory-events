using Inventory.Projector;
using Inventory.ReadModel;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:Configuration"]!));
builder.Services.AddSingleton<ProductViews>();
builder.Services.AddHostedService<ProjectionWorker>();

builder.Build().Run();
