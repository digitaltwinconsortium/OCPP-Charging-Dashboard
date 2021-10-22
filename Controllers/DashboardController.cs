using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpcUaWebDashboard.Controllers
{
    public class StatusHub : Hub
    {
    }

    [Authorize]
    public class DashboardController : Controller
    {
        private static IHubContext<StatusHub> _hubContext;

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
            return View("Index");
        }

        public ActionResult Notify()
        {
            return View("Index");
        }

        public static void AddChargers(string chargers)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addChargers", chargers).GetAwaiter().GetResult();
            }
        }

        public static void AddChart(string name)
        {
            if (_hubContext != null)
            {
               _hubContext.Clients.All.SendAsync("addChart", name).GetAwaiter().GetResult();
            }
        }

        public static void AddDataToChart(string name, string[] timestamps, float[] values)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addDataToChart", name, timestamps, values).GetAwaiter().GetResult();
            }
        }

        public static void ClearCharts()
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("removeDataFromCharts").GetAwaiter().GetResult();
            }
        }

        public static void CreateTableForTransactions(string key, List<Tuple<string,string, string, string, string>> telemetry)
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

        public static void CreateTableForStatus(string key, List<string> statusList)
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
