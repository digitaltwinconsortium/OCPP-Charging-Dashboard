using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace OpcUaWebDashboard.Controllers
{
    public class StatusHub : Hub
    {
    }

    //[Authorize]
    public class DashboardController : Controller
    {
        private static IHubContext<StatusHub> _hubContext;
        private static List<Tuple<string, string, string, string, string>> _latestTelemetry;
        private static Stopwatch _tableTimer;
        private static bool _pageReloaded = false;

        public DashboardController(IHubContext<StatusHub> hubContext)
        {
            _latestTelemetry = new List<Tuple<string, string, string, string, string>>();
            _tableTimer = new Stopwatch();
            _tableTimer.Start();
            _hubContext = hubContext;
        }

        public ActionResult Privacy()
        {
            return View("Privacy");
        }

        public ActionResult Index()
        {
            _pageReloaded = true;

            return View("Index");
        }

        public static void AddDatasetToChart(string name)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addDatasetToChart", name).GetAwaiter().GetResult();
            }
        }

        public static void AddDataToChart(string timestamp, float[] values)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addDataToChart", timestamp, values).GetAwaiter().GetResult();
            }
        }

        public static void ClearChart()
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("removeDataFromChart").GetAwaiter().GetResult();
            }
        }

        public static void CreateTableForTelemetry(List<Tuple<string,string, string, string, string>> telemetry)
        {
            if (_hubContext != null)
            {
                foreach (Tuple<string, string, string, string, string> item in telemetry)
                {
                    bool found = false;
                    for (int i = 0; i < _latestTelemetry.Count; i++)
                    {
                        if (_latestTelemetry[i].Item1 == item.Item1)
                        {
                            _latestTelemetry[i] = item;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _latestTelemetry.Add(item);
                    }
                }

                // update table every second
                if (_tableTimer.ElapsedMilliseconds > 1000)
                {
                    // create HTML table
                    StringBuilder sb = new StringBuilder();
                    sb.Append("<table width='1000px' cellpadding='3' cellspacing='3'>");

                    // header
                    sb.Append("<tr>");
                    sb.Append("<th><b>Transaction</b></th>");
                    sb.Append("<th><b>Badge</b></th>");
                    sb.Append("<th><b>Start Time</b></th>");
                    sb.Append("<th><b>Stop Time</b></th>");
                    sb.Append("<th><b>Used Power (Wh)</b></th>");
                    sb.Append("</tr>");

                    // rows
                    foreach (Tuple<string, string, string, string, string> item in _latestTelemetry)
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

                    _hubContext.Clients.All.SendAsync("addTable", sb.ToString()).GetAwaiter().GetResult();

                    if (_pageReloaded == true)
                    {
                        _pageReloaded = false;
                    }

                    _tableTimer.Restart();
                }
            }
        }
    }
}