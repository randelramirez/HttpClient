using System;
using System.Net.Http;
using System.Threading.Tasks;
using Client.MessageHandlers;
using Client.Services;
using Client.TypedClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                // Run our IntegrationService containing all samples and
                // await this call to ensure the application doesn't 
                // prematurely exit.
                await serviceProvider.GetService<IService>().Run();
            }
            catch (Exception generalException)
            {
                // log the exception
                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger.LogError(generalException,
                    "An exception happened while running the integration service.");
            }

            Console.ReadKey();
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });
            // register IService on our container with a 
            // scoped lifetime

            // For the CRUD demos
            //serviceCollection.AddScoped<IService, CRUDService>();

            // For stream demos
            //serviceCollection.AddScoped<IService, StreamService>();

            // For the HttpClientFactory demos
            //serviceCollection.AddScoped<IService, HttpClientFactoryManagementService>();

            // Using a named client
            serviceCollection.AddHttpClient("ContactsClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = new TimeSpan(0, 0, 30);
                client.DefaultRequestHeaders.Clear();
            })
            .ConfigurePrimaryHttpMessageHandler(handler => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
            });

            serviceCollection.AddHttpClient("ContactsClientCustomHandler", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = new TimeSpan(0, 0, 30);
                client.DefaultRequestHeaders.Clear();
            })
            // We use 20 seconds at least for this demo so that we can differtiate timeout and cancelled exceptions 
            // (the httpclient have 30 seconds before timeout and then it throws a cancelled exception)
           .AddHttpMessageHandler(handler => new TimeOutDelegatingHandler(TimeSpan.FromSeconds(20)))
           .AddHttpMessageHandler(handler => new RetryPolicyDelegatingHandler(2))
           .ConfigurePrimaryHttpMessageHandler(handler =>
           new HttpClientHandler()
           {
               AutomaticDecompression = System.Net.DecompressionMethods.GZip
           });


            // using Typed client
            serviceCollection.AddHttpClient<ContactsClient>()
            .ConfigurePrimaryHttpMessageHandler(handler =>
                 new HttpClientHandler()
                 {
                     AutomaticDecompression = System.Net.DecompressionMethods.GZip
                 });

            // For the dealing with errors and faults demos
            //serviceCollection.AddScoped<IService, ErrorHandlingService>();

            // For the custom http handlers demos
            serviceCollection.AddScoped<IService, HttpCustomMessageHandlerService>();
        }
    }
}
