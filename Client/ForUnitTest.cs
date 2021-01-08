using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class ForUnitTest
    {
        private readonly HttpClient httpClient;

        public ForUnitTest(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<IEnumerable<ContactViewModel>> GetContactsAsStream()
        {
            var request = new HttpRequestMessage(
              HttpMethod.Get,
              "api/contacts/");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();

                using var streamReader = new StreamReader(stream);
                using var jsonTextReader = new JsonTextReader(streamReader);
                var jsonSerializer = new JsonSerializer();

                var contacts = jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);
                return contacts;
            }
        }
    }
}
