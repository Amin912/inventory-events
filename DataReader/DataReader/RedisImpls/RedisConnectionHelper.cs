using StackExchange.Redis;

namespace DataReader.RedisImpls;

public class RedisConnection
{
    private static Lazy<ConnectionMultiplexer>? _lazyConnection;
    
    public static ConnectionMultiplexer Connection
    {
        get
        {
            _lazyConnection ??= new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect("localhost:6379"));
            return _lazyConnection.Value;
        }
    }
}
