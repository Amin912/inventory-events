using System.Timers;
using DataReader;
using DataReader.KafkaImpl;
using Timer = System.Timers.Timer;
using DataReader.models;
using DataReader.RedisImpls;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    private static InventoryEventConsumer inventoryEventConsumer = new();
    private static async void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        Console.WriteLine("listening...");
        await inventoryEventConsumer.StartConsumingAsync(CancellationToken.None);
        inventoryEventConsumer.Dispose();
        Console.WriteLine("Updated database from event store");
    }

    public static async Task Main(string[] args)
    {
        var task = Task.Run(()=>inventoryEventConsumer.StartConsumingAsync(CancellationToken.None));
        var i = "";
        while (i != "<")
        {
            i = Console.ReadLine();
            foreach (var keyValuePair in InMemDb.Db)
            {
                Console.WriteLine(keyValuePair.ToString());
            }
        }

        await task;

        // var productCache = new ProductCache();
        // var inventoryService = new InventoryService();
        //
        // // Start Kafka consumer
        // using (var consumer = new InventoryEventConsumer(productCache))
        // {
        //     var cts = new CancellationTokenSource();
        //     var consumerTask = consumer.StartConsumingAsync(cts.Token);
        //
        //     // Start your web API or other entry points here
        //     var builder = WebApplication.CreateBuilder(args);
        //     // Add services to the container
        //     builder.Services.AddSingleton<InventoryService>(inventoryService);
        //     
        //     var app = builder.Build();
        //     await app.RunAsync();
        // }



    }
}