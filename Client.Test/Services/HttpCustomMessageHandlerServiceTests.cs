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
    public class HttpCustomMessageHandlerServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly HttpCustomMessageHandlerService _customHandlerService;

        public HttpCustomMessageHandlerServiceTests()
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
        public async Task Run_WithSuccessfulResponses_CompletesCustomHandlerDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _customHandlerService.Run();

            // Assert
            VerifyHttpClientFactoryCalls();
        }

        [Fact]
        public async Task CustomMessageHandler_WithLogging_InterceptsRequests()
        {
            // Arrange
            var requestsCaptured = 0;
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => 
                {
                    requestsCaptured++;
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            await _customHandlerService.Run();

            // Assert
            Assert.True(requestsCaptured > 0, "Custom handler should have intercepted requests");
        }

        [Fact]
        public async Task CustomHandler_WithRetryLogic_RetriesOnFailure()
        {
            // Arrange
            var attemptCount = 0;
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    attemptCount++;
                    if (attemptCount < 3)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                    });
                });

            // Act
            await _customHandlerService.Run();

            // Assert
            Console.WriteLine($"Attempt count: {attemptCount}");
            Assert.True(attemptCount >= 3, $"Custom handler should have retried the request on failure. Actual attempts: {attemptCount}");
        }

        [Fact]
        public async Task CustomHandler_WithErrorResponse_HandlesGracefully()
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
            var exception = await Record.ExceptionAsync(() => _customHandlerService.Run());

            // Assert - Service should handle HTTP errors gracefully or throw expected exceptions
            if (exception != null)
            {
                Assert.True(exception is HttpRequestException || exception.InnerException is HttpRequestException,
                    $"Expected HttpRequestException, but got: {exception.GetType().Name}");
            }
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_DoesNotThrowInConstructor()
        {
            // Act & Assert
            var exception = Record.Exception(() => new HttpCustomMessageHandlerService(null!));
            // HttpCustomMessageHandlerService doesn't validate in constructor
            Assert.Null(exception);
        }

        [Fact]
        public void HttpCustomMessageHandlerService_CanBeInstantiated()
        {
            // Act & Assert
            var service = new HttpCustomMessageHandlerService(_mockHttpClientFactory.Object);
            Assert.NotNull(service);
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

        private void VerifyHttpClientFactoryCalls()
        {
            _mockHttpClientFactory.Verify(f => f.CreateClient("ContactsClientCustomHandler"), Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}