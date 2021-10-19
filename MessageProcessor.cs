using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Newtonsoft.Json;
using OCPPCentralSystem.Schemas.DTDL;
using OpcUaWebDashboard.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OpcUaWebDashboard
{
    /// <summary>
    /// This class processes all ingested data into IoTHub.
    /// </summary>
    public class MessageProcessor : IEventProcessor
    {
        private Stopwatch _checkpointStopwatch = new Stopwatch();
        private const double _checkpointPeriodInMinutes = 5;

        public Task OpenAsync(PartitionContext context)
        {
            // get number of messages between checkpoints
            _checkpointStopwatch.Start();

            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            Trace.TraceError($"Message processor error '{error.Message}' on partition with id '{context.PartitionId}'");
            Trace.TraceError($"Exception stack '{error.StackTrace}'");
            return Task.CompletedTask;
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        private void ProcessPublisherMessage(OCPPChargePoint publisherMessage)
        {
            List<Tuple<string, string, string, string, string>> tableEntries = new List<Tuple<string, string, string, string, string>>();
            List<DateTime> xaxis = new List<DateTime>();
            List<float> yaxis = new List<float>();

            // clear previous data
            DashboardController.ClearChart();

            foreach (KeyValuePair<int, Connector> connector in publisherMessage.Connectors)
            {
                // add connector name as label
                DashboardController.AddDatasetToChart("Connector " + connector.Key.ToString());

                // add items to our transaction table
                foreach (KeyValuePair<int, Transaction> transaction in connector.Value.CurrentTransactions.ToArray())
                {
                    if (transaction.Value.StopTime != DateTime.MinValue)
                    {
                        tableEntries.Add(new Tuple<string, string, string, string, string>(
                            transaction.Value.ID.ToString(),
                            transaction.Value.BadgeID,
                            transaction.Value.StartTime.ToString(),
                            transaction.Value.StopTime.ToString(),
                            (transaction.Value.MeterValueFinish - transaction.Value.MeterValueStart).ToString()
                        ));
                    }
                    else
                    {
                        tableEntries.Add(new Tuple<string, string, string, string, string>(
                            transaction.Value.ID.ToString(),
                            transaction.Value.BadgeID,
                            transaction.Value.StartTime.ToString(),
                            "in progress",
                            string.Empty
                        ));
                    }
                }

                // create our transaction table in the dashboard
                DashboardController.CreateTableForTelemetry(tableEntries);

                // update our line chart in the dashboard
                foreach (MeterReading reading in connector.Value.MeterReadings)
                {
                    float[] array = new float[1];
                    array[0] = reading.MeterValue;
                    DashboardController.AddDataToChart(reading.Timestamp.ToString(), array);
                }
            }
        }

        private void Checkpoint(PartitionContext context, Stopwatch checkpointStopwatch)
        {
            context.CheckpointAsync();
            checkpointStopwatch.Restart();
            Trace.TraceInformation($"checkpoint completed at {DateTime.UtcNow}");
        }

        /// <summary>
        /// Process all events from OPC UA servers and update the last value of each node in the topology.
        /// </summary>
        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> ingestedMessages)
        {
            // process each message
            foreach (var eventData in ingestedMessages)
            {
                string message = null;
                try
                {
                    // checkpoint, so that the processor does not need to start from the beginning if it restarts
                    if (_checkpointStopwatch.Elapsed > TimeSpan.FromMinutes(_checkpointPeriodInMinutes))
                    {
                        await Task.Run(() => Checkpoint(context, _checkpointStopwatch)).ConfigureAwait(false);
                    }

                    message = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    if (message != null)
                    {
                        OCPPChargePoint publisherMessage = JsonConvert.DeserializeObject<OCPPChargePoint>(message);
                        if (publisherMessage != null)
                        {
                            ProcessPublisherMessage(publisherMessage);
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Exception '{e.Message}' processing message '{message}'");
                }
            }
        }
    }
}


