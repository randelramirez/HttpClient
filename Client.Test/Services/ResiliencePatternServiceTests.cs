using Client.Services;
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
    public class ResiliencePatternServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<ResiliencePatternService>> _mockLogger;
        private readonly ResiliencePatternService _resilienceService;

        public ResiliencePatternServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _mockLogger = new Mock<ILogger<ResiliencePatternService>>();

            _resilienceService = new ResiliencePatternService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesResilienceDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _resilienceService.Run();

            // Assert
            VerifyHttpClientFactoryCalls();
            VerifyLogging();
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ResiliencePatternService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ResiliencePatternService(_mockHttpClientFactory.Object, null!));
        }

        [Fact]
        public void ResiliencePatternService_CanBeInstantiated()
        {
            // Act & Assert
            var service = new ResiliencePatternService(_mockHttpClientFactory.Object, _mockLogger.Object);
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
            _mockHttpClientFactory.Verify(f => f.CreateClient("ContactsClient"), Times.AtLeastOnce);
        }

        private void VerifyLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("resilience", StringComparison.OrdinalIgnoreCase)),
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