using DataReader.KafkaImpl;
using DataReader.models;
using DataReader.RedisImpls;

namespace DataReader;

public class InventoryService
{
    private readonly ProductCache _productCache;
    private readonly InventoryEventProducer _eventProducer;
    private readonly ShoppingCartSession _cartSession;

    public InventoryService()
    {
        _productCache = new ProductCache();
        _eventProducer = new InventoryEventProducer();
        _cartSession = new ShoppingCartSession();
    }

    public async Task<bool> ProcessOrderAsync(string userId, string productId, int quantity)
    {
        using (var locks = new DistributedLock(productId))
        {
            if (!await locks.AcquireLockAsync(TimeSpan.FromSeconds(10)))
                throw new Exception("Could not acquire lock");

            var product = await _productCache.GetProductAsync(productId);
            
            // Check if enough inventory exists across warehouses
            var totalInventory = product.WarehouseInventory.Values.Sum();
            if (totalInventory < quantity)
                return false;

            // Determine which warehouse to fulfill from
            var warehouse = SelectWarehouseForFulfillment(product.WarehouseInventory, quantity);
            
            // Create and publish inventory event
            var evt = new InventoryEvent
            {
                EventId = Guid.NewGuid().ToString(),
                ProductId = productId,
                WarehouseId = warehouse,
                QuantityChange = -quantity,
                EventType = "ORDER_FULFILLED",
                Timestamp = DateTime.UtcNow
            };

            await _eventProducer.PublishInventoryEventAsync(evt);
            
            // Update shopping cart
            await _cartSession.AddToCartAsync(userId, productId, quantity);

            return true;
        }
    }

    private string SelectWarehouseForFulfillment(
        Dictionary<string, int> warehouseInventory,
        int quantity
    )
    {
        // Simple implementation - select first warehouse with enough inventory
        foreach (var kvp in warehouseInventory)
        {
            if (kvp.Value >= quantity)
                return kvp.Key;
        }
        
        throw new Exception("No single warehouse has enough inventory");
    }
}