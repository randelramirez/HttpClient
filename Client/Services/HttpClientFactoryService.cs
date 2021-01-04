﻿using Core;
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
    public class HttpClientFactoryService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public HttpClientFactoryService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            Console.WriteLine("Running GetContactsWithHttpClientFromFactory...");
            await GetContactsWithHttpClientFromFactory();

            Console.WriteLine("Running GetContactsWithNamedHttpClientFromFactory...");
            await GetContactsWithNamedHttpClientFromFactory();
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

            using (var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead))
            {
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
        }

    }
}
