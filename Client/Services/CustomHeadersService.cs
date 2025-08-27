using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Client.Services
{
    public class CustomHeadersService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public CustomHeadersService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await RequestWithCustomHeaders();
            await RequestWithUserAgent();
            await RequestWithCacheControl();
            await RequestWithConditionalHeaders();
        }

        private async Task RequestWithCustomHeaders()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Add custom headers
            request.Headers.Add("X-Custom-Header", "MyCustomValue");
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
            request.Headers.Add("X-Client-ID", "HttpClientDemo");
            request.Headers.Add("X-Request-Source", "CustomHeadersService");
            request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString()); // Add correlation ID for tracking
            request.Headers.Add("X-API-Version", "1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Request with custom headers successful");
                    
                    // Display response headers
                    Console.WriteLine("Response headers:");
                    foreach (var header in response.Headers)
                    {
                        Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                    }
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                    Console.WriteLine($"Retrieved {contacts?.Count ?? 0} contacts");
                }
                else
                {
                    Console.WriteLine($"Custom headers request failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Custom headers request error: {ex.Message}");
            }
        }

        private async Task RequestWithUserAgent()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Set custom User-Agent
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HttpClientDemo/1.0 (.NET 9.0)");
            
            // Add standard headers for consistency
            httpClient.DefaultRequestHeaders.Add("X-API-Version", "1.0");
            httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());

            try
            {
                var response = await httpClient.GetAsync("api/contacts");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Request with custom User-Agent successful");
                    var content = await response.Content.ReadAsStringAsync();
                    var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                    Console.WriteLine($"Retrieved {contacts?.Count ?? 0} contacts");
                }
                else
                {
                    Console.WriteLine($"User-Agent request failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"User-Agent request error: {ex.Message}");
            }
        }

        private async Task RequestWithCacheControl()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            
            // Set cache control headers
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MaxAge = TimeSpan.Zero
            };

            request.Headers.IfModifiedSince = DateTimeOffset.UtcNow.AddMinutes(-1);
            
            // Add standard headers for consistency
            request.Headers.Add("X-API-Version", "1.0");
            request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await httpClient.SendAsync(request);
                
                Console.WriteLine($"Cache control request status: {response.StatusCode}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    Console.WriteLine("Content not modified since last request");
                }
                else if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Fresh content received");
                    var content = await response.Content.ReadAsStringAsync();
                    var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                    Console.WriteLine($"Retrieved {contacts?.Count ?? 0} contacts");
                }
                else
                {
                    Console.WriteLine($"Cache control request failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Cache control request error: {ex.Message}");
            }
        }

        private async Task RequestWithConditionalHeaders()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // First request to get ETag
            var initialRequest = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            initialRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var initialResponse = await httpClient.SendAsync(initialRequest);
            
            if (!initialResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to get initial response for conditional headers");
                return;
            }

            var etag = initialResponse.Headers.ETag;
            
            if (etag != null)
            {
                Console.WriteLine($"Got ETag: {etag.Tag}");
                
                // Make conditional request with If-None-Match
                var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
                conditionalRequest.Headers.IfNoneMatch.Add(etag);
                
                // Add standard headers for consistency
                conditionalRequest.Headers.Add("X-API-Version", "1.0");
                conditionalRequest.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
                conditionalRequest.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
                conditionalRequest.Headers.Add("X-Client-ID", "HttpClientDemo");
                conditionalRequest.Headers.Add("X-Request-Source", "CustomHeadersService");
                conditionalRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                try
                {
                    var conditionalResponse = await httpClient.SendAsync(conditionalRequest);
                    
                    if (conditionalResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        Console.WriteLine("Conditional request: Content not modified (304)");
                    }
                    else if (conditionalResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Conditional request: Content was modified, got fresh data");
                        var content = await conditionalResponse.Content.ReadAsStringAsync();
                        var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                        Console.WriteLine($"Retrieved {contacts?.Count ?? 0} contacts");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Conditional request error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("No ETag received in initial response");
            }
        }
    }
}