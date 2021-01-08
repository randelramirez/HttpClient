using Client.Test.HandlersStub;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test
{
    public class SampleServiceTest
    {

        [Fact]
        public void GetContactsAsStream_On401Response_MustThrowUnauthorizedApiAccessException()
        {
            var httpClient = new HttpClient(new Return401UnauthorizedResponseHandler());
            var testableClass = new SampleService(httpClient);

            //var cancellationTokenSource = new CancellationTokenSource();

            Assert.ThrowsAsync<UnauthorizedApiAccessException>(
                () => testableClass.GetContactsAsStream());
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
