using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Functions.DTOs;
using Functions.Entities;

namespace Functions
{
    public class DeliveryOrderProcessor
    {
        private readonly string _cosmosDBConnectionString =
            "AccountEndpoint=https://eshoponweb-cosmos-db-account.documents.azure.com:443/;AccountKey=FwfIgUdUv6CS7l2V6lnzVhxSdIeDr2gxAP2mldzDc6OwowPfYBgNivLmexOzTaTurzSVUifzPj0WACDbucvddw==;";
        private readonly string _blobStorageConnectionString =
            "DefaultEndpointsProtocol=https;AccountName=stgaccounteshoponweb;AccountKey=V3dOqQJj+WRx/eFN797Qr7V2xzl6gxb0pbb30eSfnb89IIEzmH/4KwkWE3RKPC/yBP94+vibo8hI+AStyCpnZw==;EndpointSuffix=core.windows.net";

        private readonly string _blobContainerName = "orders";
        private readonly string _cosmosDatabaseName = "eshoponweb";
        private readonly string _cosmosContainerName = "orders";

        [FunctionName(nameof(DeliveryOrderProcessor))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var data = await new StreamReader(req.Body).ReadToEndAsync();

            try
            {
                // Deserialize the incoming request data into an Order object
                var order = JsonSerializer.Deserialize<Order>(data);

                var orderEntity = new OrderEntity
                {
                    ShipToAddress = order.ShipToAddress,
                    OrderDate = order.OrderDate,
                    OrderItems = order.OrderItems,
                    FinalPrice = order.Total()
                };

                // Perform tasks with Cosmos DB and Blob Storage
                await WriteToCosmosDBAsync(orderEntity, log);
                await WriteToBlobStorageAsync(data, log);

                return new OkResult();
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Writes order data to Cosmos DB
        /// </summary>
        private async Task WriteToCosmosDBAsync(OrderEntity orderEntity, ILogger log)
        {
            try
            {
                using var cosmosClient = new CosmosClient(_cosmosDBConnectionString);
                var container = cosmosClient.GetContainer(_cosmosDatabaseName, _cosmosContainerName);
                await container.CreateItemAsync(orderEntity);
                log.LogInformation("Order successfully written to Cosmos DB.");
            }
            catch (Exception ex)
            {
                log.LogError($"Error writing to Cosmos DB: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Writes order data to Azure Blob Storage
        /// </summary>
        private async Task WriteToBlobStorageAsync(string plainJsonData, ILogger log)
        {
            try
            {
                // Create Blob Service Client
                BlobServiceClient blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_blobContainerName);

                // Ensure the container exists
                await containerClient.CreateIfNotExistsAsync();

                string blobName = $"{Guid.NewGuid()}.json";

                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Upload JSON data to Blob Storage
                using MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(plainJsonData));
                await blobClient.UploadAsync(memoryStream);
                log.LogInformation($"Order successfully written to Blob Storage with blob name: {blobName}");
            }
            catch (Exception ex)
            {
                log.LogError($"Error writing to Blob Storage: {ex.Message}");
                throw;
            }
        }
    }
}