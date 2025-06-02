using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.Infrastructure.Services;

internal class OrderItemsReserver : IOrderItemsReserver
{
    private string _serviceBusConnectionString;
    public OrderItemsReserver(IConfiguration configuration)
    {
        _serviceBusConnectionString = configuration["ServiceBusConnectionString"]!;
    }
    public async Task Reserve(Order order)
    {
        var client = new ServiceBusClient(_serviceBusConnectionString);

        var sender = client.CreateSender("orders");

        var orderItems = order.OrderItems.Select(x => new { Id = x.Id, Quantity = x.Units });
        ServiceBusMessage message = new ServiceBusMessage(JsonConvert.SerializeObject(orderItems));

        await sender.SendMessageAsync(message);

        Console.WriteLine("Message sent!");

        await sender.CloseAsync();
        await client.DisposeAsync();
    }
}
