using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
    public class AuthenticationService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;
        private string _accessToken;

        public AuthenticationService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await AuthenticateWithBearerToken();
            await GetProtectedResourceWithBasicAuth();
            await RefreshTokenExample();
        }

        private async Task AuthenticateWithBearerToken()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Simulate getting a bearer token
            _accessToken = "your-jwt-token-here";
            
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await httpClient.GetAsync("api/contacts");
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Successfully authenticated with Bearer token");
                var content = await response.Content.ReadAsStringAsync();
                var contacts = JsonConvert.DeserializeObject<List<ContactViewModel>>(content);
                Console.WriteLine($"Retrieved {contacts.Count} contacts");
            }
            else
            {
                Console.WriteLine($"Authentication failed: {response.StatusCode}");
            }
        }

        private async Task GetProtectedResourceWithBasicAuth()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");
            
            // Basic Authentication
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("username:password"));
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);

            try
            {
                var response = await httpClient.GetAsync("api/contacts/protected");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Successfully accessed protected resource with Basic Auth");
                Console.WriteLine($"Response: {content}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to access protected resource: {ex.Message}");
            }
        }

        private async Task RefreshTokenExample()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Simulate refresh token request
            var refreshTokenRequest = new
            {
                refresh_token = "your-refresh-token",
                grant_type = "refresh_token"
            };

            var json = JsonConvert.SerializeObject(refreshTokenRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("api/auth/refresh", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    
                    Console.WriteLine("Token refreshed successfully");
                    // Update the access token for future requests
                    _accessToken = tokenResponse?.access_token;
                }
                else
                {
                    Console.WriteLine($"Token refresh failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Token refresh error: {ex.Message}");
            }
        }
    }
}