namespace DataReader.models;

public class InventoryEvent
{
    public string EventId { get; set; }
    public string ProductId { get; set; }
    public string WarehouseId { get; set; }
    public int QuantityChange { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }

}