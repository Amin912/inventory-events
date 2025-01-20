using Confluent.Kafka;
using DataReader.models;
using DataReader.RedisImpls;
using Newtonsoft.Json;

namespace DataReader.KafkaImpl;

public class InventoryEventConsumer : IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    // private readonly ProductCache _productCache;
    private const string TOPIC = "orders";
    private bool _consuming = true;

    public InventoryEventConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "inventory-processor",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        // _productCache = productCache;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(TOPIC);

        while (_consuming && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                var evt = JsonConvert.DeserializeObject<InventoryEvent>(
                    consumeResult.Message.Value
                );
                Console.WriteLine(consumeResult.Message.Value);
                await ProcessInventoryEventAsync(evt);
                _consumer.Commit(consumeResult);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log error and continue
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }

    private async Task ProcessInventoryEventAsync(InventoryEvent evt)
    {
        // Update database
        await UpdateInventoryInDatabase(evt);
        
        // Invalidate cache
        // await _productCache.InvalidateProductCacheAsync(evt.ProductId);
        
        // Additional processing like analytics, alerts, etc.
        // await ProcessInventoryAlerts(evt);
    }

    public void Dispose()
    {
        _consuming = false;
        _consumer?.Close();
        _consumer?.Dispose();
    }

    private async Task UpdateInventoryInDatabase(InventoryEvent evt)
    {
        var product = await InMemDb.FetchProductFromDb(evt.ProductId) ?? new Product(evt.ProductId, "", 0, new Dictionary<string, int>());
        product.WarehouseInventory[evt.WarehouseId] = !product.WarehouseInventory.ContainsKey(evt.WarehouseId)? -evt.QuantityChange : product.WarehouseInventory[evt.WarehouseId] - evt.QuantityChange;
        InMemDb.AddPorductToDb(product);
    }
}
