using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

using System.Threading.Tasks;

namespace Client.Services
{
    public class ErrorHandlingService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public ErrorHandlingService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await GetContactAndDealWithInvalidResponses();
        }

        private async Task GetContactAndDealWithInvalidResponses()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // we are passing a non-existent ID
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/contacts/030a43b0-f9a5-405a-811c-bf342524b2be");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using (var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead))
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

                var stream = await response.Content.ReadAsStreamAsync();
                using var streamReader = new StreamReader(stream);
                using var jsonTextReader = new JsonTextReader(streamReader);
                var jsonSerializer = new JsonSerializer();
                var contact = jsonSerializer.Deserialize<ContactViewModel>(jsonTextReader);

                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");

            }
        }
    }
}
