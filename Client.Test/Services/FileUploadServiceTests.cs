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
            _mockMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Default);
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
            HttpRequestMessage capturedRequest = null;
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Upload successful")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Post && 
                        req.RequestUri.ToString().Contains("upload")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(successResponse);

            SetupOtherUploadResponses();

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.IsType<MultipartFormDataContent>(capturedRequest.Content);
        }

        [Fact]
        public async Task UploadFileAsStream_CreatesStreamContent()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Stream upload successful")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("upload-stream")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(successResponse);

            SetupOtherUploadResponses();

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.IsType<StreamContent>(capturedRequest.Content);
        }

        [Fact]
        public async Task UploadWithProgressTracking_UsesProgressableStreamContent()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;
            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Progress upload successful")
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("upload-with-progress")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(successResponse);

            SetupOtherUploadResponses();

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.NotNull(capturedRequest);
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
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(errorResponse);

            // Act
            var exception = await Record.ExceptionAsync(() => _fileUploadService.Run());

            // Assert
            Assert.Null(exception); // Should handle upload errors gracefully
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
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var exception = await Record.ExceptionAsync(() => _fileUploadService.Run());

            // Assert
            Assert.Null(exception); // Should handle network errors gracefully
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
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    uploadRequests.Add(request);
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Success") });

            // Act
            await _fileUploadService.Run();

            // Assert
            Assert.Equal(3, uploadRequests.Count); // Should make 3 upload requests
            Assert.Contains(uploadRequests, req => req.RequestUri.ToString().Contains("upload") && !req.RequestUri.ToString().Contains("stream") && !req.RequestUri.ToString().Contains("progress"));
            Assert.Contains(uploadRequests, req => req.RequestUri.ToString().Contains("upload-stream"));
            Assert.Contains(uploadRequests, req => req.RequestUri.ToString().Contains("upload-with-progress"));
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
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(successResponse);
        }

        private void SetupOtherUploadResponses()
        {
            var responses = new Dictionary<string, HttpResponseMessage>
            {
                ["upload"] = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Multipart upload successful") },
                ["upload-stream"] = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Stream upload successful") },
                ["upload-with-progress"] = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Progress upload successful") }
            };

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => !req.RequestUri.ToString().Contains("upload-stream") && !req.RequestUri.ToString().Contains("upload-with-progress")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responses["upload"]);

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("upload-stream")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responses["upload-stream"]);

            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("upload-with-progress")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responses["upload-with-progress"]);
        }

        private void VerifyMultipleUploadRequestsMade()
        {
            _mockMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(3), // Should make 3 upload requests
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