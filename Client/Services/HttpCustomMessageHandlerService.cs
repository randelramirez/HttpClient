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

            using (var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    // inspect the status code
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // show this to the user
                        Console.WriteLine("The requested contact cannot be found.");
                        return;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // trigger a login flow
                        return;
                    }
                    response.EnsureSuccessStatusCode();
                }

                var content = await response.Content.ReadAsStringAsync();
                ContactViewModel contactViewModel = default;
                if (response.Content.Headers.ContentType.MediaType == "application/json")
                {
                    contactViewModel = JsonConvert.DeserializeObject<ContactViewModel>(content);
                }

                Console.WriteLine($"Name: {contactViewModel.Name}, Address: {contactViewModel.Address}");
            }
        }

        private async Task<ContactViewModel> GetContacts()
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
            if (response.Content.Headers.ContentType.MediaType == "application/json")
            {
                contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
            }

            return contacts.FirstOrDefault();
        }
    }
}
