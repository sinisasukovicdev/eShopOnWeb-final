using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace Functions
{
    public class OrderItemsReserverAsync
    {
        private readonly string _storageAccountConnectionString =
            "DefaultEndpointsProtocol=https;AccountName=stgaccounteshoponweb;AccountKey=V3dOqQJj+WRx/eFN797Qr7V2xzl6gxb0pbb30eSfnb89IIEzmH/4KwkWE3RKPC/yBP94+vibo8hI+AStyCpnZw==;EndpointSuffix=core.windows.net";

        private const string _containerName = "orders";

        private const string _logicAppUrl = "https://prod-55.northeurope.logic.azure.com:443/" +
            "workflows/9b1814bbdafd46f9a243ef3380d5d749/triggers/When_a_HTTP_request_is_received/paths/invoke" +
            "?api-version=2016-10-01&sp=%2Ftriggers%2FWhen_a_HTTP_request_is_received%2Frun" +
            "&sv=1.0&sig=GpNLftMHIIPKk6-bCs9NlyD0lWQ6pY1NozzqiLxV7CE";

        private const int _maxDeliveryAttempts = 3;

        [FunctionName(nameof(OrderItemsReserverAsync))]
        public async Task Run(
            [ServiceBusTrigger("orders", Connection = "OrdersQueueConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            ILogger log)
        {
            string formattedJsonData = string.Empty;

            try
            {
                log.LogInformation($"C# ServiceBus queue trigger function processed message: {message.Body}");

                formattedJsonData = FormatJsonMessage(message.Body);

                await UploadToBlobStorageAsync(formattedJsonData, log);

                log.LogInformation("Message successfully uploaded to Blob Storage.");
            }
            catch (Exception ex) when (message.DeliveryCount >= _maxDeliveryAttempts)
            {
                log.LogError($"Failed processing message after {_maxDeliveryAttempts} attempts. Dead-lettering message. Error: {ex.Message}");

                await HandleDeadLetterAsync(message, formattedJsonData, messageActions, ex, log);
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred during message processing: {ex.Message}");
                throw;
            }
        }

        private string FormatJsonMessage(BinaryData messageBody)
        {
            var jsonElementData = JsonSerializer.Deserialize<JsonElement>(messageBody);
            return JsonSerializer.Serialize(jsonElementData, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task UploadToBlobStorageAsync(string jsonData, ILogger log)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(_storageAccountConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

                await containerClient.CreateIfNotExistsAsync();

                string blobName = $"{Guid.NewGuid()}.json";
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                using MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
                await blobClient.UploadAsync(memoryStream);

                log.LogInformation($"Blob uploaded successfully with name: {blobName}");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error uploading to Blob Storage: {ex.Message}", ex);
            }
        }

        private async Task HandleDeadLetterAsync(
            ServiceBusReceivedMessage message,
            string jsonData,
            ServiceBusMessageActions messageActions,
            Exception exception,
            ILogger log)
        {
            try
            {
                using HttpClient client = new HttpClient();
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(_logicAppUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation("Logic App successfully called to handle failed message.");
                }
                else
                {
                    log.LogWarning($"Logic App returned non-success status code: {response.StatusCode}");
                }

                await messageActions.DeadLetterMessageAsync(message, exception.Message);
                log.LogInformation("Message has been dead-lettered successfully.");
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to handle dead-letter process: {ex.Message}");
                throw;
            }
        }
    }
}