using Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
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

            var httpResponseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(data)
            };
            
            return Task.FromResult(httpResponseMessage);
        }
    }
}
