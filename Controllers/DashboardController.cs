using EVCharging.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCPPCentralSystem.Schemas.DTDL;
using System;
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
        private readonly UserManager<IdentityUser> _userManager;

        private readonly SignInManager<IdentityUser> _signInManager;

        private static IEmailSender _emailSender;

        private static IHubContext<StatusHub> _hubContext;

        private static Timer _timer = new Timer(GenerateDashboard, null, -1, -1);

        private static List<string> _notificationList = new List<string>();

        private static Dictionary<string, string> _badgeClaims = new Dictionary<string, string>();

        public DashboardController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender,
            IHubContext<StatusHub> hubContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _hubContext = hubContext;
        }

        public ActionResult Privacy()
        {
            return View("Privacy");
        }

        public ActionResult Index()
        {
            _timer.Change(2000, 2000);

            return View("Index");
        }

        public ActionResult Notify()
        {
            IdentityUser user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            bool signedIn = _signInManager.IsSignedIn(User);

            if ((user != null) && user.EmailConfirmed && signedIn)
            {
                _notificationList.Add(user.Email);
                Console.WriteLine("Added user " + user.Email + " to notification list.");

                return View("Notification");
            }
            else
            {
                return View("Index");
            }
        }

        public ActionResult ClaimBadge(string badgeId)
        {
            IdentityUser user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            bool signedIn = _signInManager.IsSignedIn(User);

            if ((user != null) && user.EmailConfirmed && signedIn)
            {
                if (!_badgeClaims.ContainsKey(badgeId))
                {
                    _badgeClaims.Add(badgeId, user.Email);
                    Console.WriteLine("Added user " + user.Email + " to badge claim list.");
                }

                return View("Claim", new ClaimModel { BadgeId = badgeId });
            }
            else
            {
                return View("Index");
            }
        }

        private static void GenerateDashboard(object state)
        {
            MessageProcessor.CentralStationLock.Wait();
            try
            {
                if (MessageProcessor.CentralStation != null)
                {
                    // slow down timer
                    _timer.Change(15000, 15000);

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

                    AddChargers(sb.ToString());

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
                            AddChart(chargePoint.Value.ID + "_" + connector.Value.ID.ToString() + "_chart", labels.ToArray(), readings.ToArray());

                            // add items to our transaction table
                            List<Tuple<string, string, string, string, string>> tableEntries = new List<Tuple<string, string, string, string, string>>();

                            foreach (KeyValuePair<int, Transaction> transaction in connector.Value.CurrentTransactions)
                            {
                                if (transaction.Value.StopTime != DateTime.MinValue)
                                {
                                    // notify users that have claimed this transaction
                                    if (_badgeClaims.ContainsKey(transaction.Value.BadgeID))
                                    {
                                        _emailSender.SendEmailAsync(
                                            _badgeClaims[transaction.Value.BadgeID],
                                            "Microsoft EV Charging", "Your vehicle charging from " + transaction.Value.StartTime.ToString() + " is now complete. Please move your vehicle.")
                                            .GetAwaiter().GetResult();
                                         Console.WriteLine("Sent charging complete notification email to " + _badgeClaims[transaction.Value.BadgeID] + ".");
                                        _badgeClaims.Remove(transaction.Value.BadgeID);
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
                        int numPeopleWaiting = _notificationList.Count;
                        while (_notificationList.Count > 0)
                        {
                            _emailSender.SendEmailAsync(
                                _notificationList[0],
                                "Microsoft EV Charging", "A charger is now available and there are " + (numPeopleWaiting - 1).ToString() + " other people waiting and got notified.")
                                .GetAwaiter().GetResult();
                            Console.WriteLine("Sent charger available notification email to " + _notificationList[0] + ".");
                            _notificationList.RemoveAt(0);
                        }
                    }

                    _hubContext.Clients.All.SendAsync("availableStatus", chargerAvailable).GetAwaiter().GetResult();
                }
            }
            finally
            {
                MessageProcessor.CentralStationLock.Release();
            }
        }

        private static void AddChargers(string chargers)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addChargers", chargers).GetAwaiter().GetResult();
            }
        }

        private static void AddChart(string name, string[] timestamps, float[] values)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("addChart", name, timestamps, values).GetAwaiter().GetResult();
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

                _hubContext.Clients.All.SendAsync("addTable", key, sb.ToString()).GetAwaiter().GetResult();
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
                for (int i = 0; i < statusList.Count; i++)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style='width:200px'>" + (i + 1).ToString() + "</td>");
                    sb.Append("<td style='width:200px'>" + statusList[i] + "</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</table>");

                _hubContext.Clients.All.SendAsync("addTable", key, sb.ToString()).GetAwaiter().GetResult();
            }
        }
    }
}
