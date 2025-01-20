namespace DataWriter;

public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public Dictionary<string, int> WarehouseInventory { get; set; }
}