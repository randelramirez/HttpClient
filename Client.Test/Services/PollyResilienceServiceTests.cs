using Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public class PollyResilienceServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<PollyResilienceService>> _mockLogger;
        private readonly PollyResilienceService _pollyResilienceService;

        public PollyResilienceServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("PollyClient")).Returns(_httpClient);
            _mockHttpClientFactory.Setup(f => f.CreateClient("ProductionClient")).Returns(_httpClient);
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _mockLogger = new Mock<ILogger<PollyResilienceService>>();

            _pollyResilienceService = new PollyResilienceService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesAllDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            var exception = await Record.ExceptionAsync(() => _pollyResilienceService.Run());

            // Assert
            Assert.Null(exception);
            VerifyHttpClientFactoryCalls();
            VerifySuccessfulLogging();
        }

        [Fact]
        public async Task PollyIntegratedClient_WithSuccessfulResponse_LogsSuccess()
        {
            // Arrange
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

            // Act
            await _pollyResilienceService.Run();

            // Assert
            VerifyLogContains(LogLevel.Information, "SUCCESS: Retrieved");
        }

        [Fact]
        public async Task PollyIntegratedClient_WithHttpRequestException_HandlesGracefully()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var exception = await Record.ExceptionAsync(() => _pollyResilienceService.Run());

            // Assert
            Assert.Null(exception); // Should handle gracefully
            VerifyLogContains(LogLevel.Error, "Request failed even after Polly policies");
        }

        [Fact]
        public async Task ProductionReadyClient_WithSuccessfulResponse_LogsProductionSuccess()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _pollyResilienceService.Run();

            // Assert
            VerifyLogContains(LogLevel.Information, "Production client SUCCESS");
        }

        [Fact]
        public async Task BasicVsPollyComparison_WithSuccessfulResponses_LogsBothClients()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _pollyResilienceService.Run();

            // Assert
            VerifyLogContains(LogLevel.Information, "Basic client succeeded");
            VerifyLogContains(LogLevel.Information, "Polly client succeeded");
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        public async Task PollyClient_WithTransientErrors_HandlesGracefully(HttpStatusCode errorStatusCode)
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(errorStatusCode));

            // Act
            var exception = await Record.ExceptionAsync(() => _pollyResilienceService.Run());

            // Assert
            Assert.Null(exception); // Polly should handle transient errors gracefully
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new PollyResilienceService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new PollyResilienceService(_mockHttpClientFactory.Object, null!));
        }

        [Fact]
        public async Task Run_CallsCorrectHttpClientNames()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _pollyResilienceService.Run();

            // Assert
            _mockHttpClientFactory.Verify(f => f.CreateClient("PollyClient"), Times.AtLeastOnce);
            _mockHttpClientFactory.Verify(f => f.CreateClient("ProductionClient"), Times.AtLeastOnce);
            _mockHttpClientFactory.Verify(f => f.CreateClient("ContactsClient"), Times.AtLeastOnce);
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
            _mockHttpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.AtLeastOnce);
        }

        private void VerifySuccessfulLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SUCCESS")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyLogContains(LogLevel logLevel, string message)
        {
            _mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}