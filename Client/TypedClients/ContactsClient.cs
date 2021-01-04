using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client.TypedClients
{
    /*
        A Typed Client is effectively a transient object, that means a new instance is created each time one is needed. 
        It receives a new HttpClient instance each time it's constructed. 
        However, the HttpMessageHandler objects in the pool are the objects that are reused by multiple HttpClient instances
     */
    public class ContactsClient
    {
        private readonly HttpClient client;

        // The HttpClient instances injected by DI, can be disposed of safely, because the associated HttpMessageHandler is managed by the factory. 
        public ContactsClient(HttpClient client)
        {
            this.client = client;
            this.client.BaseAddress = new Uri("https://localhost:44354/"); // new Uri(Configuration["BaseUrl"]);
            this.client.Timeout = new TimeSpan(0, 0, 30);
            this.client.DefaultRequestHeaders.Clear();

            // NOTE: WE DO NOT OVERRIDE THE default HttpClientHandler here so that we get the one from the pool
        }

        public async Task<IEnumerable<ContactViewModel>> GetContacts()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/contacts");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using var response = await client.SendAsync(request,
              HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStreamAsync();
            response.EnsureSuccessStatusCode();
            using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();

            return jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);
        }
    }
}
