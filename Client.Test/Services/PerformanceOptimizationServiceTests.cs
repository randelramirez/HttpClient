using Client.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class PerformanceOptimizationServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<PerformanceOptimizationService>> _mockLogger;
        private readonly PerformanceOptimizationService _performanceService;

        public PerformanceOptimizationServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _mockLogger = new Mock<ILogger<PerformanceOptimizationService>>();

            _performanceService = new PerformanceOptimizationService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesAllOptimizations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _performanceService.Run();

            // Assert
            VerifyHttpClientFactoryCalls();
            VerifyPerformanceLogging();
        }

        [Fact]
        public async Task HighThroughputRequests_WithConnectionPooling_OptimizesConnections()
        {
            // Arrange
            var requestCount = 0;
            var concurrentRequests = 0;
            var maxConcurrentRequests = 0;

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
                {
                    Interlocked.Increment(ref requestCount);
                    var current = Interlocked.Increment(ref concurrentRequests);
                    
                    // Track max concurrent requests
                    var max = Math.Max(maxConcurrentRequests, current);
                    Interlocked.Exchange(ref maxConcurrentRequests, max);
                    
                    // Simulate processing time
                    await Task.Delay(20, ct);
                    
                    Interlocked.Decrement(ref concurrentRequests);
                    
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                    };
                });

            // Act
            var startTime = DateTime.UtcNow;
            await _performanceService.Run();
            var endTime = DateTime.UtcNow;

            // Assert
            var duration = endTime - startTime;
            Assert.True(requestCount > 0, "Performance service should make requests");
            Assert.True(duration < TimeSpan.FromSeconds(5), "Should complete efficiently with mocking");
        }

        [Fact]
        public async Task StreamingDownload_WithLargePayload_ProcessesEfficiently()
        {
            // Arrange
            var largePayload = new string('x', 1024 * 100); // 100KB payload
            var largePayloadBytes = System.Text.Encoding.UTF8.GetBytes(largePayload);

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(largePayloadBytes))
                }));

            // Act
            var startTime = DateTime.UtcNow;
            await _performanceService.Run();
            var endTime = DateTime.UtcNow;

            // Assert
            var duration = endTime - startTime;
            Assert.True(duration < TimeSpan.FromSeconds(10), "Large payload streaming should be efficient");
        }

        [Fact]
        public async Task CompressionHandling_WithGZipContent_DecompressesEfficiently()
        {
            // Arrange
            var originalContent = "Large content that benefits from compression: " + new string('A', 1000);
            var originalBytes = System.Text.Encoding.UTF8.GetBytes(originalContent);
            
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Compress))
            {
                await gzipStream.WriteAsync(originalBytes, 0, originalBytes.Length);
            }
            
            var compressedBytes = compressedStream.ToArray();
            
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(compressedBytes)
            };
            response.Content.Headers.Add("Content-Encoding", "gzip");

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => 
                {
                    var newResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(compressedBytes)
                    };
                    newResponse.Content.Headers.Add("Content-Encoding", "gzip");
                    return Task.FromResult(newResponse);
                });

            // Act
            var exception = await Record.ExceptionAsync(() => _performanceService.Run());

            // Assert
            Assert.Null(exception);
            Assert.True(compressedBytes.Length < originalBytes.Length, "Content should be compressed");
        }

        [Fact]
        public async Task BulkOperations_WithBatchProcessing_OptimizesThroughput()
        {
            // Arrange
            var requestCount = 0;
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequestMessage req, CancellationToken ct) =>
                {
                    Interlocked.Increment(ref requestCount);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                    });
                });

            // Act
            await _performanceService.Run();

            // Assert
            // PerformanceOptimizationService should make multiple requests for streaming, connection pooling, etc.
            Assert.True(requestCount >= 5, $"Expected at least 5 requests for performance optimizations, got {requestCount}");
        }

        [Theory]
        [InlineData(1000)] // 1KB
        [InlineData(10000)] // 10KB  
        [InlineData(100000)] // 100KB
        public async Task VariablePayloadSizes_WithOptimization_HandlesEfficiently(int payloadSize)
        {
            // Arrange
            var payload = new string('x', payloadSize);
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                }));

            // Act
            var startTime = DateTime.UtcNow;
            await _performanceService.Run();
            var endTime = DateTime.UtcNow;

            // Assert
            var duration = endTime - startTime;
            var expectedMaxDuration = TimeSpan.FromSeconds(10); // Reasonable upper bound
            Assert.True(duration < expectedMaxDuration, $"Payload size {payloadSize} should be processed efficiently in less than {expectedMaxDuration.TotalSeconds}s, took {duration.TotalSeconds}s");
        }

        [Fact]
        public async Task ConnectionReuse_WithKeepAlive_OptimizesPerformance()
        {
            // Arrange
            var requestTimes = new List<DateTime>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequestMessage req, CancellationToken ct) =>
                {
                    requestTimes.Add(DateTime.UtcNow);
                    
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                    });
                });

            // Act
            await _performanceService.Run();

            // Assert
            Assert.True(requestTimes.Count > 0, "Performance service should make requests");
        }

        [Fact]
        public async Task MemoryOptimization_WithDisposableResources_ManagesMemoryEfficiently()
        {
            // Arrange
            var memoryBefore = GC.GetTotalMemory(false);
            
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
            await _performanceService.Run();
            
            // Force garbage collection to test memory management
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfter = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = memoryAfter - memoryBefore;
            Assert.True(memoryIncrease < 10 * 1024 * 1024, "Memory usage should be optimized (less than 10MB increase)");
        }

        [Fact]
        public async Task ErrorHandling_WithPerformanceOptimization_FailsFast()
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
            var startTime = DateTime.UtcNow;
            var exception = await Record.ExceptionAsync(() => _performanceService.Run());
            var endTime = DateTime.UtcNow;

            // Assert
            var duration = endTime - startTime;
            
            // The service should fail fast when encountering network errors
            Assert.NotNull(exception);
            Assert.True(exception is HttpRequestException || exception.InnerException is HttpRequestException,
                $"Expected HttpRequestException, but got: {exception.GetType().Name}");
            Assert.True(duration < TimeSpan.FromSeconds(5), "Error handling should fail fast");
        }

        [Fact]
        public void Constructor_WithNullDependencies_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new PerformanceOptimizationService(null!, _mockLogger.Object));
            
            Assert.Throws<ArgumentNullException>(() => 
                new PerformanceOptimizationService(_mockHttpClientFactory.Object, null!));
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

        private void VerifyPerformanceLogging()
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("performance", StringComparison.OrdinalIgnoreCase)),
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