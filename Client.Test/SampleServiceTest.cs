using Client.Test.HandlersStub;
using Moq;
using Moq.Protected;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.Test;

/// <summary>
/// Test class for SampleService HTTP operations.
/// Tests are using xUnit v3 for .NET 9.
/// </summary>
public class SampleServiceTest
{
    [Fact]
    public async Task GetContactsAsStream_On401Response_MustThrowUnauthorizedApiAccessException()
    {
        // Arrange
        using var httpClient = new HttpClient(new Return401UnauthorizedResponseHandler());
        var sut = new SampleService(httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedApiAccessException>(
            () => sut.GetContactsAsStream());
        
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GetContactsAsStream_On401Response_MustThrowUnauthorizedApiAccessException_WithMoq()
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });
        
        using var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        var sut = new SampleService(httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedApiAccessException>(() 
            => sut.GetContactsAsStream());
            
        Assert.NotNull(exception);
        
        // Verify that the HTTP call was made
        httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetContactsAsStream_On200Response_ReturnsCorrectNumberOfContacts()
    {
        // Arrange
        using var httpClient = new HttpClient(new Return200OkResponseHandler());
        var sut = new SampleService(httpClient);

        // Act
        var data = await sut.GetContactsAsStream();
        
        // Assert
        Assert.NotNull(data);
        Assert.Equal(2, data.Count());
        
        // Additional assertions to verify data quality
        Assert.All(data, contact => 
        {
            Assert.NotEqual(Guid.Empty, contact.Id);
            Assert.NotNull(contact.Name);
            Assert.NotNull(contact.Address);
            Assert.Equal("Test", contact.Name);
            Assert.Equal("Test", contact.Address);
        });
    }

    [Fact]
    public async Task GetContactsAsStream_On404Response_ReturnsNull()
    {
        // Arrange
        using var httpClient = new HttpClient(new Return404NotFoundResponseHandler());
        var sut = new SampleService(httpClient);

        // Act
        var result = await sut.GetContactsAsStream();
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetContactsAsStream_On404Response_ReturnsNull_WithMoq()
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });
        
        using var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        var sut = new SampleService(httpClient);

        // Act
        var result = await sut.GetContactsAsStream();
        
        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetContactsAsStream_OnServerError_ThrowsHttpRequestException(HttpStatusCode errorStatusCode)
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = errorStatusCode,
                ReasonPhrase = "Server Error"
            });
        
        using var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        var sut = new SampleService(httpClient);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.GetContactsAsStream());
        
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GetContactsAsStream_VerifiesCorrectRequestConfiguration()
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        HttpRequestMessage? capturedRequest = null;
        
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, token) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        
        using var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        var sut = new SampleService(httpClient);

        // Act
        await sut.GetContactsAsStream();
        
        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        
        // The request URI should be the full URL since SampleService sets BaseAddress
        Assert.Equal("https://localhost:44354/api/contacts/", capturedRequest.RequestUri?.ToString());
        
        Assert.Contains(capturedRequest.Headers.Accept, 
            header => header.MediaType == "application/json");
    }
}
