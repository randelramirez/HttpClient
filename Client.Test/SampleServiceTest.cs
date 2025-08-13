using Client.Test.HandlersStub;
using Moq;
using Moq.Protected;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test
{
    // Note for the following unit tests, we can actually replace the handler stubs as using Moq objects
    public class SampleServiceTest
    {
        [Fact]
        public async Task GetContactsAsStream_On401Response_MustThrowUnauthorizedApiAccessException()
        {
            var httpClient = new HttpClient(new Return401UnauthorizedResponseHandler());
            var testableClass = new SampleService(httpClient);

            //var cancellationTokenSource = new CancellationTokenSource();

            await Assert.ThrowsAsync<UnauthorizedApiAccessException>(
                () => testableClass.GetContactsAsStream());
        }

        [Fact]
        public async Task GetContactsAsStream_On401Response_MustThrowUnauthorizedApiAccessException_WithMoq()
        {
            var unauthorizedResponseHttpMessageHandlerMock = new Mock<HttpMessageHandler>();
            // so we can setup a protected method named SendAsync inside HttpMessageHandler
            unauthorizedResponseHttpMessageHandlerMock.Protected()
                  .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               ).ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.Unauthorized
               });
            var httpClient = new HttpClient(unauthorizedResponseHttpMessageHandlerMock.Object);
            var testableClass = new SampleService(httpClient);

            await Assert.ThrowsAsync<UnauthorizedApiAccessException>(() 
                => testableClass.GetContactsAsStream());
        }

        [Fact]
        public async Task GetContactsAsStream_On200Response_ReturnsCorrectNumberOfContacts()
        {
            var httpClient = new HttpClient(new Return200OkResponseHandler());
            var testableClass = new SampleService(httpClient);

            //var cancellationTokenSource = new CancellationTokenSource();
            var data = await testableClass.GetContactsAsStream();
            Assert.Equal(2, data.Count());
        }
    }
}
