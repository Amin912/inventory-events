namespace DataReader.models;

public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public Dictionary<string, int> WarehouseInventory { get; set; }

    public Product(string id, string name, decimal price, Dictionary<string, int> warehouseInventory)
    {
        Id = id;
        Name = name;
        Price = price;
        WarehouseInventory = warehouseInventory;
    }

    public override string ToString()
    {
        return $"{Id}, {Name}, {Price}, {WarehouseInventory.Sum((s) => s.Value)}";
    }
}