using Client.TypedClients;
using Core;
using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
    public class HttpClientFactoryManagementService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ContactsClient contactsClient;

        public HttpClientFactoryManagementService(IHttpClientFactory httpClientFactory, ContactsClient contactsClient)
        {
            this.httpClientFactory = httpClientFactory;

            // The HttpClient instances injected by DI, can be disposed of safely, because the associated HttpMessageHandler is managed by the factory. 
            this.contactsClient = contactsClient;
        }

        public async Task Run()
        {
            //await GetContactsWithHttpClientFromFactory();
            //await GetContactsWithNamedHttpClientFromFactory();
            await GetContactsWithTypedHttpClient();
        }

        public async Task GetContactsWithHttpClientFromFactory()
        {
            var httpClient = httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://localhost:44354/api/contacts");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStreamAsync();
            response.EnsureSuccessStatusCode();

            using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();
            
            var contacts = jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);
            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }

        private async Task GetContactsWithNamedHttpClientFromFactory()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/contacts");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStreamAsync();
            response.EnsureSuccessStatusCode();

            using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();

            var contacts = jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);
            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }

        private async Task GetContactsWithTypedHttpClient()
        {
            var contacts = await this.contactsClient.GetContacts();
            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }
    }
}
