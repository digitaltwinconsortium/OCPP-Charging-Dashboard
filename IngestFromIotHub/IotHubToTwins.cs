namespace IngestFromIotHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Core.Pipeline;
    using Azure.DigitalTwins.Core;
    using Azure.Identity;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class IotHubToTwins
    {
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("IoTHubtoTwins")]
        public async Task Run([EventHubTrigger("evcharging", Connection = "EVENTHUB_CONNECTIONSTRING", ConsumerGroup = "adt")] EventData[] events, ILogger log)
        {
            if (adtInstanceUrl == null)
            {
                log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
            }

            var exceptions = new List<Exception>();

            try
            {
                // Authenticate with Digital Twins
                var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net");
                var client = new DigitalTwinsClient(
                    new Uri(adtInstanceUrl),
                    cred,
                    new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"ADT service client connection created.");

                foreach (EventData eventData in events)
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    // Find fields
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(messageBody);

                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    var charging = deviceMessage["body"]["Charging"];
                    
                    // </Find_device_ID_and_charging>
                    log.LogInformation($"Device:{deviceId} Charging is:{charging}");

                    
                    // <Update_twin_with_device_charging>
                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendReplace("/Charging", charging.Value<double>());
                    
                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                }
            }
            catch (Exception e)
            {
                // We need to keep processing the rest of the batch - capture this exception and continue.
                // Also, consider capturing details of the message that failed processing so it can be processed again later.
                exceptions.Add(e);
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
