using System.Collections.Generic;

namespace EVCharging
{
    public interface IHtmlContentGenerator
    {
        List<string> NotificationList { get; set; }

        Dictionary<string, string> BadgeClaims { get; set; }
    }
}