using Client.Services;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test.Services
{
    public class FileUploadServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly FileUploadService _fileUploadService;

        public FileUploadServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _fileUploadService = new FileUploadService(_mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task Run_WithValidUploads_ExecutesSuccessfully()
        {
            // Arrange
            SetupSuccessfulUploadResponses();

            // Act
            var exception = await Record.ExceptionAsync(() => _fileUploadService.Run());

            // Assert
            Assert.Null(exception);
            VerifyMultipleUploadRequestsMade();
        }

        [Fact]
        public async Task UploadFileWithMultipartFormData_CreatesCorrectRequest()
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
                    if (request.RequestUri != null && request.RequestUri.ToString().Contains("api/files/upload") && 
                        !request.RequestUri.ToString().Contains("stream") && 
                        !request.RequestUri.ToString().Contains("progress"))
                    {
                        capturedRequest = request;
                    }
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Upload successful") });

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.IsType<MultipartFormDataContent>(capturedRequest.Content);
        }

        [Fact]
        public async Task UploadFileAsStream_CreatesStreamContent()
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
                    if (request.RequestUri != null && request.RequestUri.ToString().Contains("upload-stream"))
                    {
                        capturedRequest = request;
                    }
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Stream upload successful") });

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.IsType<StreamContent>(capturedRequest.Content);
        }

        [Fact]
        public async Task UploadWithProgressTracking_UsesProgressableStreamContent()
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
                    if (request.RequestUri != null && request.RequestUri.ToString().Contains("upload-with-progress"))
                    {
                        capturedRequest = request;
                    }
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Progress upload successful") });

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.IsType<ProgressableStreamContent>(capturedRequest.Content);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.RequestEntityTooLarge)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task HandleUploadErrors_HandlesGracefully(HttpStatusCode statusCode)
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

            // Act
            var exception = await Record.ExceptionAsync(() => _fileUploadService.Run());

            // Assert
            Assert.Null(exception); // Should handle upload errors gracefully via try-catch
        }

        [Fact]
        public async Task UploadWithNetworkError_HandlesHttpRequestException()
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
            var exception = await Record.ExceptionAsync(() => _fileUploadService.Run());

            // Assert
            Assert.Null(exception); // Should handle network errors gracefully via try-catch
        }

        [Fact]
        public async Task MultipleUploadTypes_AllExecuteInSequence()
        {
            // Arrange
            var uploadRequests = new List<HttpRequestMessage>();
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    uploadRequests.Add(request);
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Success") });

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.Equal(3, uploadRequests.Count); // Should make 3 upload requests
            
            // Verify the correct endpoints were called
            Assert.Contains(uploadRequests, req => req.RequestUri != null && req.RequestUri.ToString().Contains("api/files/upload") && !req.RequestUri.ToString().Contains("stream") && !req.RequestUri.ToString().Contains("progress"));
            Assert.Contains(uploadRequests, req => req.RequestUri != null && req.RequestUri.ToString().Contains("api/files/upload-stream"));
            Assert.Contains(uploadRequests, req => req.RequestUri != null && req.RequestUri.ToString().Contains("api/files/upload-with-progress"));
        }

        [Fact]
        public async Task Run_CreatesMultipleHttpClientInstances()
        {
            // Arrange
            var httpClientCallCount = 0;
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient"))
                .Returns(() =>
                {
                    httpClientCallCount++;
                    return _httpClient;
                });

            SetupSuccessfulUploadResponses();

            // Act
            await _fileUploadService.Run();

            // Assert
            // Verify that the service creates a new HttpClient for each upload method
            Assert.Equal(3, httpClientCallCount);
        }

        private void SetupSuccessfulUploadResponses()
        {
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Upload successful")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(successResponse);
        }

        private void VerifyMultipleUploadRequestsMade()
        {
            _mockMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(3), // Should make 3 upload requests
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}