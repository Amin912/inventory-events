using DataReader.models;

namespace DataReader;

public static class InMemDb
{
    public static readonly Dictionary<string, Product> Db = new();

    public static Task<Product?> FetchProductFromDb(string id)
    {
        return Task.Run(() =>
        {
            Db.TryGetValue(id, out var p);
            return p ;
        });
    }
    
    public static void AddPorductToDb(Product product)
    {
        Db[product.Id] = product;
    }
}