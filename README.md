# HttpClient .NET Examples

A comprehensive collection of HttpClient usage examples and patterns in .NET 9, demonstrating various scenarios from basic CRUD operations to advanced patterns like authentication, file uploads, concurrent requests, and more.

## 🚀 Features

This repository showcases the following HttpClient patterns and scenarios:

### Core Operations
- **CRUD Operations** - Complete Create, Read, Update, Delete examples
- **Error Handling** - Proper exception handling, timeout management, and retry patterns
- **Streaming** - Efficient handling of large payloads using streams
- **Custom Message Handlers** - Retry policies, timeout handlers, and custom middleware

### Advanced Scenarios
- **Authentication** - Bearer token, Basic auth, and token refresh patterns
- **File Uploads** - Multipart form data, stream uploads, and progress tracking
- **Concurrency** - Parallel requests, throttling, and batch processing
- **Custom Headers** - User agents, cache control, and conditional requests
- **Webhooks** - Signed webhooks, retry logic, and batch processing
- **GraphQL** - Query execution, mutations, and variable handling

### Best Practices
- HttpClientFactory usage patterns
- Proper resource disposal
- Performance optimization techniques
- Error resilience patterns
- Testing strategies with mocking

## 📁 Project Structure

```
HttpClient/
├── API/                    # Web API server for testing
├── Client/                 # HttpClient examples and services
│   ├── Services/          # Various HttpClient usage scenarios
│   │   ├── CRUDService.cs
│   │   ├── AuthenticationService.cs
│   │   ├── FileUploadService.cs
│   │   ├── ConcurrencyService.cs
│   │   ├── CustomHeadersService.cs
│   │   ├── WebhookService.cs
│   │   ├── GraphQLService.cs
│   │   ├── ErrorHandlingService.cs
│   │   └── StreamService.cs
│   └── Program.cs         # Main entry point
├── Client.Test/           # Comprehensive unit tests
├── Core/                  # Shared models and DTOs
└── Persistence/          # Data access layer
```

## 🛠️ Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 or VS Code
- Optional: Postman or similar tool for API testing

### Running the Examples

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd HttpClient
   ```

2. **Restore packages**
   ```bash
   dotnet restore
   ```

3. **Start the API server**
   ```bash
   cd API
   dotnet run
   ```
   The API will be available at `https://localhost:44354`

4. **Run client examples**
   ```bash
   cd Client
   dotnet run
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

### Choosing Examples

In `Client/Program.cs`, uncomment the service you want to run:

```csharp
// For CRUD operations
serviceCollection.AddScoped<IService, CRUDService>();

// For authentication examples
serviceCollection.AddScoped<IService, AuthenticationService>();

// For file upload examples
serviceCollection.AddScoped<IService, FileUploadService>();

// And so on...
```

## 📖 Usage Examples

### Basic CRUD Operations

```csharp
public class CRUDService : IService
{
    private static HttpClient httpClient = new HttpClient();

    public async Task<ContactViewModel> GetContact(Guid contactId)
    {
        var response = await httpClient.GetAsync($"api/contacts/{contactId}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ContactViewModel>(content);
    }

    public async Task<ContactViewModel> CreateContact()
    {
        var newContact = new Contact()
        {
            Name = $"New Name {DateTimeOffset.UtcNow}",
            Address = $"New Address {DateTimeOffset.UtcNow}"
        };

        var json = JsonConvert.SerializeObject(newContact);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync("api/contacts", content);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ContactViewModel>(responseContent);
    }
}
```

### Authentication with Bearer Token

```csharp
public class AuthenticationService : IService
{
    private readonly IHttpClientFactory httpClientFactory;
    private string? _accessToken;

    public async Task AuthenticateWithBearerToken()
    {
        var httpClient = httpClientFactory.CreateClient("ContactsClient");
        
        _accessToken = "your-jwt-token-here";
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await httpClient.GetAsync("api/contacts");
        // Handle response...
    }
}
```

### File Upload with Progress Tracking

```csharp
public class FileUploadService : IService
{
    public async Task UploadWithProgressTracking()
    {
        var httpClient = httpClientFactory.CreateClient("ContactsClient");
        
        var fileContent = new string('A', 10000); // 10KB sample
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);

        var progressContent = new ProgressableStreamContent(new ByteArrayContent(fileBytes));
        progressContent.Progress += (sent, total) =>
        {
            var percentage = (double)sent / total * 100;
            Console.WriteLine($"Upload progress: {percentage:F1}%");
        };

        var response = await httpClient.PostAsync("api/files/upload-with-progress", progressContent);
        // Handle response...
    }
}
```

### Concurrent Requests with Throttling

```csharp
public class ConcurrencyService : IService
{
    public async Task ConcurrentRequestsWithSemaphore()
    {
        var httpClient = httpClientFactory.CreateClient("ContactsClient");
        var semaphore = new SemaphoreSlim(3, 3); // Limit to 3 concurrent requests
        
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(ProcessWithSemaphore(httpClient, semaphore, i));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessWithSemaphore(HttpClient httpClient, SemaphoreSlim semaphore, int taskId)
    {
        await semaphore.WaitAsync();
        try
        {
            var response = await httpClient.GetAsync("api/contacts");
            // Process response...
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

## 🧪 Testing

The repository includes comprehensive unit tests demonstrating how to test HttpClient usage with mocking:

```csharp
public class CRUDServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockMessageHandler;
    private readonly HttpClient _httpClient;

    [Fact]
    public async Task GetContact_WithValidId_ReturnsContact()
    {
        // Arrange
        var expectedContact = new ContactViewModel { /*...*/ };
        var jsonResponse = JsonConvert.SerializeObject(expectedContact);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        _mockMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert
        var result = await _crudService.GetContact(contactId);
        Assert.NotNull(result);
    }
}
```

Run tests with:
```bash
dotnet test --logger:trx --collect:"XPlat Code Coverage"
```

## 📋 Service Scenarios

| Service | Description | Key Features |
|---------|-------------|--------------|
| `CRUDService` | Basic CRUD operations | GET, POST, PUT, DELETE, PATCH operations |
| `AuthenticationService` | Authentication patterns | Bearer tokens, Basic auth, token refresh |
| `FileUploadService` | File upload scenarios | Multipart uploads, streaming, progress tracking |
| `ConcurrencyService` | Concurrent request handling | Parallel execution, throttling, batch processing |
| `CustomHeadersService` | Header management | Custom headers, User-Agent, cache control |
| `WebhookService` | Webhook patterns | Signed webhooks, retry logic, batch events |
| `GraphQLService` | GraphQL integration | Queries, mutations, variables |
| `ErrorHandlingService` | Error management | Timeout handling, retry policies, cancellation |
| `StreamService` | Streaming operations | Large payload handling, compression |

## ⚙️ Configuration

### HttpClient Factory Setup

```csharp
public static class HttpClientServicesExtensions
{
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
    {
        services.AddHttpClient("ContactsClient", client =>
        {
            client.BaseAddress = new Uri("https://localhost:44354/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "HttpClientExample/1.0");
        })
        .AddHttpMessageHandler<RetryPolicyDelegatingHandler>()
        .AddHttpMessageHandler<TimeOutDelegatingHandler>();

        return services;
    }
}
```

### Custom Message Handlers

```csharp
public class RetryPolicyDelegatingHandler : DelegatingHandler
{
    private const int MaxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (int i = 0; i <= MaxRetries; i++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                
                if (response.IsSuccessStatusCode || i == MaxRetries)
                    return response;
                    
                if (IsTransientError(response.StatusCode))
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)), cancellationToken);
                    continue;
                }
                
                return response;
            }
            catch (HttpRequestException) when (i < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)), cancellationToken);
            }
        }
        
        throw new HttpRequestException("Request failed after maximum retry attempts");
    }
}
```

## 🚨 Error Handling Patterns

### Timeout and Cancellation

```csharp
public async Task GetContactsWithTimeout()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    
    try
    {
        var response = await httpClient.GetAsync("api/contacts", cts.Token);
        // Process response...
    }
    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    {
        Console.WriteLine("Request timed out");
    }
    catch (TaskCanceledException ex)
    {
        Console.WriteLine("Request was cancelled");
    }
}
```

### Polly Integration (Recommended)

```csharp
// Add to your project for production use
services.AddHttpClient("ContactsClient")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
```

## 📊 Performance Tips

1. **Use HttpClientFactory** - Avoid socket exhaustion and DNS issues
2. **Reuse HttpClient instances** - Don't create new instances for each request  
3. **Configure connection pooling** - Set appropriate `MaxConnectionsPerServer`
4. **Use streaming for large payloads** - Avoid loading entire response into memory
5. **Implement proper timeout handling** - Set appropriate timeouts for your scenarios
6. **Consider compression** - Enable gzip compression for bandwidth savings

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with .NET 9.0
- Uses HttpClientFactory for optimal resource management
- Demonstrates modern async/await patterns
- Includes comprehensive testing examples
- Follows Microsoft's recommended practices for HttpClient usage

## 📚 Further Reading

- [HttpClient Guidelines](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [HttpClientFactory in .NET](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly)
- [Performance Best Practices](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests)