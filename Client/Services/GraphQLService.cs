using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
    public class GraphQLService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public GraphQLService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await ExecuteGraphQLQuery();
            await ExecuteGraphQLMutation();
            await ExecuteGraphQLWithVariables();
        }

        private async Task ExecuteGraphQLQuery()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var graphqlQuery = new
            {
                query = @"
                    query GetContacts {
                        contacts {
                            id
                            name
                            address
                            email
                        }
                    }"
            };

            var jsonContent = JsonConvert.SerializeObject(graphqlQuery);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("api/graphql", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    Console.WriteLine("GraphQL query executed successfully");
                    Console.WriteLine($"Response: {responseContent}");
                    
                    if (result?.data?.contacts != null)
                    {
                        Console.WriteLine($"Retrieved {result.data.contacts.Count} contacts");
                    }
                }
                else
                {
                    Console.WriteLine($"GraphQL query failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"GraphQL query error: {ex.Message}");
            }
        }

        private async Task ExecuteGraphQLMutation()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var graphqlMutation = new
            {
                query = @"
                    mutation CreateContact($input: CreateContactInput!) {
                        createContact(input: $input) {
                            id
                            name
                            address
                            email
                        }
                    }",
                variables = new
                {
                    input = new
                    {
                        name = "GraphQL User",
                        address = "123 GraphQL Street",
                        email = "graphql@example.com"
                    }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(graphqlMutation);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("api/graphql", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    Console.WriteLine("GraphQL mutation executed successfully");
                    
                    if (result?.data?.createContact != null)
                    {
                        var contact = result.data.createContact;
                        Console.WriteLine($"Created contact: {contact.name} (ID: {contact.id})");
                    }
                    else if (result?.errors != null)
                    {
                        Console.WriteLine($"GraphQL errors: {JsonConvert.SerializeObject(result.errors)}");
                    }
                }
                else
                {
                    Console.WriteLine($"GraphQL mutation failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"GraphQL mutation error: {ex.Message}");
            }
        }

        private async Task ExecuteGraphQLWithVariables()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            var contactId = Guid.NewGuid();
            
            var graphqlQuery = new
            {
                query = @"
                    query GetContactById($id: ID!) {
                        contact(id: $id) {
                            id
                            name
                            address
                            email
                            createdAt
                            updatedAt
                        }
                    }",
                variables = new
                {
                    id = contactId.ToString()
                }
            };

            var jsonContent = JsonConvert.SerializeObject(graphqlQuery);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add GraphQL-specific headers
            var request = new HttpRequestMessage(HttpMethod.Post, "api/graphql")
            {
                Content = content
            };
            request.Headers.Add("GraphQL-Client", "HttpClientExample/1.0");

            try
            {
                var response = await httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    Console.WriteLine("GraphQL query with variables executed successfully");
                    
                    if (result?.data?.contact != null)
                    {
                        var contact = result.data.contact;
                        Console.WriteLine($"Found contact: {contact.name}");
                    }
                    else if (result?.errors != null)
                    {
                        Console.WriteLine("GraphQL returned errors:");
                        foreach (var error in result.errors)
                        {
                            Console.WriteLine($"  - {error.message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Contact with ID {contactId} not found");
                    }
                }
                else
                {
                    Console.WriteLine($"GraphQL query with variables failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"GraphQL query with variables error: {ex.Message}");
            }
        }
    }
}