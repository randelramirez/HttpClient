using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Services
{
    public class ConcurrencyService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public ConcurrencyService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await ParallelRequestsExample();
            await ConcurrentRequestsWithSemaphore();
            await BatchProcessingWithThrottling();
        }

        private async Task ParallelRequestsExample()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine("Starting parallel requests...");

            // Create multiple tasks that run in parallel
            var tasks = new List<Task<ContactViewModel>>();
            
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(GetContactAsync(httpClient, i));
            }

            try
            {
                var results = await Task.WhenAll(tasks);
                stopwatch.Stop();

                Console.WriteLine($"Completed {results.Length} parallel requests in {stopwatch.ElapsedMilliseconds}ms");
                
                foreach (var contact in results.Where(c => c != null))
                {
                    Console.WriteLine($"Retrieved: {contact.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parallel requests error: {ex.Message}");
            }
        }

        private async Task ConcurrentRequestsWithSemaphore()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            var semaphore = new SemaphoreSlim(3, 3); // Limit to 3 concurrent requests
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine("Starting concurrent requests with semaphore throttling...");

            var tasks = new List<Task>();
            
            for (int i = 0; i < 10; i++)
            {
                var taskId = i;
                tasks.Add(ProcessWithSemaphore(httpClient, semaphore, taskId));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"Completed throttled concurrent requests in {stopwatch.ElapsedMilliseconds}ms");
        }

        private async Task BatchProcessingWithThrottling()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            var batchSize = 3;
            var totalRequests = 9;
            
            Console.WriteLine("Starting batch processing with throttling...");

            for (int batch = 0; batch < totalRequests; batch += batchSize)
            {
                var batchTasks = new List<Task>();
                var currentBatchSize = Math.Min(batchSize, totalRequests - batch);

                Console.WriteLine($"Processing batch {(batch / batchSize) + 1}...");

                for (int i = 0; i < currentBatchSize; i++)
                {
                    var requestId = batch + i;
                    batchTasks.Add(ProcessBatchRequest(httpClient, requestId));
                }

                await Task.WhenAll(batchTasks);
                
                // Add delay between batches to avoid overwhelming the server
                if (batch + batchSize < totalRequests)
                {
                    await Task.Delay(1000);
                }
            }

            Console.WriteLine("Batch processing completed");
        }

        private async Task<ContactViewModel> GetContactAsync(HttpClient httpClient, int requestId)
        {
            try
            {
                Console.WriteLine($"Request {requestId} started");
                
                var response = await httpClient.GetAsync("api/contacts");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                    
                    Console.WriteLine($"Request {requestId} completed - got {contacts?.Count ?? 0} contacts");
                    return contacts?.FirstOrDefault();
                }
                else
                {
                    Console.WriteLine($"Request {requestId} failed with status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request {requestId} error: {ex.Message}");
                return null;
            }
        }

        private async Task ProcessWithSemaphore(HttpClient httpClient, SemaphoreSlim semaphore, int taskId)
        {
            await semaphore.WaitAsync();
            
            try
            {
                Console.WriteLine($"Task {taskId} acquired semaphore");
                
                var response = await httpClient.GetAsync("api/contacts");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Task {taskId} completed successfully");
                }
                else
                {
                    Console.WriteLine($"Task {taskId} failed: {response.StatusCode}");
                }

                // Simulate some processing time
                await Task.Delay(500);
            }
            finally
            {
                Console.WriteLine($"Task {taskId} releasing semaphore");
                semaphore.Release();
            }
        }

        private async Task ProcessBatchRequest(HttpClient httpClient, int requestId)
        {
            try
            {
                Console.WriteLine($"  Batch request {requestId} starting");
                
                var response = await httpClient.GetAsync($"api/contacts");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  Batch request {requestId} completed");
                }
                else
                {
                    Console.WriteLine($"  Batch request {requestId} failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Batch request {requestId} error: {ex.Message}");
            }
        }
    }
}