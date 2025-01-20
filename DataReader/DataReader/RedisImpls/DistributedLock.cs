using StackExchange.Redis;

namespace DataReader.RedisImpls;

public class DistributedLock : IDisposable
{
    private readonly IDatabase _cache;
    private readonly string _lockKey;
    private readonly string _lockValue;
    
    public DistributedLock(string resourceId)
    {
        _cache = RedisConnection.Connection.GetDatabase();
        _lockKey = $"lock:{resourceId}";
        _lockValue = Guid.NewGuid().ToString();
    }

    public async Task<bool> AcquireLockAsync(TimeSpan timeout)
    {
        return await _cache.StringSetAsync(
            _lockKey,
            _lockValue,
            timeout,
            When.NotExists
        );
    }

    public async Task ReleaseLockAsync()
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        await _cache.ScriptEvaluateAsync(
            script,
            [_lockKey],
            [_lockValue]
        );
    }

    public void Dispose()
    {
        ReleaseLockAsync().Wait();
    }
}