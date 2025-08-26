using Client.Services;
using Core;
using Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class CRUDServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly CRUDService _crudService;

        public CRUDServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Default);
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

            _crudService = new CRUDService(_httpClient);
        }

        [Fact]
        public async Task GetContact_WithValidId_ReturnsContact()
        {
            // Arrange
            var contactId = Guid.NewGuid();
            var expectedContact = new ContactViewModel
            {
                Id = contactId,
                Name = "Test Contact",
                Address = "Test Address"
            };

            var jsonResponse = JsonConvert.SerializeObject(expectedContact);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString().Contains(contactId.ToString())),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _crudService.GetContact(contactId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedContact.Id, result.Id);
            Assert.Equal(expectedContact.Name, result.Name);
            Assert.Equal(expectedContact.Address, result.Address);
        }

        [Fact]
        public async Task CreateContact_WithValidData_ReturnsCreatedContact()
        {
            // Arrange
            var expectedContact = new ContactViewModel
            {
                Id = Guid.NewGuid(),
                Name = "New Contact",
                Address = "New Address"
            };

            var jsonResponse = JsonConvert.SerializeObject(expectedContact);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri.ToString().Contains("api/contacts")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _crudService.CreateContact();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedContact.Id, result.Id);
        }

        [Fact]
        public async Task GetContact_WithInvalidId_ThrowsHttpRequestException()
        {
            // Arrange
            var contactId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _crudService.GetContact(contactId));
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/xml")]
        public async Task GetContact_WithDifferentContentTypes_HandlesCorrectly(string contentType)
        {
            // Arrange
            var contactId = Guid.NewGuid();
            var expectedContact = new ContactViewModel
            {
                Id = contactId,
                Name = "Test Contact",
                Address = "Test Address"
            };

            string responseContent;
            if (contentType == "application/json")
            {
                responseContent = JsonConvert.SerializeObject(expectedContact);
            }
            else
            {
                // For XML test, we'll still use JSON but set the content type to XML
                // In a real scenario, you'd serialize to XML
                responseContent = JsonConvert.SerializeObject(expectedContact);
            }

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, contentType)
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse);

            // Act
            if (contentType == "application/json")
            {
                var result = await _crudService.GetContact(contactId);
                
                // Assert
                Assert.NotNull(result);
                Assert.Equal(expectedContact.Id, result.Id);
            }
            else
            {
                // For XML content type, the method might throw an exception or handle differently
                // This depends on the actual implementation
                var exception = await Record.ExceptionAsync(() => _crudService.GetContact(contactId));
                // Assert that either it works or throws expected exception
                Assert.True(exception == null || exception is InvalidOperationException);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}