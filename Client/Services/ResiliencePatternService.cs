using Core.ViewModels;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Services
{
    /// <summary>
    /// Demonstrates Microsoft's recommended patterns for HttpClient resilience.
    /// Based on Microsoft's Cloud Design Patterns and .NET documentation.
    /// </summary>
    public class ResiliencePatternService : IService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResiliencePatternService> _logger;

        public ResiliencePatternService(
            IHttpClientFactory httpClientFactory,
            ILogger<ResiliencePatternService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run()
        {
            _logger.LogInformation("Starting resilience patterns demonstration");

            try
            {
                // Demonstrate different resilience patterns
                await DemonstrateRetryWithExponentialBackoff();
                await DemonstrateCircuitBreakerPattern();
                await DemonstrateBulkheadPattern();
                await DemonstrateTimeoutPattern();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resilience patterns demonstration");
            }
        }

        /// <summary>
        /// Demonstrates retry with exponential backoff - Microsoft's recommended approach.
        /// </summary>
        private async Task DemonstrateRetryWithExponentialBackoff()
        {
            _logger.LogInformation("=== Retry with Exponential Backoff Pattern ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            const int maxRetries = 3;
            var baseDelay = TimeSpan.FromSeconds(1);
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync("api/contacts");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                        _logger.LogInformation("Successfully retrieved {Count} contacts on attempt {Attempt}", 
                            contacts?.Count ?? 0, attempt + 1);
                        return;
                    }
                    
                    if (attempt == maxRetries)
                    {
                        _logger.LogError("All retry attempts failed. Status: {StatusCode}", response.StatusCode);
                        return;
                    }
                    
                    // Calculate exponential backoff delay
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                    _logger.LogWarning("Attempt {Attempt} failed with {StatusCode}. Retrying in {Delay}ms", 
                        attempt + 1, response.StatusCode, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Network error on attempt {Attempt}", attempt + 1);
                    
                    if (attempt == maxRetries)
                    {
                        throw;
                    }
                    
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Demonstrates circuit breaker pattern for preventing cascading failures.
        /// </summary>
        private async Task DemonstrateCircuitBreakerPattern()
        {
            _logger.LogInformation("=== Circuit Breaker Pattern ===");
            
            // This would typically use a configured client with CircuitBreakerDelegatingHandler
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            try
            {
                var response = await httpClient.GetAsync("api/contacts");
                _logger.LogInformation("Circuit breaker: Request succeeded with status {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Circuit breaker: Request failed");
            }
        }

        /// <summary>
        /// Demonstrates bulkhead pattern for resource isolation.
        /// </summary>
        private async Task DemonstrateBulkheadPattern()
        {
            _logger.LogInformation("=== Bulkhead Pattern ===");
            
            // Use different HTTP clients for different types of operations
            var criticalClient = _httpClientFactory.CreateClient("ContactsClient");
            var nonCriticalClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var criticalTask = ExecuteCriticalOperation(criticalClient);
            var nonCriticalTask = ExecuteNonCriticalOperation(nonCriticalClient);
            
            await Task.WhenAll(criticalTask, nonCriticalTask);
        }

        /// <summary>
        /// Demonstrates proper timeout handling with cancellation tokens.
        /// </summary>
        private async Task DemonstrateTimeoutPattern()
        {
            _logger.LogInformation("=== Timeout Pattern ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                var response = await httpClient.GetAsync("api/contacts", cts.Token);
                _logger.LogInformation("Timeout pattern: Request completed within timeout");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("Timeout pattern: Request timed out");
            }
            catch (TaskCanceledException ex) when (cts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout pattern: Request was cancelled");
            }
        }

        private async Task ExecuteCriticalOperation(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync("api/contacts");
                _logger.LogInformation("Critical operation completed with status {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical operation failed");
            }
        }

        private async Task ExecuteNonCriticalOperation(HttpClient httpClient)
        {
            try
            {
                var response = await httpClient.GetAsync("api/contacts");
                _logger.LogInformation("Non-critical operation completed with status {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-critical operation failed (this is acceptable)");
            }
        }
    }
}