using Client.MessageHandlers;
using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Services
{
    public class HttpCustomMessageHandlerService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private CancellationTokenSource cancellationTokenSource =
            new CancellationTokenSource();

        private static HttpClient notSoNicelyInstantiatedHttpClient =
           new HttpClient(
               new RetryPolicyDelegatingHandler(
                   new HttpClientHandler()
                   { AutomaticDecompression = System.Net.DecompressionMethods.GZip },
                   2));


        public HttpCustomMessageHandlerService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await GetContactsithRetryPolicy(this.cancellationTokenSource.Token);
        }

        public async Task GetContactsithRetryPolicy(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting GetContactsWithRetryPolicy");
            //var contact = await GetContacts();
            var httpClient = httpClientFactory.CreateClient("ContactsClientCustomHandler");

            //var request = new HttpRequestMessage(
            //    HttpMethod.Get,
            //    $"api/contacts/{contact.Id}");

            // Invalid Contact Id
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/contacts/{Guid.NewGuid()}");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            Console.WriteLine("About to send request");
            using (var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken))
            {
                Console.WriteLine($"Received response with status: {response.StatusCode}");
                // Let the retry handler deal with failures - we just process the final response
                Console.WriteLine($"Received response with status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    ContactViewModel? contactViewModel = null;
                    if (response.Content.Headers.ContentType?.MediaType == "application/json" && !string.IsNullOrWhiteSpace(content))
                    {
                        // Handle both single contact and array responses
                        if (content.Trim().StartsWith("["))
                        {
                            // Array response - take first contact or null
                            var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                            contactViewModel = contacts?.FirstOrDefault();
                        }
                        else
                        {
                            // Single contact response
                            contactViewModel = JsonConvert.DeserializeObject<ContactViewModel>(content);
                        }
                    }

                    if (contactViewModel != null)
                    {
                        Console.WriteLine($"Name: {contactViewModel.Name}, Address: {contactViewModel.Address}");
                    }
                    else
                    {
                        Console.WriteLine("No contact data received.");
                    }
                }
                else
                {
                    // Handle specific error cases
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("The requested contact cannot be found.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("Unauthorized access - login required.");
                    }
                    else
                    {
                        Console.WriteLine($"Request failed with status: {response.StatusCode}");
                        // For testing purposes, let retry handlers deal with failures
                        // In production, you might want to throw or handle differently
                    }
                }
            }
        }

        private async Task<ContactViewModel?> GetContacts()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/contacts/");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var contacts = new List<ContactViewModel>();
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content) ?? new List<ContactViewModel>();
            }

            return contacts.FirstOrDefault();
        }
    }
}
