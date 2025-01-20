namespace DataWriter;

public class OrderService
{
    private readonly KafkaPublisher _kafkaPublisher = new("orders");
    
    public void SubmitOrder(Order order)
    {
        _kafkaPublisher.Publish(order);
    }
}