using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using OpcUaWebDashboard;

namespace EVCharging
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IoTHubConfig.ConfigureIotHub();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
