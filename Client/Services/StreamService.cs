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
    public class StreamService : IService
    {

        private static HttpClient httpClient = new HttpClient(
            new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
            });

        public StreamService()
        {
            httpClient.BaseAddress = new Uri("https://localhost:44354/");
            httpClient.Timeout = new TimeSpan(0, 0, 30);
            httpClient.DefaultRequestHeaders.Clear();
        }

        public async Task Run()
        {
            //await GetContactsAsStream();
            //await GetContactsAsStreamWithCompletionMode();
            //await CreateContactUsingStreams();
            //await CreateAndReadContactUsingStreams();
            await GetContactWithGZipCompression();
        }

        public async Task GetContactAsStream(Guid contactId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/contacts/{contactId}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();

            using var streamReader = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();
            var contact = jsonSerializer.Deserialize<ContactViewModel>(jsonTextReader);

            Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
        }

        private async Task GetContactsAsStream()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/contacts/");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();

            using var streamReader = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();

            var contacts = jsonSerializer.Deserialize<List<ContactViewModel>>(jsonTextReader);

            // do something with the contacts   
            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }

        private async Task GetContactsAsStreamWithCompletionMode()
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

                // do something with the contacts   
                foreach (var contact in contacts)
                {
                    Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
                }
            }
        }

        private async Task<ContactViewModel> CreateContactUsingStreams()
        {

            var newContact = new Contact()
            {
                Name = $"New Contact Stream {DateTimeOffset.Now.ToUnixTimeMilliseconds()}",
                Address = $"Address created through stream {DateTimeOffset.Now.ToUnixTimeMilliseconds()}"
            };

            var memoryContentStream = new MemoryStream();

            using var streamWriter = new StreamWriter(memoryContentStream,
                new UTF8Encoding(), 1024, true);
            using var jsonTextWriter = new JsonTextWriter(streamWriter);

            var jsonSerializer = new JsonSerializer();
            jsonSerializer.Serialize(jsonTextWriter, newContact);
            jsonTextWriter.Flush();

            memoryContentStream.Seek(0, SeekOrigin.Begin);

            using var request = new HttpRequestMessage(
              HttpMethod.Post,
              $"api/contacts/");
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using var streamContent = new StreamContent(memoryContentStream);
            request.Content = streamContent;
            request.Content.Headers.ContentType =
              new MediaTypeHeaderValue("application/json");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var createdContent = await response.Content.ReadAsStringAsync();
            var createdContact = JsonConvert.DeserializeObject<ContactViewModel>(createdContent);
            Console.WriteLine($"Name: {createdContact.Name}, Address: {createdContact.Address}");
            return createdContact;
        }

        private async Task CreateAndReadContactUsingStreams()
        {
            var newContact = new Contact()
            {
                Name = $"New Contact Created Using Streams {DateTimeOffset.Now.ToUnixTimeMilliseconds()}",
                Address = $"Address Created Usings Streams {DateTimeOffset.Now.ToUnixTimeMilliseconds()}"
            };

            var jsonSerializer = new JsonSerializer();
            var memoryContentStream = new MemoryStream();

            using var streamWriter = new StreamWriter(memoryContentStream, new UTF8Encoding(), 1024, true);
            using var jsonTextWriter = new JsonTextWriter(streamWriter);
          
            jsonSerializer.Serialize(jsonTextWriter, newContact);
            jsonTextWriter.Flush();

            memoryContentStream.Seek(0, SeekOrigin.Begin);
            using var request = new HttpRequestMessage(
              HttpMethod.Post,
              $"api/contacts/");
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));


            using var streamContent = new StreamContent(memoryContentStream);
            request.Content = streamContent;
            request.Content.Headers.ContentType =
              new MediaTypeHeaderValue("application/json");

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync();

            using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
            using var jsonTextReader = new JsonTextReader(streamReader);

            var contact = jsonSerializer.Deserialize<ContactViewModel>(jsonTextReader);
            Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
        }

        private async Task GetContactWithGZipCompression()
        {
            var contact = await CreateContactUsingStreams();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/contacts/{contact.Id}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();

            using var streamReader = new StreamReader(stream, new UTF8Encoding(), true, 1024, false);
            using var jsonTextReader = new JsonTextReader(streamReader);
            var jsonSerializer = new JsonSerializer();
            Console.WriteLine("Reading response....");
            var createdContact = jsonSerializer.Deserialize<ContactViewModel>(jsonTextReader);
            Console.WriteLine($"Name: {createdContact.Name}, Address: {createdContact.Address}");
        }
    }
}
