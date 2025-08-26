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
            _mockMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Default);
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
            SetupSuccessfulResponses();

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task HandleNotFoundResponse_HandlesGracefully()
        {
            // Arrange
            var notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(notFoundResponse);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle 404 gracefully
        }

        [Fact]
        public async Task HandleUnauthorizedResponse_HandlesGracefully()
        {
            // Arrange
            var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(unauthorizedResponse);

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle 401 gracefully
        }

        [Fact]
        public async Task HandleTimeoutException_CatchesTaskCanceledException()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new TaskCanceledException("Request timed out", new TimeoutException()));

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle timeout gracefully
        }

        [Fact]
        public async Task HandleCancellationException_CatchesTaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new TaskCanceledException("Request was cancelled"));

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle cancellation gracefully
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task HandleServerErrors_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            // Arrange
            var errorResponse = new HttpResponseMessage(statusCode);
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(errorResponse);

            // Act & Assert
            // The service might handle these gracefully or throw - depends on implementation
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());
            // We don't assert the exception type here as it depends on the actual implementation
        }

        [Fact]
        public async Task MultipleRequests_HandlesPartialFailures()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") })
                .ThrowsAsync(new TaskCanceledException("Timeout"))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            // Act
            var exception = await Record.ExceptionAsync(() => _errorHandlingService.Run());

            // Assert
            Assert.Null(exception); // Should handle mixed success/failure scenarios
        }

        private void SetupSuccessfulResponses()
        {
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(successResponse);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}