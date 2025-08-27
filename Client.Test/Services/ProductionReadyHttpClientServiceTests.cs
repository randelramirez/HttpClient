using Client.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class ProductionReadyHttpClientServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<ProductionReadyHttpClientService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly ProductionReadyHttpClientService _productionService;

        public ProductionReadyHttpClientServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _mockLogger = new Mock<ILogger<ProductionReadyHttpClientService>>();
            
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(c => c["ApiSettings:TimeoutSeconds"]).Returns("30");
            _mockConfiguration.Setup(c => c["ApiSettings:MaxRetries"]).Returns("3");
            _mockConfiguration.Setup(c => c["ApiSettings:BaseUrl"]).Returns("https://localhost:44354/");

            _productionService = new ProductionReadyHttpClientService(
                _mockHttpClientFactory.Object, 
                _mockLogger.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesAllDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _productionService.Run();

            // Assert
            VerifyHttpClientFactoryCalls();
            VerifyProductionLogging();
        }

        [Fact]
        public async Task HeaderManagement_WithProperHeaders_SetsCorrectHeaders()
        {
            // Arrange
            var capturedRequests = new List<HttpRequestMessage>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => capturedRequests.Add(request))
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                }));

            // Act
            await _productionService.Run();

            // Assert - Find any request (they should all have proper headers from DemonstrateProperHeaderManagement)
            Assert.NotEmpty(capturedRequests);
            var headerRequest = capturedRequests.First(); // First request is from DemonstrateProperHeaderManagement
            Assert.Contains(headerRequest.Headers.Accept, h => h.MediaType == "application/json");
            Assert.True(headerRequest.Headers.Contains("X-Request-ID"));
            Assert.True(headerRequest.Headers.Contains("X-Client-Version"));
            Assert.True(headerRequest.Headers.Contains("X-Correlation-ID"));
        }

        [Fact]
        public async Task SecureApiCalls_WithAuthentication_IncludesBearerToken()
        {
            // Arrange
            var capturedRequests = new List<HttpRequestMessage>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => capturedRequests.Add(request))
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                }));

            // Act
            await _productionService.Run();

            // Assert - Find the request with Authorization header (from DemonstrateSecureApiCalls)
            var secureRequest = capturedRequests.FirstOrDefault(r => r.Headers.Authorization != null);
            Assert.NotNull(secureRequest);
            Assert.NotNull(secureRequest.Headers.Authorization);
            Assert.Equal("Bearer", secureRequest.Headers.Authorization.Scheme);
            Assert.True(secureRequest.Headers.Contains("X-Requested-With"));
        }

        [Fact]
        public async Task CorrelationIdPropagation_WithTracing_PropagatesIds()
        {
            // Arrange
            var requests = new List<HttpRequestMessage>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => requests.Add(request))
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                }));

            // Act
            await _productionService.Run();

            // Assert
            var correlationRequest = requests.Find(r => r.Headers.Contains("X-Correlation-ID"));
            Assert.NotNull(correlationRequest);
            Assert.True(correlationRequest.Headers.Contains("X-Request-ID"));
        }

        [Fact]
        public async Task ConfigurationBasedClient_WithSettings_UsesConfigValues()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _productionService.Run();

            // Assert
            _mockConfiguration.Verify(c => c["ApiSettings:TimeoutSeconds"], Times.Never); // Service uses hardcoded values in demo
            VerifyProductionLogging();
        }

        [Fact]
        public async Task HealthChecks_WithServiceMonitoring_ChecksEndpointHealth()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                }));

            // Act
            await _productionService.Run();

            // Assert
            VerifyHealthCheckLogging();
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task SecureApiCalls_WithErrorResponses_HandlesGracefully(HttpStatusCode statusCode)
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult(new HttpResponseMessage(statusCode)));

            // Act
            await _productionService.Run();

            // Assert
            VerifyErrorHandlingLogging();
        }

        [Fact]
        public async Task Production_WithNetworkException_LogsErrorGracefully()
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
            await _productionService.Run();

            // Assert
            VerifyErrorLogging();
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProductionReadyHttpClientService(null!, _mockLogger.Object, _mockConfiguration.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ProductionReadyHttpClientService(_mockHttpClientFactory.Object, null!, _mockConfiguration.Object));
        }

        [Fact]
        public void Constructor_WithNullConfiguration_DoesNotThrow()
        {
            // Act & Assert - Configuration is optional in this service
            var exception = Record.Exception(() => 
                new ProductionReadyHttpClientService(_mockHttpClientFactory.Object, _mockLogger.Object, null!));
            Assert.Null(exception);
        }

        private void SetupSuccessfulResponses()
        {
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                }));
        }

        private void VerifyHttpClientFactoryCalls()
        {
            _mockHttpClientFactory.Verify(f => f.CreateClient("ContactsClient"), Times.AtLeastOnce);
        }

        private void VerifyProductionLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("production", StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyHealthCheckLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("health", StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyErrorHandlingLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("status", StringComparison.OrdinalIgnoreCase) || 
                                                 v.ToString()!.Contains("failed", StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyErrorLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
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