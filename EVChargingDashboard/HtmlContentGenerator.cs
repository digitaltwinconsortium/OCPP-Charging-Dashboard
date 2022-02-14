using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using OCPPCentralSystem.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EVCharging
{
    public class HtmlContentGenerator : IHtmlContentGenerator
    {
        public List<string> NotificationList { get; set; }

        public Dictionary<string, string> BadgeClaims { get; set; }

        public Timer SignalRTimer { get; set; }

        private IEmailSender _emailSender;

        private StatusHub _hub;

        public HtmlContentGenerator(IHubContext<StatusHub> context, IEmailSender emailSender)
        {
            _hub = new StatusHub(context);
            _emailSender = emailSender;

            BadgeClaims = new Dictionary<string, string>();
            NotificationList = new List<string>();

            SignalRTimer = new Timer(GenerateDashboard, null, 2000, 2000);

            Console.WriteLine("Content Generator started.");
        }

        private void GenerateDashboard(object state)
        {
            MessageProcessor.CentralStationLock.Wait();
            try
            {
                if (MessageProcessor.CentralStation != null)
                {
                    Console.WriteLine("Generating content...");

                    // slow down timer
                    SignalRTimer.Change(15000, 15000);

                    StringBuilder sb = new StringBuilder();
                    sb.Append("<table cellpadding='3' cellspacing='3'>");

                    sb.Append("<tr>");
                    foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in MessageProcessor.CentralStation)
                    {
                        sb.Append("<th>");
                        sb.Append("<b>" + chargePoint.Value.ID + "</b>");
                        sb.Append("</th>");
                    }
                    sb.Append("</tr>");

                    sb.Append("<tr>");
                    foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in MessageProcessor.CentralStation)
                    {
                        sb.Append("<td width='500px' valign='top'>");
                        sb.Append("<hr/>");
                        sb.Append("<div id=\"" + chargePoint.Value.ID + "_statustable\"/>");
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");

                    sb.Append("<tr>");
                    foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in MessageProcessor.CentralStation)
                    {
                        sb.Append("<td width='500px' valign='top'>");
                        sb.Append("<table cellpadding='3' cellspacing='3'>");
                        foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                        {
                            string name = chargePoint.Value.ID + "_" + connector.Value.ID.ToString();

                            sb.Append("<tr>");
                            sb.Append("<td width='500px' valign='top'>");
                            sb.Append("<hr/>");
                            sb.Append("<div id=\"" + name + "_transactiontable\"></div>");
                            sb.Append("</td>");
                            sb.Append("</tr>");

                            sb.Append("<tr>");
                            sb.Append("<td width='500px' valign='top'>");
                            sb.Append("<hr/>");
                            sb.Append("<canvas id=\"" + name + "_chart\" height=\"200\"/>");
                            sb.Append("</td>");
                            sb.Append("</tr>");
                        }
                        sb.Append("</table>");
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");

                    sb.Append("</table>");

                    _hub.AddChargers(sb.ToString());

                    // wait a few milliseconds for the table scaffolding to be in place before populating it
                    Thread.Sleep(100);

                    bool chargerAvailable = false;

                    foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in MessageProcessor.CentralStation)
                    {
                        // build status table of connectors
                        List<string> status = new List<string>();

                        foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                        {
                            status.Add(connector.Value.Status);

                            if (connector.Value.Status == "Available")
                            {
                                chargerAvailable = true;
                            }

                            // add meter readings chart
                            List<string> labels = new List<string>();
                            List<float> readings = new List<float>();
                            foreach (MeterReading reading in connector.Value.MeterReadings)
                            {
                                labels.Add(reading.Timestamp.ToString());
                                readings.Add(reading.MeterValue);
                            }
                            _hub.AddChart(chargePoint.Value.ID + "_" + connector.Value.ID.ToString() + "_chart", labels.ToArray(), readings.ToArray());

                            // add items to our transaction table
                            List<Tuple<string, string, string, string, string>> tableEntries = new List<Tuple<string, string, string, string, string>>();

                            foreach (KeyValuePair<int, Transaction> transaction in connector.Value.CurrentTransactions)
                            {
                                if (transaction.Value.StopTime != DateTime.MinValue)
                                {
                                    // notify users that have claimed this transaction
                                    if (BadgeClaims.ContainsKey(transaction.Value.BadgeID))
                                    {
                                        _emailSender.SendEmailAsync(
                                            BadgeClaims[transaction.Value.BadgeID],
                                            "Microsoft EV Charging", "Your vehicle charging from " + transaction.Value.StartTime.ToString() + " is now complete. Please move your vehicle.")
                                            .GetAwaiter().GetResult();
                                        Console.WriteLine("Sent charging complete notification email to " + BadgeClaims[transaction.Value.BadgeID] + ".");
                                        BadgeClaims.Remove(transaction.Value.BadgeID);
                                    }

                                    if (connector.Value.MeterReadings.Count > 0)
                                    {
                                        tableEntries.Add(new Tuple<string, string, string, string, string>(
                                            transaction.Value.ID.ToString(),
                                            transaction.Value.BadgeID,
                                            transaction.Value.StartTime.ToString(),
                                            transaction.Value.StopTime.ToString(),
                                            (transaction.Value.MeterValueFinish - transaction.Value.MeterValueStart).ToString() + " " + connector.Value.MeterReadings[0].MeterValueUnit
                                        ));
                                    }
                                    else
                                    {
                                        tableEntries.Add(new Tuple<string, string, string, string, string>(
                                            transaction.Value.ID.ToString(),
                                            transaction.Value.BadgeID,
                                            transaction.Value.StartTime.ToString(),
                                            transaction.Value.StopTime.ToString(),
                                            (transaction.Value.MeterValueFinish - transaction.Value.MeterValueStart).ToString()
                                        ));
                                    }
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

                            CreateTableForTransactions(chargePoint.Value.ID + "_" + connector.Value.ID.ToString() + "_transactiontable", tableEntries);
                        }

                        CreateTableForStatus(chargePoint.Value.ID + "_statustable", status);
                    }

                    if (chargerAvailable)
                    {
                        int numPeopleWaiting = NotificationList.Count;
                        while (NotificationList.Count > 0)
                        {
                            _emailSender.SendEmailAsync(
                                NotificationList[0],
                                "Microsoft EV Charging", "A charger is now available and there are " + (numPeopleWaiting - 1).ToString() + " other people waiting and got notified.")
                                .GetAwaiter().GetResult();
                            Console.WriteLine("Sent charger available notification email to " + NotificationList[0] + ".");
                            NotificationList.RemoveAt(0);
                        }
                    }

                    _hub.AvailableStatus(chargerAvailable);

                    Console.WriteLine("Charger available: " + chargerAvailable.ToString() + " Notification list length: " + NotificationList.Count.ToString() + " BadgeClaims list length: " + BadgeClaims.Count.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                MessageProcessor.CentralStationLock.Release();
            }
        }

        private void CreateTableForTransactions(string key, List<Tuple<string, string, string, string, string>> telemetry)
        {
            // create HTML table
            StringBuilder sb = new StringBuilder();
            sb.Append("<table cellpadding='3' cellspacing='3'>");

            // header
            sb.Append("<tr>");
            sb.Append("<th><b>Transaction</b></th>");
            sb.Append("<th><b>Badge</b></th>");
            sb.Append("<th><b>Start Time</b></th>");
            sb.Append("<th><b>Stop Time</b></th>");
            sb.Append("<th><b>Used Power</b></th>");
            sb.Append("</tr>");

            // rows
            foreach (Tuple<string, string, string, string, string> item in telemetry)
            {
                sb.Append("<tr>");
                sb.Append("<td style='width:200px'>" + item.Item1 + "</td>");
                sb.Append("<td style='width:200px'>" + item.Item2 + "<a href='/Dashboard/ClaimBadge?badgeId=" + item.Item2 + "'> Claim</a></td>");
                sb.Append("<td style='width:200px'>" + item.Item3 + "</td>");
                sb.Append("<td style='width:200px'>" + item.Item4 + "</td>");
                sb.Append("<td style='width:200px'>" + item.Item5 + "</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            _hub.AddTable(key, sb.ToString());
        }

        private void CreateTableForStatus(string key, List<string> statusList)
        {
            // create HTML table
            StringBuilder sb = new StringBuilder();
            sb.Append("<table cellpadding='3' cellspacing='3'>");

            // header
            sb.Append("<tr>");
            sb.Append("<th><b>Connector</b></th>");
            sb.Append("<th><b>Status</b></th>");
            sb.Append("</tr>");

            // rows
            for (int i = 0; i < statusList.Count; i++)
            {
                sb.Append("<tr>");
                sb.Append("<td style='width:200px'>" + (i + 1).ToString() + "</td>");
                sb.Append("<td style='width:200px'>" + statusList[i] + "</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            _hub.AddTable(key, sb.ToString());
        }
    }
}
