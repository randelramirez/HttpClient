using System;
using System.Threading.Tasks;
using Client.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            var configuration = BuildConfiguration();
            
            ConfigureServices(serviceCollection, configuration);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Starting HttpClient Best Practices Demo Application");

                // Run our selected service demonstration
                var service = serviceProvider.GetRequiredService<IService>();
                await service.Run();

                logger.LogInformation("HttpClient demonstration completed successfully");
            }
            catch (Exception generalException)
            {
                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger?.LogError(generalException, "An exception occurred while running the demonstration");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static void ConfigureServices(IServiceCollection serviceCollection, IConfiguration configuration)
        {
            // Register configuration
            serviceCollection.AddSingleton(configuration);

            // Configure logging
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Register HttpClient services with Microsoft's recommended patterns
            serviceCollection.AddHttpClientServices();

            // Register all service demonstrations
            RegisterServiceDemonstrations(serviceCollection);

            // Select which demonstration to run
            // Uncomment the desired service to run its demonstration

            #region Basic Patterns
            // Basic CRUD operations with HttpClient
            //serviceCollection.AddScoped<IService, CRUDService>();

            // Sample service with different response handling
            //serviceCollection.AddScoped<IService, SampleService>();
            #endregion

            #region HttpClientFactory Patterns
            // HttpClientFactory management and usage patterns
            //serviceCollection.AddScoped<IService, HttpClientFactoryManagementService>();

            // Custom message handlers demonstration
            //serviceCollection.AddScoped<IService, HttpCustomMessageHandlerService>();
            #endregion

            #region Advanced Patterns
            // Authentication patterns (Bearer, Basic, Refresh tokens)
            //serviceCollection.AddScoped<IService, AuthenticationService>();

            // File upload scenarios (Multipart, Stream, Progress)
            //serviceCollection.AddScoped<IService, FileUploadService>();

            // Error handling and fault tolerance
            //serviceCollection.AddScoped<IService, ErrorHandlingService>();

            // Concurrency and parallel request patterns
            //serviceCollection.AddScoped<IService, ConcurrencyService>();

            // Custom headers and request configuration
            //serviceCollection.AddScoped<IService, CustomHeadersService>();

            // Webhook patterns and batch processing
            //serviceCollection.AddScoped<IService, WebhookService>();

            // GraphQL integration patterns
            //serviceCollection.AddScoped<IService, GraphQLService>();

            // Stream processing for large payloads
            //serviceCollection.AddScoped<IService, StreamService>();
            #endregion

            #region Microsoft Best Practices
            // ✨ Custom resilience patterns (Educational)
            //serviceCollection.AddScoped<IService, ResiliencePatternService>();

            // ✨ Performance optimization patterns
            //serviceCollection.AddScoped<IService, PerformanceOptimizationService>();

            // ✨ Production-ready patterns (Headers, Security, Monitoring)
            //serviceCollection.AddScoped<IService, ProductionReadyHttpClientService>();
            #endregion

            #region Microsoft Recommended (Polly)
            // 🏆 RECOMMENDED: Microsoft's official Polly resilience patterns
            serviceCollection.AddScoped<IService, PollyResilienceService>();
            #endregion
        }

        private static void RegisterServiceDemonstrations(IServiceCollection serviceCollection)
        {
            // Register all service demonstrations as scoped services
            serviceCollection.AddScoped<CRUDService>();
            serviceCollection.AddScoped<SampleService>();
            serviceCollection.AddScoped<HttpClientFactoryManagementService>();
            serviceCollection.AddScoped<HttpCustomMessageHandlerService>();
            serviceCollection.AddScoped<AuthenticationService>();
            serviceCollection.AddScoped<FileUploadService>();
            serviceCollection.AddScoped<ErrorHandlingService>();
            serviceCollection.AddScoped<ConcurrencyService>();
            serviceCollection.AddScoped<CustomHeadersService>();
            serviceCollection.AddScoped<WebhookService>();
            serviceCollection.AddScoped<GraphQLService>();
            serviceCollection.AddScoped<StreamService>();
            
            // Custom implementations (Educational)
            serviceCollection.AddScoped<ResiliencePatternService>();
            serviceCollection.AddScoped<PerformanceOptimizationService>();
            serviceCollection.AddScoped<ProductionReadyHttpClientService>();
            
            // Microsoft recommended Polly implementation
            serviceCollection.AddScoped<PollyResilienceService>();
        }
    }
}
