using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace EVCharging
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // start reading messages from IoT Hub
            IoTHubConfig.ConfigureIotHub();

            // start our web server
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
