using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using DataWriter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAny",
        b => b
            .SetIsOriginAllowed(hostName => true)  // Allow any origin
            .AllowAnyMethod()  // Allow any method
            .AllowAnyHeader()   
            .AllowCredentials());
});
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
var orderService = ((IApplicationBuilder)app).ApplicationServices.GetRequiredService<OrderService>();
app.MapPost("/order", ([FromBody] Order order) => orderService.SubmitOrder(order));
app.Run();