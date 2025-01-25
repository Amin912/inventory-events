using Inventory.Projector;
using Inventory.ReadModel;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:Configuration"]!));
builder.Services.AddSingleton<ProductViews>();
builder.Services.AddHostedService<ProjectionWorker>();

var host = builder.Build();

// `--rebuild` replays the whole log into a fresh read model, then exits.
if (args.Contains("--rebuild"))
{
    await Rebuild.RunAsync(host.Services, builder.Configuration);
    return;
}

host.Run();
