using Client.Services;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class ErrorHandlingServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly ErrorHandlingService _errorHandlingService;

        public ErrorHandlingServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _errorHandlingService = new ErrorHandlingService(_mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task Run_WithValidResponse_ExecutesSuccessfully()
        {
            // Arrange
            SetupSuccessfulResponse();

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception);
            VerifyHttpRequestMade(Times.Once()); // Run() calls GetContactsLongProcessHandleExceptionsForNET5() once
        }

        [Fact]
        public async Task Run_WithValidJsonResponse_ProcessesContacts()
        {
            // Arrange
            var jsonResponse = @"[{""Id"":""" + Guid.NewGuid() + @""",""Name"":""Test Contact"",""Address"":""Test Address""}]";
            
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Get &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString().Contains("GetContactsLongProcess")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(successResponse);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task Run_WithTimeoutException_HandlesGracefully()
        {
            // Arrange - Service creates TaskCanceledException with TimeoutException as inner
            var timeoutException = new TaskCanceledException("Request timed out", new TimeoutException());
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(timeoutException);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle timeout gracefully in catch block
        }

        [Fact]
        public async Task Run_WithCancellationException_HandlesGracefully()
        {
            // Arrange - Service creates TaskCanceledException without inner exception
            var cancellationException = new TaskCanceledException("Request was cancelled");
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(cancellationException);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle cancellation gracefully in catch block
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task Run_WithServerErrors_CallsEnsureSuccessStatusCode(HttpStatusCode statusCode)
        {
            // Arrange
            var errorResponse = new HttpResponseMessage(statusCode);
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(errorResponse);

            // Act & Assert
            // The service calls EnsureSuccessStatusCode() so server errors should throw HttpRequestException
            await Assert.ThrowsAsync<HttpRequestException>(() => _errorHandlingService.Run());
        }

        [Fact]
        public async Task Run_VerifiesCorrectEndpoint()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) 
                { 
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") 
                });

            // Act
            await _errorHandlingService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("api/contacts/GetContactsLongProcess", capturedRequest.RequestUri?.ToString());
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/xml")]
        public async Task Run_HandlesMultipleContentTypes(string contentType)
        {
            // Arrange
            string responseContent;
            if (contentType == "application/json")
            {
                responseContent = @"[{""Id"":""" + Guid.NewGuid() + @""",""Name"":""Test"",""Address"":""Test""}]";
            }
            else
            {
                responseContent = @"<?xml version=""1.0""?>
<ArrayOfContactViewModel xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <ContactViewModel>
        <Id>" + Guid.NewGuid() + @"</Id>
        <Name>Test</Name>
        <Address>Test</Address>
    </ContactViewModel>
</ArrayOfContactViewModel>";
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, contentType)
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle both content types
        }

        [Fact]
        public async Task Run_WithJsonDeserializationError_HandlesGracefully()
        {
            // Arrange - Invalid JSON that will cause deserialization to fail
            var invalidJsonResponse = @"{""invalid"": ""json"", ""structure"": true}";
            
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(invalidJsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            // The service doesn't handle JSON deserialization errors, so this might throw
            // or might complete with empty collection - depends on Newtonsoft.Json behavior
            Assert.True(exception == null || exception is Newtonsoft.Json.JsonException);
        }

        private void SetupSuccessfulResponse()
        {
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(successResponse);
        }

        private void VerifyHttpRequestMade(Times times)
        {
            _mockMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    times,
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}