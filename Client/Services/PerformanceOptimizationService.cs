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
    /// Demonstrates Microsoft's recommended patterns for performance optimization with HttpClient.
    /// Based on Microsoft's performance best practices documentation.
    /// </summary>
    public class PerformanceOptimizationService : IService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PerformanceOptimizationService> _logger;

        public PerformanceOptimizationService(
            IHttpClientFactory httpClientFactory,
            ILogger<PerformanceOptimizationService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run()
        {
            _logger.LogInformation("Starting performance optimization patterns demonstration");

            await DemonstrateStreamingForLargePayloads();
            await DemonstrateConnectionPooling();
            await DemonstrateCompressionUsage();
            await DemonstrateKeepAliveOptimization();
            await DemonstrateConcurrentRequestsWithThrottling();
        }

        /// <summary>
        /// Demonstrates streaming for large payloads to avoid memory issues.
        /// Microsoft recommends this for large responses.
        /// </summary>
        private async Task DemonstrateStreamingForLargePayloads()
        {
            _logger.LogInformation("=== Streaming for Large Payloads ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Use ResponseHeadersRead to start processing before entire response is downloaded
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            // Process stream directly instead of loading into memory
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);
            
            var serializer = new JsonSerializer();
            var contacts = serializer.Deserialize<List<ContactViewModel>>(jsonReader);
            
            _logger.LogInformation("Streamed {Count} contacts without loading entire response into memory", 
                contacts?.Count ?? 0);
        }

        /// <summary>
        /// Demonstrates proper connection pooling configuration.
        /// </summary>
        private async Task DemonstrateConnectionPooling()
        {
            _logger.LogInformation("=== Connection Pooling Optimization ===");
            
            // Make multiple requests to demonstrate connection reuse
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(httpClient.GetAsync("api/contacts"));
            }
            
            var responses = await Task.WhenAll(tasks);
            
            _logger.LogInformation("Completed {Count} concurrent requests using connection pooling", responses.Length);
            
            // Dispose responses
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        /// <summary>
        /// Demonstrates proper usage of compression for bandwidth optimization.
        /// </summary>
        private async Task DemonstrateCompressionUsage()
        {
            _logger.LogInformation("=== Compression Usage ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
            
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var contentEncoding = response.Content.Headers.ContentEncoding;
            _logger.LogInformation("Response received with encoding: {Encoding}", 
                string.Join(", ", contentEncoding));
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received {Size} characters with compression", content.Length);
        }

        /// <summary>
        /// Demonstrates Keep-Alive optimization for reducing connection overhead.
        /// </summary>
        private async Task DemonstrateKeepAliveOptimization()
        {
            _logger.LogInformation("=== Keep-Alive Optimization ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            // Make sequential requests to demonstrate connection reuse
            var startTime = DateTime.UtcNow;
            
            for (int i = 0; i < 3; i++)
            {
                using var response = await httpClient.GetAsync("api/contacts");
                _logger.LogInformation("Request {Number} completed with status {StatusCode}", 
                    i + 1, response.StatusCode);
            }
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Completed 3 sequential requests in {Duration}ms using Keep-Alive", 
                duration.TotalMilliseconds);
        }

        /// <summary>
        /// Demonstrates concurrent requests with proper throttling to avoid overwhelming the server.
        /// </summary>
        private async Task DemonstrateConcurrentRequestsWithThrottling()
        {
            _logger.LogInformation("=== Concurrent Requests with Throttling ===");
            
            var httpClient = _httpClientFactory.CreateClient("ContactsClient");
            
            // Use SemaphoreSlim to limit concurrent requests
            var semaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent requests
            var tasks = new List<Task>();
            
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(ProcessRequestWithThrottling(httpClient, semaphore, i));
            }
            
            await Task.WhenAll(tasks);
            
            _logger.LogInformation("Completed 10 requests with throttling (max 3 concurrent)");
        }

        private async Task ProcessRequestWithThrottling(HttpClient httpClient, SemaphoreSlim semaphore, int requestId)
        {
            await semaphore.WaitAsync();
            
            try
            {
                _logger.LogInformation("Starting throttled request {RequestId}", requestId);
                
                using var response = await httpClient.GetAsync("api/contacts");
                response.EnsureSuccessStatusCode();
                
                _logger.LogInformation("Completed throttled request {RequestId} with status {StatusCode}", 
                    requestId, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Throttled request {RequestId} failed", requestId);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}