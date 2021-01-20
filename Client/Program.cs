using System;
using System.Threading.Tasks;
using Client.Services;
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

            serviceCollection.AddHttpClientServices();

            // register IService on our container with a 
            // scoped lifetime

            // For the CRUD demos
            //serviceCollection.AddScoped<IService, CRUDService>();

            // For stream demos
            //serviceCollection.AddScoped<IService, StreamService>();

            // For the HttpClientFactory demos
            //serviceCollection.AddScoped<IService, HttpClientFactoryManagementService>();



            // For the dealing with errors and faults demos
            serviceCollection.AddScoped<IService, ErrorHandlingService>();

            // For the custom http handlers demos
            //serviceCollection.AddScoped<IService, HttpCustomMessageHandlerService>();
        }
    }
}
