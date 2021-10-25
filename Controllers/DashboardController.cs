﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCPPCentralSystem.Schemas.DTDL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpcUaWebDashboard.Controllers
{
    public class StatusHub : Hub
    {
    }

    [Authorize]
    public class DashboardController : Controller
    {
        public static ConcurrentDictionary<string, OCPPChargePoint> CentralStation { get; set; }

        private static IHubContext<StatusHub> _hubContext;

        private static Timer _timer = new Timer(GenerateDashboard, null, -1, -1);

        public DashboardController(IHubContext<StatusHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public ActionResult Privacy()
        {
            return View("Privacy");
        }

        public ActionResult Index()
        {
            _timer.Change(5000, 5000);
       
            return View("Index");
        }

        public ActionResult Notify()
        {
            return View("Index");
        }

        private static void GenerateDashboard(Object state)
        {
            if (CentralStation != null)
            {
                // turn off timer
                _timer.Change(-1, -1);

                // clear previous data
                ClearCharts();

                StringBuilder sb = new StringBuilder();
                sb.Append("<table cellpadding='3' cellspacing='3'>");

                sb.Append("<tr>");
                foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in CentralStation)
                {
                    sb.Append("<th>");
                    sb.Append("<b>" + chargePoint.Value.ID + "</b>");
                    sb.Append("</th>");
                }
                sb.Append("</tr>");

                sb.Append("<tr>");
                foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in CentralStation)
                {
                    sb.Append("<td valign='top'>");
                    sb.Append("<hr/>");
                    sb.Append("<div id=\"" + chargePoint.Value.ID + "statustable\" width=\"400\"/>");
                    sb.Append("</td>");
                }
                sb.Append("</tr>");

                sb.Append("<tr>");
                foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in CentralStation)
                {
                    sb.Append("<td valign='top'>");
                    sb.Append("<table cellpadding='3' cellspacing='3'>");
                    foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                    {
                        string name = chargePoint.Value.ID + "_" + connector.Value.ID.ToString();

                        sb.Append("<tr>");
                        sb.Append("<td valign='top'>");
                        sb.Append("<hr/>");
                        sb.Append("<div id=\"" + name + "transactiontable\" width =\"380\"></div>");
                        sb.Append("</td>");
                        sb.Append("</tr>");

                        sb.Append("<tr>");
                        sb.Append("<td valign='top'>");
                        sb.Append("<hr/>");
                        sb.Append("<canvas id=\"" + name + "\" width=\"380\" height=\"200\"/>");
                        sb.Append("</td>");
                        sb.Append("</tr>");
                    }
                    sb.Append("</table>");
                    sb.Append("</td>");
                }
                sb.Append("</tr>");

                sb.Append("</table>");

                AddChargers(sb.ToString());

                foreach (KeyValuePair<string, OCPPChargePoint> chargePoint in CentralStation)
                {
                    // build status table of connectors
                    List<string> status = new List<string>();

                    foreach (KeyValuePair<int, Connector> connector in chargePoint.Value.Connectors)
                    {
                        status.Add(connector.Value.Status);
                        string chartName = chargePoint.Value.ID + "_" + connector.Value.ID.ToString();

                        // add chart
                        AddChart(chartName);

                        // update our line chart in the dashboard
                        List<string> labels = new List<string>();
                        List<float> readings = new List<float>();
                        foreach (MeterReading reading in connector.Value.MeterReadings)
                        {
                            labels.Add(reading.Timestamp.ToString());
                            readings.Add(reading.MeterValue);
                        }
                        AddDataToChart(chartName, labels.ToArray(), readings.ToArray());

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

                        CreateTableForTransactions(chargePoint.Value.ID + "_" + connector.Value.ID.ToString(), tableEntries);
                    }

                    CreateTableForStatus(chargePoint.Value.ID, status);
                }
            }
        }

        private static void AddChargers(string chargers)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addChargers", chargers).GetAwaiter().GetResult();
            }
        }

        private static void AddChart(string name)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addChart", name).GetAwaiter().GetResult();
            }
        }

        private static void AddDataToChart(string name, string[] timestamps, float[] values)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addDataToChart", name, timestamps, values).GetAwaiter().GetResult();
            }
        }

        private static void ClearCharts()
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("removeDataFromCharts").GetAwaiter().GetResult();
            }
        }

        private static void CreateTableForTransactions(string key, List<Tuple<string,string, string, string, string>> telemetry)
        {
            if (_hubContext != null)
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
                sb.Append("<th><b>Used Power (Wh)</b></th>");
                sb.Append("</tr>");

                // rows
                foreach (Tuple<string, string, string, string, string> item in telemetry)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style='width:200px'>" + item.Item1 + "</td>");
                    sb.Append("<td style='width:200px'>" + item.Item2 + "</td>");
                    sb.Append("<td style='width:200px'>" + item.Item3 + "</td>");
                    sb.Append("<td style='width:200px'>" + item.Item4 + "</td>");
                    sb.Append("<td style='width:200px'>" + item.Item5 + "</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</table>");

                _hubContext.Clients.All.SendAsync("addTransactionTable", key, sb.ToString()).GetAwaiter().GetResult();
            }
        }

        private static void CreateTableForStatus(string key, List<string> statusList)
        {
            if (_hubContext != null)
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
                bool connectorAvailable = false;
                for (int i = 0; i < statusList.Count; i++)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style='width:200px'>" + (i + 1).ToString() + "</td>");
                    sb.Append("<td style='width:200px'>" + statusList[i] + "</td>");
                    sb.Append("</tr>");

                    if (statusList[i] == "Available")
                    {
                        connectorAvailable = true;
                    }
                }

                sb.Append("</table>");

                _hubContext.Clients.All.SendAsync("availableStatus", key, connectorAvailable).GetAwaiter().GetResult();
                _hubContext.Clients.All.SendAsync("addStatusTable", key, sb.ToString()).GetAwaiter().GetResult();
            }
        }
    }
}
