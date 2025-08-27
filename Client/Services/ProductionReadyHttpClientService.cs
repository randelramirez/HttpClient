using Core.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
    /// <summary>
    /// Demonstrates Microsoft's recommended patterns for production-ready HttpClient usage.
    /// Based on Microsoft's enterprise application patterns and best practices.
    /// </summary>
    public class ProductionReadyHttpClientService : IService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProductionReadyHttpClientService> _logger;
        private readonly IConfiguration _configuration;

        public ProductionReadyHttpClientService(
            IHttpClientFactory httpClientFactory,
            ILogger<ProductionReadyHttpClientService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
        }

        public async Task Run()
        {
            _logger.LogInformation("Starting production-ready HttpClient patterns demonstration");

            await DemonstrateProperHeaderManagement();
            await DemonstrateConfigurationBasedClient();
            await DemonstrateSecureApiCalls();
            await DemonstrateHealthChecks();
            await DemonstrateCorrelationIdPropagation();
        }

        /// <summary>
        /// Demonstrates proper header management following Microsoft's guidelines.
        /// </summary>
        private async Task DemonstrateProperHeaderManagement()
        {
            _logger.LogInformation("=== Proper Header Management ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Add standard headers
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            
            // Add custom headers for observability
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
            request.Headers.Add("X-Client-Version", "1.0.0");
            request.Headers.Add("X-Correlation-ID", System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString());
            
            // Set User-Agent following RFC guidelines
            request.Headers.UserAgent.ParseAdd("HttpClientDemo/1.0 (.NET 9.0)");
            
            using var response = await httpClient.SendAsync(request);
            
            _logger.LogInformation("Request sent with proper headers. Status: {StatusCode}", response.StatusCode);
            
            // Log response headers for debugging
            _logger.LogDebug("Response headers: {Headers}", response.Headers);
        }

        /// <summary>
        /// Demonstrates configuration-based client setup for different environments.
        /// </summary>
        private async Task DemonstrateConfigurationBasedClient()
        {
            _logger.LogInformation("=== Configuration-Based Client ===");
            
            // This demonstrates how to use configuration for different environments
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            // In production, these would come from appsettings.json, environment variables, or Azure Key Vault
            var apiTimeout = TimeSpan.FromSeconds(30); // _configuration.GetValue<int>("ApiSettings:TimeoutSeconds")
            var maxRetries = 3; // _configuration.GetValue<int>("ApiSettings:MaxRetries")
            var baseUrl = "https://localhost:44354/"; // _configuration.GetValue<string>("ApiSettings:BaseUrl")
            
            _logger.LogInformation("Using configuration - BaseUrl: {BaseUrl}, Timeout: {Timeout}s, MaxRetries: {MaxRetries}",
                baseUrl, apiTimeout.TotalSeconds, maxRetries);
            
            using var response = await httpClient.GetAsync("api/contacts");
            _logger.LogInformation("Configuration-based request completed with status {StatusCode}", response.StatusCode);
        }

        /// <summary>
        /// Demonstrates secure API calls with proper authentication patterns.
        /// </summary>
        private async Task DemonstrateSecureApiCalls()
        {
            _logger.LogInformation("=== Secure API Calls ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Demonstrate different authentication methods
            
            // Option 1: Bearer Token (most common for APIs)
            var bearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."; // Would come from secure storage
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            
            // Option 2: API Key in header (alternative)
            // request.Headers.Add("X-API-Key", apiKey);
            
            // Option 3: Custom authentication scheme
            // request.Headers.Authorization = new AuthenticationHeaderValue("CustomScheme", customToken);
            
            // Add security headers
            request.Headers.Add("X-Requested-With", "HttpClient");
            
            try
            {
                using var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Secure API call succeeded with status {StatusCode}", response.StatusCode);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Authentication failed - token may be expired or invalid");
                }
                else
                {
                    _logger.LogWarning("Secure API call failed with status {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Secure API call failed with exception");
            }
        }

        /// <summary>
        /// Demonstrates health check patterns for service monitoring.
        /// </summary>
        private async Task DemonstrateHealthChecks()
        {
            _logger.LogInformation("=== Health Check Patterns ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            try
            {
                // Test with a known endpoint since we don't have a health endpoint
                using var response = await httpClient.GetAsync("api/contacts");
                var isHealthy = response.IsSuccessStatusCode;
                _logger.LogInformation("Service health check via contacts endpoint: {IsHealthy}", 
                    isHealthy ? "Healthy" : "Unhealthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service health check failed");
            }
        }

        /// <summary>
        /// Demonstrates correlation ID propagation for distributed tracing.
        /// </summary>
        private async Task DemonstrateCorrelationIdPropagation()
        {
            _logger.LogInformation("=== Correlation ID Propagation ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            // Generate or use existing correlation ID
            var correlationId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString();
            
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Add correlation ID for distributed tracing
            request.Headers.Add("X-Correlation-ID", correlationId);
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
            
            // Add trace context if available
            if (System.Diagnostics.Activity.Current != null)
            {
                request.Headers.Add("traceparent", System.Diagnostics.Activity.Current.Id);
                if (!string.IsNullOrEmpty(System.Diagnostics.Activity.Current.TraceStateString))
                {
                    request.Headers.Add("tracestate", System.Diagnostics.Activity.Current.TraceStateString);
                }
            }
            
            _logger.LogInformation("Sending request with correlation ID: {CorrelationId}", correlationId);
            
            try
            {
                using var response = await httpClient.SendAsync(request);
                
                // Check if server echoed back the correlation ID
                if (response.Headers.TryGetValues("X-Correlation-ID", out var serverCorrelationIds))
                {
                    _logger.LogInformation("Server echoed correlation ID: {ServerCorrelationId}", 
                        string.Join(", ", serverCorrelationIds));
                }
                
                _logger.LogInformation("Correlation ID propagation test completed with status {StatusCode}", 
                    response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Correlation ID propagation test failed for correlation ID: {CorrelationId}", 
                    correlationId);
            }
        }
    }
}