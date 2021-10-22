using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Newtonsoft.Json;
using OCPPCentralSystem.Schemas.DTDL;
using OpcUaWebDashboard.Controllers;
using System;
using System.Collections.Concurrent;
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
            Console.WriteLine($"Message processor error '{error.Message}' on partition with id '{context.PartitionId}'");
            Console.WriteLine($"Exception stack '{error.StackTrace}'");
            return Task.CompletedTask;
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        private void ProcessPublisherMessage(ConcurrentDictionary<string, OCPPChargePoint> publisherMessage)
        {
            // clear previous data
            DashboardController.ClearCharts();

            StringBuilder sb = new StringBuilder();
            sb.Append("<table cellpadding='3' cellspacing='3'>");

            sb.Append("<tr>");
            foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in publisherMessage)
            {
                sb.Append("<th>");
                sb.Append("<b>" + chargePoint.Value.ID + "</b>");
                sb.Append("</th>");
            }
            sb.Append("</tr>");

            sb.Append("<tr>");
            foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in publisherMessage)
            {
                sb.Append("<td valign='top'>");
                sb.Append("<hr/>");
                sb.Append("<div id=\"" + chargePoint.Value.ID + "statustable\" width=\"400\"/>");
                sb.Append("</td>");
            }
            sb.Append("</tr>");

            sb.Append("<tr>");
            foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in publisherMessage)
            {
                sb.Append("<td valign='top'>");
                sb.Append("<table cellpadding='3' cellspacing='3'>");
                foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                {
                    string name = chargePoint.Value.ID + "_" + connector.Value.ID.ToString();

                    sb.Append("<tr>");
                    sb.Append("<td valign='top'>");
                    sb.Append("<hr/>");
                    sb.Append("<div id=\"" + name + "transactiontable\" width =\"400\"></div>");
                    sb.Append("</td>");
                    sb.Append("</tr>");

                    sb.Append("<tr>");
                    sb.Append("<td valign='top'>");
                    sb.Append("<hr/>");
                    sb.Append("<canvas id=\"" + name + "\" width=\"400\" height=\"200\"/>");
                    sb.Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                sb.Append("</td>");
            }
            sb.Append("</tr>");

            sb.Append("</table>");

            DashboardController.AddChargers(sb.ToString());

            foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in publisherMessage)
            {
                // build status table of connectors
                List<string> status = new List<string>();

                foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                {
                    status.Add(connector.Value.Status);
                    string chartName = chargePoint.Value.ID + "_" + connector.Value.ID.ToString();

                    // add chart
                    DashboardController.AddChart(chartName);

                    // update our line chart in the dashboard
                    List<string> labels = new List<string>();
                    List<float> readings = new List<float>();
                    foreach (MeterReading reading in connector.Value.MeterReadings)
                    {
                        labels.Add(reading.Timestamp.ToString());
                        readings.Add(reading.MeterValue);
                    }
                    DashboardController.AddDataToChart(chartName, labels.ToArray(), readings.ToArray());

                    // add items to our transaction table
                    List<Tuple<string, string, string, string, string>> tableEntries = new List<Tuple<string, string, string, string, string>>();

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

                    DashboardController.CreateTableForTransactions(chargePoint.Value.ID + "_" + connector.Value.ID.ToString(), tableEntries);
                }

                DashboardController.CreateTableForStatus(chargePoint.Value.ID, status);
            }
        }

        private void Checkpoint(PartitionContext context, Stopwatch checkpointStopwatch)
        {
            context.CheckpointAsync();
            checkpointStopwatch.Restart();
            Console.WriteLine($"checkpoint completed at {DateTime.UtcNow}");
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
                        ConcurrentDictionary<string, OCPPChargePoint> publisherMessage = JsonConvert.DeserializeObject<ConcurrentDictionary<string, OCPPChargePoint>>(message);
                        if (publisherMessage != null)
                        {
                            ProcessPublisherMessage(publisherMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception '{ex.Message}' processing message '{message}'");
                }
            }
        }
    }
}


