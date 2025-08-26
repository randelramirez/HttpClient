using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Client.Services
{
    public class WebhookService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private const string WebhookSecret = "your-webhook-secret-key";

        public WebhookService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await SendWebhookWithSignature();
            await SendWebhookWithRetry();
            await SendBatchWebhooks();
        }

        private async Task SendWebhookWithSignature()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var webhookPayload = new
            {
                event_type = "contact.created",
                timestamp = DateTimeOffset.UtcNow,
                data = new
                {
                    id = Guid.NewGuid(),
                    name = "John Doe",
                    email = "john.doe@example.com"
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(webhookPayload);
            var signature = GenerateSignature(jsonPayload, WebhookSecret);

            var request = new HttpRequestMessage(HttpMethod.Post, "api/webhooks/receive");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            
            // Add signature header for webhook verification
            request.Headers.Add("X-Webhook-Signature", signature);
            request.Headers.Add("X-Webhook-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            try
            {
                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Webhook with signature sent successfully");
                }
                else
                {
                    Console.WriteLine($"Webhook failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Webhook error: {ex.Message}");
            }
        }

        private async Task SendWebhookWithRetry()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            var maxRetries = 3;
            var retryDelay = TimeSpan.FromSeconds(2);

            var webhookPayload = new
            {
                event_type = "contact.updated",
                timestamp = DateTimeOffset.UtcNow,
                retry_attempt = 0,
                data = new
                {
                    id = Guid.NewGuid(),
                    name = "Jane Smith",
                    email = "jane.smith@example.com"
                }
            };

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var payloadWithAttempt = new
                    {
                        webhookPayload.event_type,
                        webhookPayload.timestamp,
                        retry_attempt = attempt + 1,
                        webhookPayload.data
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payloadWithAttempt);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("api/webhooks/receive-with-retry", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Webhook with retry sent successfully on attempt {attempt + 1}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"Webhook attempt {attempt + 1} failed: {response.StatusCode}");
                        
                        if (attempt < maxRetries - 1)
                        {
                            Console.WriteLine($"Retrying in {retryDelay.TotalSeconds} seconds...");
                            await Task.Delay(retryDelay);
                            retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 1.5); // Exponential backoff
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Webhook attempt {attempt + 1} error: {ex.Message}");
                    
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(retryDelay);
                        retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 1.5);
                    }
                }
            }

            Console.WriteLine("Webhook failed after all retry attempts");
        }

        private async Task SendBatchWebhooks()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var batchPayload = new
            {
                batch_id = Guid.NewGuid(),
                timestamp = DateTimeOffset.UtcNow,
                events = new object[]
                {
                    new
                    {
                        event_type = "contact.created",
                        data = new { id = Guid.NewGuid(), name = "Alice Johnson" }
                    },
                    new
                    {
                        event_type = "contact.updated", 
                        data = new { id = Guid.NewGuid(), name = "Bob Wilson" }
                    },
                    new
                    {
                        event_type = "contact.deleted",
                        data = new { id = Guid.NewGuid() }
                    }
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(batchPayload);
            var signature = GenerateSignature(jsonPayload, WebhookSecret);

            var request = new HttpRequestMessage(HttpMethod.Post, "api/webhooks/batch");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Signature", signature);
            request.Headers.Add("X-Batch-Size", batchPayload.events.Length.ToString());

            try
            {
                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Batch webhook sent successfully with {batchPayload.events.Length} events");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response: {responseContent}");
                }
                else
                {
                    Console.WriteLine($"Batch webhook failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Batch webhook error: {ex.Message}");
            }
        }

        private string GenerateSignature(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }
}