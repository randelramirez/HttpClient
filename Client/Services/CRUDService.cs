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
using System.Xml.Serialization;

namespace Client.Services
{
    public class CRUDService : IService
    {
        private static HttpClient httpClient = new HttpClient();

        public CRUDService()
        {
            httpClient.BaseAddress = new Uri("https://localhost:44354/");
            httpClient.Timeout = new TimeSpan(0, 0, 30);
            httpClient.DefaultRequestHeaders.Clear();
        }

        public async  Task Run()
        {
            //await GetContacts();
            //await GetContactsThroughHttpRequestMessage();
            //await CreateContact();
            await UpdateContact();
        }

        public async Task GetContacts()
        {
            var response = await httpClient.GetAsync("api/contacts");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var contacts = new List<ContactViewModel>();
            if (response.Content.Headers.ContentType.MediaType == "application/json")
            {
                contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
            }
            else if (response.Content.Headers.ContentType.MediaType == "application/xml")
            {
                var serializer = new XmlSerializer(typeof(List<ContactViewModel>));
                contacts = (List<ContactViewModel>)serializer.Deserialize(new StringReader(content));
            }

            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }

        public async Task GetContactsThroughHttpRequestMessage()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/contacts");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);

            foreach (var contact in contacts)
            {
                Console.WriteLine($"Name: {contact.Name}, Address: {contact.Address}");
            }
        }

        public async Task CreateContact()
        {
            var newContact = new Contact()
            {
                Name = $"Contact created through HttpClient {DateTimeOffset.UtcNow}",
                Address = $"Test Address {DateTimeOffset.UtcNow}"
            };

            var serializedMovieToCreate = JsonConvert.SerializeObject(newContact);

            var request = new HttpRequestMessage(HttpMethod.Post, "api/contacts");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            request.Content = new StringContent(serializedMovieToCreate);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var createdContact = JsonConvert.DeserializeObject<ContactViewModel>(content);
            Console.WriteLine($"Name: {createdContact.Name}, Address: {createdContact.Address}");

        }

        public async Task UpdateContact()
        {
            // Get an existing contact and update it
            var json = await httpClient.GetStringAsync("https://localhost:44354/api/contacts/5F4C8184-369C-4C62-FC73-08D8ACD73ED6");
            var contactToUpdateViewModel =  JsonConvert.DeserializeObject<ContactViewModel>(json);

            // assign the id of the retrieved contact
            var contactToUpdate = new Contact()
            {
               Id = contactToUpdateViewModel.Id,
               Name = $"Updated contact name! {DateTimeOffset.UtcNow}",
               Address = "Updated Address"
            };

            var serializedContactToUpdate = JsonConvert.SerializeObject(contactToUpdate);

            var request = new HttpRequestMessage(HttpMethod.Put,
                $"api/contacts/{contactToUpdate.Id}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(serializedContactToUpdate);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Console.WriteLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            //Console.WriteLine(content); content is empty 
        }
    }
}
