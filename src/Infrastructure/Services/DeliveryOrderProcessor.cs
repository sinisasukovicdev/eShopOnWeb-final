using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.Infrastructure.Services;
public class DeliveryOrderProcessor : IDeliveryOrderProcessor
{
    private string _deliveryOrderProcessorURL;
    public DeliveryOrderProcessor(IConfiguration configuration)
    {
        _deliveryOrderProcessorURL = configuration["DeliveryOrderProcessorURL"]!;
    }
    public async Task Process(Order order)
    {
        try
        {
            using HttpClient httpClient = new HttpClient();
            StringContent content = new StringContent(order.ToJson(), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(_deliveryOrderProcessorURL, content);

            await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending POST request: {ex.Message}");
        }
    }
}
