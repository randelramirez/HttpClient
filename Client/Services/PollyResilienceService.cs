using Core.ViewModels;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client.Services
{
    /// <summary>
    /// Demonstrates Microsoft's recommended approach using Polly with HttpClientFactory.
    /// Based on Microsoft's official documentation:
    /// https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
    /// 
    /// The key advantage is that Polly policies are configured at the HttpClient level,
    /// not in individual service methods, following Microsoft's best practices.
    /// </summary>
    public class PollyResilienceService : IService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PollyResilienceService> _logger;

        public PollyResilienceService(
            IHttpClientFactory httpClientFactory,
            ILogger<PollyResilienceService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run()
        {
            _logger.LogInformation("=== Microsoft Polly with HttpClientFactory Demo ===");
            _logger.LogInformation("Key Benefit: Policies are configured at HttpClient level, not in service methods");

            await DemonstratePollyIntegratedClient();
            await DemonstrateProductionReadyClient();
            await DemonstrateBasicVsPollyComparison();
        }

        /// <summary>
        /// Demonstrates using an HttpClient that has Polly policies pre-configured.
        /// This is Microsoft's recommended approach - policies are transparent to the service code.
        /// </summary>
        private async Task DemonstratePollyIntegratedClient()
        {
            _logger.LogInformation("\n=== Polly-Integrated HttpClient (Microsoft's Recommended Pattern) ===");

            // Get HttpClient with pre-configured Polly policies
            // Policies are configured in HttpClientServices.cs following Microsoft's patterns
            var httpClient = _httpClientFactory.CreateClient("PollyClient");

            try
            {
                _logger.LogInformation("Making request through Polly-enabled HttpClient...");
                _logger.LogInformation("Note: Retry, Circuit Breaker, and Timeout policies are automatically applied!");

                // Simple HTTP call - Polly policies are applied transparently
                var response = await httpClient.GetAsync("api/contacts");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                
                _logger.LogInformation("? SUCCESS: Retrieved {Count} contacts", contacts?.Count ?? 0);
                _logger.LogInformation("? Polly policies (retry, circuit breaker, timeout) were applied automatically!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? Request failed even after Polly policies were applied");
                _logger.LogInformation("This means all retry attempts were exhausted and/or circuit breaker opened");
            }
        }

        /// <summary>
        /// Demonstrates the production-ready client with logging and Polly policies.
        /// </summary>
        private async Task DemonstrateProductionReadyClient()
        {
            _logger.LogInformation("\n=== Production-Ready Client (Logging + Polly) ===");

            // Get the production client with both logging and Polly policies
            var httpClient = _httpClientFactory.CreateClient("ProductionClient");

            try
            {
                _logger.LogInformation("Making request through Production client...");
                _logger.LogInformation("Features: Structured Logging + Polly Resilience + Performance Optimization");

                var response = await httpClient.GetAsync("api/contacts");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                
                _logger.LogInformation("? Production client SUCCESS: Retrieved {Count} contacts", contacts?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? Production client request failed");
            }
        }

        /// <summary>
        /// Demonstrates the difference between basic and Polly-enabled clients.
        /// </summary>
        private async Task DemonstrateBasicVsPollyComparison()
        {
            _logger.LogInformation("\n=== Comparison: Basic Client vs Polly Client ===");

            // Basic client - no resilience policies
            var basicClient = _httpClientFactory.CreateClient("ContactsClient");
            
            // Polly client - with resilience policies
            var pollyClient = _httpClientFactory.CreateClient("PollyClient");

            _logger.LogInformation("1?? Testing Basic Client (no resilience):");
            try
            {
                var basicResponse = await basicClient.GetAsync("api/contacts");
                basicResponse.EnsureSuccessStatusCode();
                _logger.LogInformation("? Basic client succeeded (no policies applied)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("? Basic client failed: {Error}", ex.Message);
                _logger.LogInformation("Basic client has no retry or circuit breaker protection");
            }

            _logger.LogInformation("\n2?? Testing Polly Client (with resilience):");
            try
            {
                var pollyResponse = await pollyClient.GetAsync("api/contacts");
                pollyResponse.EnsureSuccessStatusCode();
                _logger.LogInformation("? Polly client succeeded (policies were applied transparently)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("? Polly client failed: {Error}", ex.Message);
                _logger.LogInformation("Polly client failed after applying all resilience policies");
            }

            _logger.LogInformation("\n?? Key Differences:");
            _logger.LogInformation("• Basic Client: No resilience, fails immediately on errors");
            _logger.LogInformation("• Polly Client: Automatic retry, circuit breaker, timeout policies");
            _logger.LogInformation("• Service Code: Identical for both clients - policies are transparent!");
        }
    }
}