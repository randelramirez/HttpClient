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
    public class GraphQLServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly GraphQLService _graphQLService;

        public GraphQLServiceTests()
        {
            _mockMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:44354/")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("ContactsClient")).Returns(_httpClient);

            _graphQLService = new GraphQLService(_mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task Run_WithSuccessfulResponses_CompletesGraphQLDemonstrations()
        {
            // Arrange
            SetupSuccessfulResponses();

            // Act
            await _graphQLService.Run();

            // Assert
            VerifyHttpClientFactoryCalls();
        }

        [Fact]
        public async Task GraphQLService_WithMockedQueries_ProcessesGraphQLPatterns()
        {
            // Arrange
            var graphqlResponse = @"{""data"":{""contacts"":[{""id"":""1"",""name"":""Test Contact"",""address"":""Test Address""}]}}";
            
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(graphqlResponse, System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            await _graphQLService.Run();

            // Assert
            _mockMessageHandler.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GraphQLService_WithErrorResponse_HandlesGracefully()
        {
            // Arrange
            _mockMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(@"{""errors"":[{""message"":""Invalid query""}]}", System.Text.Encoding.UTF8, "application/json")
                });

            // Act
            var exception = await Record.ExceptionAsync(() => _graphQLService.Run());

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
            var exception = Record.Exception(() => new GraphQLService(null!));
            // GraphQLService doesn't validate in constructor
            Assert.Null(exception);
        }

        [Fact]
        public void GraphQLService_CanBeInstantiated()
        {
            // Act & Assert
            var service = new GraphQLService(_mockHttpClientFactory.Object);
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
                    Content = new StringContent(@"{""data"":{""contacts"":[]}}", System.Text.Encoding.UTF8, "application/json")
                });
        }

        private void VerifyHttpClientFactoryCalls()
        {
            _mockHttpClientFactory.Verify(f => f.CreateClient("ContactsClient"), Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}