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
    public class ContactsClient
    {
        private readonly HttpClient client;

        public ContactsClient(HttpClient client)
        {
            this.client = client;
            this.client.BaseAddress = new Uri("https://localhost:44354/");
            this.client.Timeout = new TimeSpan(0, 0, 30);
            this.client.DefaultRequestHeaders.Clear();
        }

        public async Task<IEnumerable<ContactViewModel>> GetContacts()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/movies");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using (var response = await client.SendAsync(request,
              HttpCompletionOption.ResponseHeadersRead))
            {
                var stream = await response.Content.ReadAsStreamAsync();
                response.EnsureSuccessStatusCode();
                using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
                using var jsonTextReader = new JsonTextReader(streamReader);
                var jsonSerializer = new JsonSerializer();

                return jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);
            }
        }
    }
}
