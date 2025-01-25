using System.Text.Json;
using Inventory.Domain;
using StackExchange.Redis;

namespace Inventory.ReadModel;

// The read model in Redis: one JSON value per product under "product:{id}".
// Written by the projector, read by the API.
public sealed class ProductViews(IConnectionMultiplexer redis)
{
    private const string Prefix = "product:";
    private IDatabase Db => redis.GetDatabase();

    public async Task<ProductView?> GetAsync(string productId)
    {
        var value = await Db.StringGetAsync(Prefix + productId);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ProductView>(value.ToString(), EventJson.Options);
    }

    public Task SaveAsync(ProductView view) =>
        Db.StringSetAsync(Prefix + view.ProductId, JsonSerializer.Serialize(view, EventJson.Options));

    public async Task<IReadOnlyList<ProductView>> ListAsync()
    {
        var views = new List<ProductView>();
        foreach (var key in Keys())
        {
            var value = await Db.StringGetAsync(key);
            if (!value.IsNullOrEmpty)
            {
                views.Add(JsonSerializer.Deserialize<ProductView>(value.ToString(), EventJson.Options)!);
            }
        }
        return views.OrderBy(v => v.ProductId).ToList();
    }

    public async Task<int> ClearAsync()
    {
        var keys = Keys().ToArray();
        return keys.Length == 0 ? 0 : (int)await Db.KeyDeleteAsync(keys);
    }

    // SCAN through every server, so it works the same against a cluster.
    private IEnumerable<RedisKey> Keys() =>
        redis.GetServers().SelectMany(server => server.Keys(pattern: Prefix + "*"));
}
