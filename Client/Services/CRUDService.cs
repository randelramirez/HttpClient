using Core;
using Core.ViewModels;
using Microsoft.AspNetCore.JsonPatch;
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
            //await UpdateContact();
            //await DeleteContact();
            //await PatchContactThroughHttpRequestMessage();
            await PatchContact();
        }

        public async Task<ContactViewModel> GetContact(Guid contactId)
        {
            var response = await httpClient.GetAsync($"api/contacts/{contactId}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            ContactViewModel contact = default;
            if (response.Content.Headers.ContentType.MediaType == "application/json")
            {
                contact = JsonConvert.DeserializeObject<ContactViewModel>(content);
            }
            else if (response.Content.Headers.ContentType.MediaType == "application/xml")
            {
                var serializer = new XmlSerializer(typeof(List<ContactViewModel>));
                contact = (ContactViewModel)serializer.Deserialize(new StringReader(content));
            }
            return contact;
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

        public async Task<ContactViewModel> CreateContact()
        {
            var newContact = new Contact()
            {
                Name = $"New Name {DateTimeOffset.UtcNow}",
                Address = $"New Address {DateTimeOffset.UtcNow}"
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
            return createdContact;

        }

        public async Task UpdateContact()
        {
            // create a new contact and then update it
            var contactToUpdateViewModel = await CreateContact();
            Console.WriteLine($"Name before update: {contactToUpdateViewModel.Name}, Address: {contactToUpdateViewModel.Address}");

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


            var updatedContact = await GetContact(contactToUpdateViewModel.Id);
            Console.WriteLine($"Name after update: {updatedContact.Name}, Address after update: {updatedContact.Address}");

        }

        private async Task DeleteContact()
        {
            // Create a new one
            var contact = await CreateContact();
            Console.WriteLine($"Deleting contact... Id: {contact.Id}, Name: {contact.Name}, Address: {contact.Address}");
            // Delete the newly created

            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"api/contacts/{contact.Id}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Console.WriteLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            //Console.WriteLine(content); content is empty 
        }


        public async Task PatchContactThroughHttpRequestMessage()
        {
            // create a contact and then update it
            var contact = await CreateContact();

            var patchDoc = new JsonPatchDocument<Contact>();

            patchDoc.Replace(c => c.Name, "Updated Name with Patch");
            patchDoc.Remove(c => c.Address);

            var serializedChangeSet = JsonConvert.SerializeObject(patchDoc);

            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/contacts/{contact.Id}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(serializedChangeSet);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json-patch+json");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Console.WriteLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
            var patchedContact = await GetContact(contact.Id);
            Console.WriteLine($"Name after patch: {patchedContact.Name}, Address after patch: {patchedContact.Address}");
        }

        public async Task PatchContact()
        {
            // create a contact and then update it
            var contact = await CreateContact();

            var patchDoc = new JsonPatchDocument<Contact>();
            patchDoc.Replace(c => c.Name, "Updated Name");
            patchDoc.Remove(c => c.Address);

            var response = await httpClient.PatchAsync(
               $"api/contacts/{contact.Id}",
               new StringContent(
                   JsonConvert.SerializeObject(patchDoc),
                  Encoding.UTF8,
                   "application/json-patch+json"));

            response.EnsureSuccessStatusCode();
            //var content = await response.Content.ReadAsStringAsync(); // content is emptu, we are returning 204

            Console.WriteLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
            var patchedContact = await GetContact(contact.Id);
            Console.WriteLine($"Name after patch: {patchedContact.Name}, Address after patch: {patchedContact.Address}");
        }
    }
}
