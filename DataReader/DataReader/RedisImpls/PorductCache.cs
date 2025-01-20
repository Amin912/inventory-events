using DataReader.models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DataReader.RedisImpls;

public class ProductCache
{
    private readonly IDatabase _cache;
    private const int CACHE_DURATION = 3600; // 1 hour

    public ProductCache()
    {
        _cache = RedisConnection.Connection.GetDatabase();
    }

    public async Task<Product> GetProductAsync(string productId)
    {
        var cached = await _cache.StringGetAsync($"product:{productId}");
        if (!cached.IsNull)
        {
            return JsonConvert.DeserializeObject<Product>(cached);
        }
        
        // Simulate database fetch
        var product = await FetchProductFromDb(productId);
        await CacheProductAsync(product);
        return product;
    }

    private async Task CacheProductAsync(Product product)
    {
        var json = JsonConvert.SerializeObject(product);
        await _cache.StringSetAsync(
            $"product:{product.Id}",
            json,
            TimeSpan.FromSeconds(CACHE_DURATION)
        );
    }

    public async Task InvalidateProductCacheAsync(string productId)
    {
        await _cache.KeyDeleteAsync($"product:{productId}");
    }

    private Task<Product> FetchProductFromDb(string productId)
    {
        return InMemDb.FetchProductFromDb(productId);
    }
}