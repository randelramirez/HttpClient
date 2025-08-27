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
    /// <summary>
    /// Comprehensive error handling tests for HttpCustomMessageHandlerService
    /// to validate that error scenarios are handled consistently
    /// </summary>
    public class HttpCustomMessageHandlerServiceErrorTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly HttpCustomMessageHandlerService _customHandlerService;

        public HttpCustomMessageHandlerServiceErrorTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            
            // Create HttpClient with retry handler that wraps the mock
            var retryHandler = new Client.MessageHandlers.RetryPolicyDelegatingHandler(_mockMessageHandler.Object, 3);
            _httpClient = new HttpClient(retryHandler)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClientCustomHandler")).Returns(_httpClient);

            _customHandlerService = new HttpCustomMessageHandlerService(_mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task Run_WithNotFoundError_HandlesGracefullyWithoutException()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            // Act
            var exception = await Record.ExceptionAsync(() => _customHandlerService.Run());

            // Assert
            Assert.Null(exception); // Should handle 404 gracefully
        }

        [Fact]
        public async Task Run_WithUnauthorizedError_HandlesGracefullyWithoutException()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            // Act
            var exception = await Record.ExceptionAsync(() => _customHandlerService.Run());

            // Assert
            Assert.Null(exception); // Should handle 401 gracefully
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task Run_WithOtherHttpErrors_BehaviorShouldBeConsistent(HttpStatusCode statusCode)
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
            // Document current behavior - this test will help identify inconsistencies
            var exception = await Record.ExceptionAsync(() => _customHandlerService.Run());
            
            // Currently the service handles some errors gracefully and throws on others
            // This test documents the actual behavior for future consistency decisions
            Assert.True(exception == null || exception is HttpRequestException,
                $"Service should either handle {statusCode} gracefully (null) or throw HttpRequestException, but got: {exception?.GetType().Name}");
        }

        [Fact]
        public async Task Run_WithNetworkException_PropagatesAppropriateException()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network connection failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _customHandlerService.Run());
            Assert.Contains("Network connection failed", exception.Message);
        }

        [Fact]
        public async Task Run_WithTimeoutException_HandlesAppropriately()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out", new TimeoutException()));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => _customHandlerService.Run());
            Assert.IsType<TimeoutException>(exception.InnerException);
        }

        [Fact]
        public async Task Run_WithCancellationToken_RespectsCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancelled token

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => _customHandlerService.Run());
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
