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
            var data = new List<ContactViewModel>();
            data.Add(new ContactViewModel() 
            {  
                Id = Guid.NewGuid(),
                Name = "Test",
                Address = "Test"
            });
            data.Add(new ContactViewModel()
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Address = "Test"
            });

           
            var httpResponseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            httpResponseMessage.Content = JsonContent.Create(data);
            return Task.FromResult(httpResponseMessage);
        }
    }
}
