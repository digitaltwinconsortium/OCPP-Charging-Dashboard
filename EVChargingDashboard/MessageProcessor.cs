using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Newtonsoft.Json;
using OCPPCentralSystem.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EVCharging
{
    public class MessageProcessor : IEventProcessor
    {
        public static ConcurrentDictionary<string, OCPPChargePoint> CentralStation { get; set; }

        public static SemaphoreSlim CentralStationLock = new SemaphoreSlim(1);

        private Stopwatch _checkpointStopwatch = new Stopwatch();

        private const double _checkpointPeriodInMinutes = 5;

        private uint _messageCount = 0;

        public Task OpenAsync(PartitionContext context)
        {
            _checkpointStopwatch.Start();

            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            Console.WriteLine($"Message processor error '{error.Message}' on partition with id '{context.PartitionId}'");
            Console.WriteLine($"Exception stack '{error.StackTrace}'");
            return Task.CompletedTask;
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync().ConfigureAwait(false);
            }
        }

        private void Checkpoint(PartitionContext context, Stopwatch checkpointStopwatch)
        {
            context.CheckpointAsync();
            checkpointStopwatch.Restart();
            Console.WriteLine($"checkpoint completed at {DateTime.UtcNow}");
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> ingestedMessages)
        {
            // checkpoint, so that the processor does not need to start from the beginning if it restarts
            if (_checkpointStopwatch.Elapsed > TimeSpan.FromMinutes(_checkpointPeriodInMinutes))
            {
                await Task.Run(() => Checkpoint(context, _checkpointStopwatch)).ConfigureAwait(false);
            }

            // process each message
            foreach (EventData eventData in ingestedMessages)
            {
                string message = null;
                CentralStationLock.Wait();
                try
                {
                    message = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    if (message != null)
                    {
                        CentralStation = JsonConvert.DeserializeObject<ConcurrentDictionary<string, OCPPChargePoint>>(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception '{ex.Message}' processing message '{message}'");
                }
                finally
                {
                    CentralStationLock.Release();
                }

                _messageCount++;
                if (_messageCount % 10 == 0)
                {
                    Console.WriteLine($"Processed {_messageCount} messages.");
                }
            }
        }
    }
}


