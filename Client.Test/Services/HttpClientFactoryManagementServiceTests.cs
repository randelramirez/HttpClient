using Client.Services;
using Client.TypedClients;
using Core.ViewModels;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class HttpClientFactoryManagementServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly ContactsClient _contactsClient;
        private readonly HttpClientFactoryManagementService _factoryManagementService;

        public HttpClientFactoryManagementServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            // Create a real ContactsClient with the mocked HttpClient
            _contactsClient = new ContactsClient(_httpClient);

            _factoryManagementService = new HttpClientFactoryManagementService(_mockHttpClientFactory.Object, _contactsClient);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesFactoryDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _factoryManagementService.Run();

            // Assert
            // Verify that HttpClient was used (indirectly through the service)
            _mockMessageHandler.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetContactsWithTypedHttpClient_WithSuccessfulResponse_ReturnsContacts()
        {
            // Arrange
            var contactsJson = @"[{""Id"":""11111111-1111-1111-1111-111111111111"",""Name"":""Test Contact"",""Address"":""Test Address""}]";
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(contactsJson, System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            var contacts = await _contactsClient.GetContacts();

            // Assert
            Assert.NotNull(contacts);
            var contactsList = new List<ContactViewModel>(contacts);
            Assert.Single(contactsList);
            Assert.Equal("Test Contact", contactsList[0].Name);
            Assert.Equal("Test Address", contactsList[0].Address);
        }

        [Fact]
        public async Task FactoryManagement_WithDifferentClientTypes_CreatesCorrectClients()
        {
            // Arrange
            var requestsReceived = new List<HttpRequestMessage>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => requestsReceived.Add(request))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            await _factoryManagementService.Run();

            // Assert
            Assert.NotEmpty(requestsReceived);
            // Verify that requests were made (HttpClientFactoryManagementService.Run() calls GetContactsWithTypedHttpClient)
            var typedClientRequest = requestsReceived.Find(r => r.RequestUri?.ToString().Contains("api/contacts") == true);
            Assert.NotNull(typedClientRequest);
        }

        [Fact]
        public async Task TypedClient_WithNetworkError_HandlesGracefully()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => _contactsClient.GetContacts());
            Assert.IsType<HttpRequestException>(exception);
        }

        [Fact]
        public async Task FactoryService_WithErrorResponse_HandlesAppropriately()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Act
            var exception = await Record.ExceptionAsync(() => _factoryManagementService.Run());

            // Assert
            // The service should propagate the HTTP error (EnsureSuccessStatusCode throws)
            Assert.NotNull(exception);
            Assert.True(exception is HttpRequestException || exception.InnerException is HttpRequestException);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.BadRequest)]
        public async Task TypedClient_WithErrorStatusCodes_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _contactsClient.GetContacts());
            Assert.NotNull(exception);
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_DoesNotThrowInConstructor()
        {
            // Act & Assert
            var exception = Record.Exception(() => new HttpClientFactoryManagementService(null!, _contactsClient));
            // HttpClientFactoryManagementService doesn't validate in constructor
            Assert.Null(exception);
        }

        [Fact]
        public void Constructor_WithNullContactsClient_DoesNotThrowInConstructor()
        {
            // Act & Assert
            var exception = Record.Exception(() => new HttpClientFactoryManagementService(_mockHttpClientFactory.Object, null!));
            // HttpClientFactoryManagementService doesn't validate in constructor
            Assert.Null(exception);
        }

        [Fact]
        public void HttpClientFactoryManagementService_CanBeInstantiated()
        {
            // Act & Assert
            var service = new HttpClientFactoryManagementService(_mockHttpClientFactory.Object, _contactsClient);
            Assert.NotNull(service);
        }

        [Fact]
        public async Task ContactsClient_RequestConfiguration_SetsCorrectHeaders()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            await _contactsClient.GetContacts();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains(capturedRequest.Headers.Accept, h => h.MediaType == "application/json");
            Assert.Contains(capturedRequest.Headers.AcceptEncoding, h => h.Value == "gzip");
        }

        private void SetupSuccessfulResponses()
        {
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                });
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}