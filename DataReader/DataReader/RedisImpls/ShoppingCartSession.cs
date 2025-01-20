using DataReader.RedisImpls;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DataReader;

public class ShoppingCartSession
{
    private readonly IDatabase _cache;
    private const int SESSION_DURATION = 3600; // 1 hour

    public ShoppingCartSession()
    {
        _cache = RedisConnection.Connection.GetDatabase();
    }

    public async Task<Dictionary<string, int>> GetCartAsync(string userId)
    {
        var cached = await _cache.StringGetAsync($"cart:{userId}");
        if (cached.IsNull)
            return new Dictionary<string, int>();

        return JsonConvert.DeserializeObject<Dictionary<string, int>>(cached);
    }

    public async Task AddToCartAsync(string userId, string productId, int quantity)
    {
        var cart = await GetCartAsync(userId);
        
        if (cart.ContainsKey(productId))
            cart[productId] += quantity;
        else
            cart[productId] = quantity;

        await _cache.StringSetAsync(
            $"cart:{userId}",
            JsonConvert.SerializeObject(cart),
            TimeSpan.FromSeconds(SESSION_DURATION)
        );
    }
}
