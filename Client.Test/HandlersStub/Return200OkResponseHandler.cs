using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Test.HandlersStub
{
    public class Return200OkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Using C# 12/13 collection expression syntax for cleaner code
            List<ContactViewModel> data = 
            [
                new ContactViewModel() 
                {  
                    Id = Guid.NewGuid(),
                    Name = "Test",
                    Address = "Test"
                },
                new ContactViewModel()
                {
                    Id = Guid.NewGuid(),
                    Name = "Test",
                    Address = "Test"
                }
            ];

            // Use Newtonsoft.Json to serialize the data to match what SampleService expects
            var json = JsonConvert.SerializeObject(data);
            var httpResponseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            return Task.FromResult(httpResponseMessage);
        }
    }
}
