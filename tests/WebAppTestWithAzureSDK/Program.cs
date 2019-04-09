using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Rest;
using WebAppTestWithAzureSDK.AzureClients;
using WebAppTestWithAzureSDK.Credentials;

namespace WebAppTestWithAzureSDK
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args).
                ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddEnvironmentVariables();
                    var env = hostingContext.HostingEnvironment;
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json",
                            optional: true, reloadOnChange: true)
                        .AddJsonFile("azure.json", optional: true, reloadOnChange: true);
                    config.AddCommandLine(args);
                })
                .ConfigureServices(services =>
                {
                    // setup a scoped resource management client
                    services.AddScoped<IResourceManagementClient, EnhancedResourceManagementClient>();
                    // setup a singleton token provider
                    services.AddSingleton<ServiceClientCredentials, DefaultChainServiceCredentials>();
                })
                .UseStartup<Startup>();
    }
}
