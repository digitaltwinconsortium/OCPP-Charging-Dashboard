using Microsoft.AspNetCore.SignalR;

namespace EVCharging
{
    public class StatusHub : Hub
    {
        private IHubContext<StatusHub> _context;

        public StatusHub(IHubContext<StatusHub> context)
        {
            _context = context;
        }

        public void AddChargers(string chargers)
        {
            _context.Clients.All.SendAsync("addChargers", chargers).GetAwaiter().GetResult();
        }

        public void AddChart(string name, string[] timestamps, float[] values)
        {
            _context.Clients.All.SendAsync("addChart", name, timestamps, values).GetAwaiter().GetResult();
        }

        public void AvailableStatus(bool chargerAvailable)
        {
            _context.Clients.All.SendAsync("availableStatus", chargerAvailable).GetAwaiter().GetResult();
        }

        public void AddTable(string key, string content)
        {
            _context.Clients.All.SendAsync("addTable", key, content).GetAwaiter().GetResult();
        }
    }
}
