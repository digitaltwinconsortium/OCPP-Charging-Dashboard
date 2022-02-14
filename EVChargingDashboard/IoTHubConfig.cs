
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EVCharging
{
    public class IoTHubConfig
    {
        public static Task ConfigureIotHub()
        {
            return Task.Run(() => ConnectToIotHubAsync(CancellationToken.None));
        }

        private static async Task ConnectToIotHubAsync(CancellationToken ct)
        {
            EventProcessorHost eventProcessorHost;

            // Get configuration settings
            string iotHubTelemetryConsumerGroup = "dashboard";
            string iotHubEventHubName = Environment.GetEnvironmentVariable("IotHubEventHubName");
            string iotHubEventHubEndpointIotHubOwnerConnectionString = Environment.GetEnvironmentVariable("EventHubEndpointIotHubOwnerConnectionString");
            string solutionStorageAccountConnectionString = Environment.GetEnvironmentVariable("StorageAccountConnectionString");

            // Initialize EventProcessorHost.
            Console.WriteLine("Creating Event Processor Host for IoT Hub: {0}, ConsumerGroup: {1}", iotHubEventHubName, iotHubTelemetryConsumerGroup);

            string StorageContainerName = "telemetrycheckpoints";
            eventProcessorHost = new EventProcessorHost(
                    iotHubEventHubName,
                    iotHubTelemetryConsumerGroup,
                    iotHubEventHubEndpointIotHubOwnerConnectionString,
                    solutionStorageAccountConnectionString,
                    StorageContainerName);

            // Registers the Event Processor Host and starts receiving messages.
            EventProcessorOptions options = new EventProcessorOptions();
            options.InitialOffsetProvider = (partitionId) => EventPosition.FromEnqueuedTime(DateTime.UtcNow);
            options.SetExceptionHandler(EventProcessorHostExceptionHandler);
            try
            {
                await eventProcessorHost.RegisterEventProcessorAsync<MessageProcessor>(options);
                Console.WriteLine($"EventProcessor successfully registered");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception during register EventProcessorHost '{e.Message}'");
            }

            // Wait till shutdown.
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine($"Application is shutting down. Unregistering EventProcessorHost...");
                    await eventProcessorHost.UnregisterEventProcessorAsync();
                    return;
                }
                await Task.Delay(1000);
            }
        }

        private static void EventProcessorHostExceptionHandler(ExceptionReceivedEventArgs args)
        {
            Console.WriteLine($"EventProcessorHostException: {args.Exception.Message}");
        }
    }
}