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
    /// Tests for consistent error handling across all services using HttpResponseHelper
    /// </summary>
    public class ConsistentErrorHandlingTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

        public ConsistentErrorHandlingTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        [InlineData("undefined")]
        [InlineData("not json")]
        public void HttpResponseHelper_WithInvalidJson_ReturnsNullGracefully(string invalidContent)
        {
            // Act
            var result = HttpResponseHelper.SafeJsonDeserialize<object>(invalidContent);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(@"{""invalid"": json}")]
        [InlineData(@"[malformed")]
        [InlineData(@"<xml>not json</xml>")]
        public void HttpResponseHelper_WithMalformedJson_ReturnsNullGracefully(string malformedJson)
        {
            // Act
            var result = HttpResponseHelper.SafeJsonDeserialize<object>(malformedJson);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public void HttpResponseHelper_WithClientErrors_HandlesGracefully(HttpStatusCode statusCode)
        {
            // Arrange
            var response = new HttpResponseMessage(statusCode);

            // Act
            var handled = HttpResponseHelper.HandleCommonErrors(response);

            // Assert
            Assert.True(handled, $"Should handle {statusCode} gracefully");
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public void HttpResponseHelper_WithServerErrors_AllowsRetryLogic(HttpStatusCode statusCode)
        {
            // Arrange
            var response = new HttpResponseMessage(statusCode);

            // Act
            var handled = HttpResponseHelper.HandleCommonErrors(response);

            // Assert
            Assert.False(handled, $"Should NOT handle {statusCode} to allow retry logic");
        }

        [Theory]
        [InlineData("text/plain", "Some plain text")]
        [InlineData("text/html", "<html><body>HTML content</body></html>")]
        [InlineData("application/xml", "<?xml version=\"1.0\"?><root></root>")]
        [InlineData("image/jpeg", "binary data")]
        public async Task HttpResponseHelper_WithNonJsonContentTypes_ReturnsNullForJsonDeserialization(string contentType, string content)
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(content, System.Text.Encoding.UTF8, contentType);

            // Act
            var result = await HttpResponseHelper.SafeDeserializeAsync<object>(response);

            // Assert
            if (contentType.Contains("json"))
            {
                // JSON content types should attempt deserialization
                Assert.True(result != null || content == "binary data"); // Allow null for invalid JSON
            }
            else
            {
                // Non-JSON content types should return null for JSON deserialization
                Assert.Null(result);
            }
        }

        [Fact]
        public async Task HttpResponseHelper_WithValidJsonArray_DeserializesCorrectly()
        {
            // Arrange
            var jsonContent = @"[{""Id"":""" + Guid.NewGuid() + @""",""Name"":""Test"",""Address"":""Test Address""}]";
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            // Act
            var result = await HttpResponseHelper.SafeDeserializeAsync<Core.ViewModels.ContactViewModel[]>(response);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Test", result[0].Name);
        }

        [Fact]
        public async Task HttpResponseHelper_WithErrorResponse_ReturnsNullGracefully()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            response.Content = new StringContent("Server Error", System.Text.Encoding.UTF8, "text/plain");

            // Act
            var result = await HttpResponseHelper.SafeDeserializeAsync<object>(response);

            // Assert
            Assert.Null(result); // Should handle error responses gracefully
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
