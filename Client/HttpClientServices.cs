using Client.MessageHandlers;
using Client.TypedClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;

namespace Client
{
    public static class HttpClientServices
    {
        public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
        {
            services.ConfigureBasicNamedClient();
            services.ConfigurePollyResilientClient();
            services.ConfigureProductionReadyClient();
            services.ConfigurePerformanceOptimizedClient();
            services.ConfigureTypedClient();
            
            // Educational-only configuration (marked as obsolete)
            services.ConfigureEducationalClientWithCustomHandlers();
            
            return services;
        }

        /// <summary>
        /// Basic named client configuration following Microsoft's minimal setup.
        /// </summary>
        private static IServiceCollection ConfigureBasicNamedClient(this IServiceCollection services)
        {
            services.AddHttpClient("ContactsClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Clear();
                
                // Add default headers following Microsoft recommendations
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientDemo/1.0 (.NET 9.0)");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });

            return services;
        }

        /// <summary>
        /// 🏆 PRODUCTION RECOMMENDED: Polly-based resilient client following Microsoft's official recommendations.
        /// Based on: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
        /// </summary>
        private static IServiceCollection ConfigurePollyResilientClient(this IServiceCollection services)
        {
            services.AddHttpClient("PollyClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = TimeSpan.FromSeconds(100); // Longer timeout for retry scenarios
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientDemo-Polly/1.0 (.NET 9.0)");
            })
            // Microsoft's recommended Polly policies
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy())
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = false,
                MaxConnectionsPerServer = 10
            });

            return services;
        }

        /// <summary>
        /// 🏆 PRODUCTION RECOMMENDED: Production-ready client with comprehensive logging and monitoring.
        /// Based on Microsoft's enterprise patterns.
        /// </summary>
        private static IServiceCollection ConfigureProductionReadyClient(this IServiceCollection services)
        {
            services.AddHttpClient("ProductionClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = TimeSpan.FromSeconds(100); // Longer timeout for production
                client.DefaultRequestHeaders.Clear();
                
                // Production headers
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientDemo/1.0 (.NET 9.0)");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("X-Client-Version", "1.0.0");
            })
            .AddHttpMessageHandler<LoggingDelegatingHandler>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = false, // Disable cookies for better performance in APIs
                MaxConnectionsPerServer = 10 // Optimize connection pooling
            });

            return services;
        }

        /// <summary>
        /// Performance-optimized client for high-throughput scenarios.
        /// Based on Microsoft's performance best practices.
        /// </summary>
        private static IServiceCollection ConfigurePerformanceOptimizedClient(this IServiceCollection services)
        {
            services.AddHttpClient("PerformanceClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = TimeSpan.FromSeconds(15); // Shorter timeout for performance
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "HttpClientDemo-Performance/1.0");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = false,
                MaxConnectionsPerServer = 50, // Higher connection limit for performance
                UseProxy = false, // Disable proxy for better performance if not needed
                PreAuthenticate = false
            });

            return services;
        }

        /// <summary>
        /// 🎓 EDUCATIONAL ONLY: Client configuration with custom handlers for learning purposes.
        /// ⚠️  DO NOT USE IN PRODUCTION - Use Polly policies instead!
        /// </summary>
        [Obsolete("Use PollyClient or ProductionClient instead. This is for educational purposes only.", false)]
        private static IServiceCollection ConfigureEducationalClientWithCustomHandlers(this IServiceCollection services)
        {
            services.AddHttpClient("ContactsClientCustomHandler", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Clear();
            })
#pragma warning disable CS0618 // Type or member is obsolete
            .AddHttpMessageHandler(() => new TimeOutDelegatingHandler(TimeSpan.FromSeconds(15)))
            .AddHttpMessageHandler(() => new RetryPolicyDelegatingHandler(2))
#pragma warning restore CS0618 // Type or member is obsolete
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
            });

            return services;
        }

        /// <summary>
        /// 🏆 PRODUCTION RECOMMENDED: Typed client configuration following Microsoft's typed client pattern.
        /// </summary>
        private static IServiceCollection ConfigureTypedClient(this IServiceCollection services)
        {
            services.AddHttpClient<ContactsClient>(client =>
            {
                // Base configuration is handled in the ContactsClient constructor
                // This allows for more flexibility in typed clients
            })
            .AddHttpMessageHandler<LoggingDelegatingHandler>()
            .AddPolicyHandler(GetRetryPolicy())
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = false
            });

            return services;
        }

        #region Polly Policy Factory Methods - Microsoft's Recommended Patterns

        /// <summary>
        /// Gets the retry policy following Microsoft's recommendations.
        /// Implements exponential backoff with jitter for optimal performance.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + // Exponential backoff
                        TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100))); // Add jitter
        }

        /// <summary>
        /// Gets the circuit breaker policy following Microsoft's recommendations.
        /// Prevents cascading failures and allows the system to recover.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Gets the timeout policy following Microsoft's recommendations.
        /// Prevents requests from hanging indefinitely.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 second timeout
        }

        #endregion
    }
}
