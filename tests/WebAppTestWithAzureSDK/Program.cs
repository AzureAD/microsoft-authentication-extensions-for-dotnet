using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                    config.AddJsonFile("azure.json", optional: true, reloadOnChange: false);
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
