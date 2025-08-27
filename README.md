# HttpClient .NET 9 Patterns & Best Practices

A comprehensive guide to HttpClient usage patterns in .NET 9, demonstrating **5 different approaches** from basic implementations to Microsoft's recommended production patterns with Polly resilience.

## ?? What You'll Learn

This repository showcases **every major HttpClient pattern** with practical examples:

- **Microsoft's official Polly recommendations** for production resilience
- **Custom implementations** for educational understanding
- **Performance optimization** techniques and patterns
- **Enterprise-grade** logging, monitoring, and security
- **Typed clients** vs **named clients** vs **basic HttpClient**

## ?? Quick Start

```bash
git clone https://github.com/randelramirez/HttpClient.git
cd HttpClient
dotnet restore
dotnet run --project Client  # Runs Microsoft's recommended Polly patterns
```

## ?? HttpClient Patterns & Approaches

### 1. ?? **Microsoft's Recommended: Polly + HttpClientFactory**

**? Use This in Production** - Microsoft's official resilience approach.

```csharp
// Zero configuration needed - policies applied automatically
public async Task<List<Contact>> GetContacts()
{
    var httpClient = _httpClientFactory.CreateClient("PollyClient");
    var response = await httpClient.GetAsync("api/contacts");
    // Retry, Circuit Breaker, Timeout policies applied transparently!
    return JsonSerializer.Deserialize<List<Contact>>(await response.Content.ReadAsStringAsync());
}
```

**How it works:**
```csharp
// Configuration (HttpClientServices.cs)
services.AddHttpClient("PollyClient")
    .AddPolicyHandler(GetRetryPolicy())        // Exponential backoff + jitter
    .AddPolicyHandler(GetCircuitBreakerPolicy()) // Prevents cascading failures
    .AddPolicyHandler(GetTimeoutPolicy());     // Request timeout protection
```

**?? When to use:** All production scenarios
**?? Example Service:** `PollyResilienceService` ? **Currently Active**

---

### 2. ??? **Enterprise Production Pattern**

**Production + Logging + Monitoring + Security**

```csharp
// Enterprise-grade with comprehensive observability
public async Task<ApiResponse<T>> CallSecureApi<T>(string endpoint)
{
    var httpClient = _httpClientFactory.CreateClient("ProductionClient");
    
    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
    request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
    
    var response = await httpClient.SendAsync(request);
    // Automatic: Structured logging + Retry + Circuit breaker + Performance optimization
    
    return await ProcessResponse<T>(response);
}
```

**Features included:**
- ? **Structured logging** with correlation IDs
- ? **Security headers** and authentication patterns
- ? **Polly resilience** (retry + circuit breaker)
- ? **Performance optimization** (compression, connection pooling)
- ? **Health monitoring** integration

**?? When to use:** Enterprise applications, microservices
**?? Example Service:** `ProductionReadyHttpClientService`

---

### 3. ? **High-Performance Pattern**

**Optimized for throughput and large payloads**

```csharp
// Streaming for memory efficiency
public async Task<Stream> DownloadLargeFile(string url)
{
    var httpClient = _httpClientFactory.CreateClient("PerformanceClient");
    
    // Stream response without loading into memory
    var response = await httpClient.SendAsync(
        new HttpRequestMessage(HttpMethod.Get, url), 
        HttpCompletionOption.ResponseHeadersRead);
    
    return await response.Content.ReadAsStreamAsync();
}

// Concurrent requests with throttling
public async Task<List<T>> ProcessConcurrentRequests<T>(IEnumerable<string> urls)
{
    var semaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent
    var tasks = urls.Select(async url =>
    {
        await semaphore.WaitAsync();
        try
        {
            return await httpClient.GetFromJsonAsync<T>(url);
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    return (await Task.WhenAll(tasks)).ToList();
}
```

**Performance features:**
- ? **Connection pooling** (50 connections per server)
- ? **Streaming** for large payloads
- ? **Compression** handling (GZip/Deflate)
- ? **Concurrent throttling** to prevent server overload

**?? When to use:** High-throughput scenarios, file processing, bulk operations
**?? Example Service:** `PerformanceOptimizationService`

---

### 4. ?? **Typed Client Pattern**

**Strongly-typed, testable, encapsulated HTTP logic**

```csharp
// Strongly-typed client
public class ContactsApiClient
{
    private readonly HttpClient _httpClient;

    public ContactsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<Contact>> GetContactsAsync()
    {
        var response = await _httpClient.GetAsync("api/contacts");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<Contact>>();
    }

    public async Task<Contact> CreateContactAsync(CreateContactRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/contacts", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Contact>();
    }
}

// Usage via dependency injection
public class ContactService
{
    private readonly ContactsApiClient _apiClient;
    
    public ContactService(ContactsApiClient apiClient) => _apiClient = apiClient;
    
    public async Task<Contact[]> GetAllContacts() => 
        (await _apiClient.GetContactsAsync()).ToArray();
}
```

**Configuration:**
```csharp
services.AddHttpClient<ContactsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
})
.AddPolicyHandler(GetRetryPolicy()); // Polly integration
```

**Benefits:**
- ? **Type safety** and IntelliSense support
- ? **Easy to mock** for unit testing
- ? **Encapsulated logic** - HTTP details hidden from business logic
- ? **Dependency injection** ready

**?? When to use:** Clean architecture, testable code, service boundaries
**?? Example Service:** `HttpClientFactoryManagementService`

---

### 5. ?? **Educational: Custom Resilience Handlers**

**?? Learning Only - Use Polly in Production**

```csharp
// Custom retry implementation (for understanding concepts)
[Obsolete("Use Polly instead - this is educational only")]
public class RetryPolicyDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) return response;
                
                if (attempt == maxRetries - 1) return response;
                
                // Simple delay - Polly does exponential backoff + jitter
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
            catch (HttpRequestException) when (attempt < maxRetries - 1)
            {
                // Continue to next attempt
            }
        }
        
        throw new HttpRequestException("All retry attempts failed");
    }
}
```

**?? Educational value:**
- Understand **how retry patterns work** internally
- Learn **circuit breaker concepts** and state management
- See **why Polly is better** (jitter, sophisticated policies, testing)

**?? Don't use in production:** Custom implementations lack battle-testing, edge case handling, and advanced features.

**?? Example Service:** `ResiliencePatternService`

---

## ?? Switching Between Patterns

In `Client/Program.cs`, uncomment the pattern you want to explore:

```csharp
// ?? Microsoft's Recommended (Currently Active)
serviceCollection.AddScoped<IService, PollyResilienceService>();

// ??? Enterprise Production Pattern
//serviceCollection.AddScoped<IService, ProductionReadyHttpClientService>();

// ? High-Performance Pattern
//serviceCollection.AddScoped<IService, PerformanceOptimizationService>();

// ?? Typed Client Pattern
//serviceCollection.AddScoped<IService, HttpClientFactoryManagementService>();

// ?? Educational Custom Handlers
//serviceCollection.AddScoped<IService, ResiliencePatternService>();
```

Then run: `dotnet run --project Client`

## ?? Complete Pattern Library

### Basic Operations
| Pattern | Service | What It Demonstrates |
|---------|---------|---------------------|
| **CRUD Operations** | `CRUDService` | GET, POST, PUT, DELETE with proper error handling |
| **Authentication** | `AuthenticationService` | Bearer tokens, API keys, token refresh |
| **Custom Headers** | `CustomHeadersService` | Request/response headers, correlation IDs |

### Advanced Scenarios
| Pattern | Service | What It Demonstrates |
|---------|---------|---------------------|
| **File Upload** | `FileUploadService` | Multipart uploads, progress tracking, streaming |
| **Error Handling** | `ErrorHandlingService` | Timeout, cancellation, retry strategies |
| **Concurrency** | `ConcurrencyService` | Parallel requests, throttling, bulk operations |
| **Streaming** | `StreamService` | Large payload processing, memory efficiency |

### Integration Patterns
| Pattern | Service | What It Demonstrates |
|---------|---------|---------------------|
| **GraphQL** | `GraphQLService` | Query execution, mutations, subscriptions |
| **Webhooks** | `WebhookService` | Signed webhooks, batch processing |

## ?? Testing Patterns

Each approach includes comprehensive tests showing how to mock and test HttpClient usage:

```csharp
[Fact]
public async Task PollyClient_WithTransientErrors_RetriesAutomatically()
{
    // Arrange: Setup sequence of failures then success
    _mockHandler.SetupSequence<Task<HttpResponseMessage>>("SendAsync")
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) 
        { 
            Content = new StringContent("[]") 
        });

    // Act: Call service
    await _pollyService.Run();

    // Assert: Verify 3 attempts were made
    _mockHandler.Protected().Verify("SendAsync", Times.Exactly(3));
}
```

**Run tests:** `dotnet test` (64 tests covering all patterns)

## ?? Configuration Examples

### Microsoft's Recommended Setup
```csharp
services.AddHttpClient("ProductionClient", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
    client.Timeout = TimeSpan.FromSeconds(100);
    client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0 (.NET 9.0)");
})
.AddHttpMessageHandler<LoggingDelegatingHandler>()
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
        TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100))))
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

### Configuration-Driven Setup
```json
{
  "HttpClientSettings": {
    "PollyClient": {
      "BaseAddress": "https://api.example.com/",
      "Timeout": "00:01:40",
      "RetryPolicy": { "RetryCount": 3, "BaseDelaySeconds": 1 },
      "CircuitBreaker": { "FailureThreshold": 5, "DurationOfBreakSeconds": 30 }
    }
  }
}
```

## ?? Microsoft's Official Recommendations

Based on [Microsoft's HttpClient documentation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly):

### ? Production Checklist
- [x] **Use HttpClientFactory** (not `new HttpClient()`)
- [x] **Use Polly for resilience** (retry, circuit breaker, timeout)
- [x] **Enable compression** (GZip/Deflate)
- [x] **Implement structured logging** with correlation IDs
- [x] **Configure connection pooling** appropriately
- [x] **Use streaming** for large payloads
- [x] **Handle cancellation** properly
- [x] **Set appropriate timeouts**

### ? Anti-Patterns to Avoid
- ? Creating HttpClient with `new HttpClient()` (socket exhaustion)
- ? Not disposing HttpResponseMessage
- ? Ignoring cancellation tokens
- ? Custom retry logic without jitter (thundering herd)
- ? Blocking async calls with `.Result` or `.Wait()`

## ?? Which Pattern Should I Use?

### For New Applications
**Start with:** `PollyResilienceService` (Microsoft's recommended Polly approach)

### For Enterprise Applications
**Use:** `ProductionReadyHttpClientService` (Polly + Logging + Security + Monitoring)

### For High-Performance Scenarios
**Use:** `PerformanceOptimizationService` (Connection pooling + Streaming + Concurrency)

### For Clean Architecture
**Use:** Typed clients pattern (`HttpClientFactoryManagementService`)

### For Learning
**Explore:** All patterns, starting with custom handlers to understand concepts, then moving to Polly

## ?? Additional Resources

- [Microsoft: HttpClient Guidelines](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [Microsoft: Polly Integration](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Microsoft: Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/core/deploying/performance)

---

## ??? Project Structure

```
HttpClient/
??? Client/
?   ??? Services/              # 12 different HttpClient usage patterns
?   ??? MessageHandlers/       # Custom & educational handlers
?   ??? TypedClients/          # Strongly-typed client examples
?   ??? HttpClientServices.cs  # Configuration for all patterns
??? Client.Test/               # 64 comprehensive unit tests
??? API/                       # Test API server
??? README.md                  # This comprehensive guide
```

**This repository is your complete reference for HttpClient patterns in .NET 9!** ??