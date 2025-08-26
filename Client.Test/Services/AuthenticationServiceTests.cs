using Client.Services;
using Microsoft.Extensions.DependencyInjection;
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
    public class AuthenticationServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly AuthenticationService _authenticationService;

        public AuthenticationServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Default);
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _authenticationService = new AuthenticationService(_mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task Run_WithValidAuthentication_ExecutesSuccessfully()
        {
            // Arrange
            SetupSuccessfulResponse();

            // Act
            var exception = await Record.ExceptionAsync(() => _authenticationService.Run());

            // Assert
            Assert.Null(exception);
            VerifyHttpRequestMade(Times.AtLeastOnce());
        }

        [Fact]
        public async Task AuthenticateWithBearerToken_SetsAuthorizationHeader()
        {
            // Arrange
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            };

            HttpRequestMessage capturedRequest = null;
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(responseMessage);

            // Act
            await _authenticationService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(_httpClient.DefaultRequestHeaders.Authorization);
            Assert.Equal("Bearer", _httpClient.DefaultRequestHeaders.Authorization.Scheme);
        }

        [Fact]
        public async Task AuthenticateWithBasicAuth_HandlesUnauthorized()
        {
            // Arrange
            var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);

            _mockMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") })
                .ReturnsAsync(unauthorizedResponse)
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            // Act
            var exception = await Record.ExceptionAsync(() => _authenticationService.Run());

            // Assert
            Assert.Null(exception); // Should handle gracefully without throwing
        }

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task HandlesDifferentResponseCodes(HttpStatusCode statusCode)
        {
            // Arrange
            var responses = new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
                new HttpResponseMessage(statusCode),
                new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            _mockMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responses[0])
                .ReturnsAsync(responses[1])
                .ReturnsAsync(responses[2]);

            // Act
            var exception = await Record.ExceptionAsync(() => _authenticationService.Run());

            // Assert
            Assert.Null(exception); // Should handle all response codes gracefully
        }

        private void SetupSuccessfulResponse()
        {
            var responses = new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"new-token\"}") }
            };

            _mockMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responses[0])
                .ReturnsAsync(responses[1])
                .ReturnsAsync(responses[2]);
        }

        private void VerifyHttpRequestMade(Times times)
        {
            _mockMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    times,
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}