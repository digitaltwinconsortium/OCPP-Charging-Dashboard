using System.Collections.Generic;
using System.Threading;

namespace EVCharging
{
    public interface IHtmlContentGenerator
    {
        List<string> NotificationList { get; set; }

        Dictionary<string, string> BadgeClaims { get; set; }

        Timer SignalRTimer { get; set; }
    }
}