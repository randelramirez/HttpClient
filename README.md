# HttpClient .NET 9 Examples - Complete Guide

[![CI](https://github.com/randelramirez/HttpClient/actions/workflows/ci.yml/badge.svg)](https://github.com/randelramirez/HttpClient/actions/workflows/ci.yml)
[![Code Quality](https://github.com/randelramirez/HttpClient/actions/workflows/code-quality.yml/badge.svg)](https://github.com/randelramirez/HttpClient/actions/workflows/code-quality.yml)

A comprehensive collection of HttpClient usage examples and patterns in .NET 9, demonstrating various scenarios from basic CRUD operations to advanced patterns like authentication, file uploads, concurrent requests, and more. This repository implements **Microsoft's official HttpClient best practices** including Polly for resilience patterns.

## ?? Overview

This project showcases every major HttpClient pattern recommended by Microsoft, from basic usage to enterprise-grade implementations with resilience, performance optimization, and production-ready monitoring.

## ?? Table of Contents

- [?? Quick Start](#-quick-start)
- [??? Project Structure](#?-project-structure)
- [?? HttpClient Approaches & Patterns](#-httpclient-approaches--patterns)
- [?? Microsoft's Recommended Patterns](#-microsofts-recommended-patterns)
- [?? Service Examples](#-service-examples)
- [?? Testing](#-testing)
- [?? Configuration](#?-configuration)
- [?? CI/CD](#-cicd)
- [?? Performance Tips](#-performance-tips)

## ?? Quick Start

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 or VS Code

### Running the Examples
```bash
# Clone the repository
git clone https://github.com/randelramirez/HttpClient.git
cd HttpClient

# Restore dependencies
dotnet restore

# Run the demo (currently: Polly Resilience Patterns)
dotnet run --project Client

# Run tests
dotnet test

# Run the API server (for testing)
dotnet run --project API
```

## ??? Project Structure

```
HttpClient/
??? .github/workflows/         # CI/CD pipelines
??? API/                       # Web API server for testing
??? Client/                    # HttpClient examples and services
?   ??? Services/             # Various HttpClient usage scenarios
?   ??? MessageHandlers/      # Custom and educational handlers
?   ??? TypedClients/         # Strongly-typed client examples
?   ??? appsettings.json      # Configuration examples
??? Client.Test/              # Comprehensive unit tests (52+ tests)
??? Core/                     # Shared models and DTOs
??? Persistence/             # Data access layer
```

## ?? HttpClient Approaches & Patterns

This repository demonstrates **5 different approaches** to using HttpClient, following Microsoft's recommendations:

### 1. ?? **Polly with HttpClientFactory (RECOMMENDED)**

**Microsoft's Official Recommendation** - Use Polly for all resilience patterns.

```csharp
// Configuration (in HttpClientServices.cs)
services.AddHttpClient("PollyClient")
    .AddPolicyHandler(GetRetryPolicy())        // Exponential backoff with jitter
    .AddPolicyHandler(GetCircuitBreakerPolicy()) // Fault tolerance
    .AddPolicyHandler(GetTimeoutPolicy());     // Timeout protection

// Usage (in any service)
public async Task<List<Contact>> GetContacts()
{
    var httpClient = _httpClientFactory.CreateClient("PollyClient");
    var response = await httpClient.GetAsync("api/contacts"); // Polly policies applied automatically!
    // ... handle response
}
```

**? Benefits:**
- Zero service code changes - policies applied transparently
- Battle-tested by thousands of applications
- Microsoft supported and documented
- Comprehensive feature set (retry, circuit breaker, timeout, bulkhead, rate limiting)

**?? Service:** `PollyResilienceService` - **Currently Active**

---

### 2. ??? **Production-Ready HttpClientFactory**

**Enterprise-grade configuration** with logging, monitoring, and Polly integration.

```csharp
// Configuration
services.AddHttpClient("ProductionClient")
    .AddHttpMessageHandler<LoggingDelegatingHandler>() // Structured logging
    .AddPolicyHandler(GetRetryPolicy())                // Polly resilience
    .AddPolicyHandler(GetCircuitBreakerPolicy())
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        UseCookies = false,
        MaxConnectionsPerServer = 10
    });

// Usage
var httpClient = _httpClientFactory.CreateClient("ProductionClient");
// Automatic: Logging + Retry + Circuit Breaker + Performance optimization
```

**? Benefits:**
- Comprehensive logging with correlation IDs
- Performance optimizations
- Security headers and authentication
- Health check patterns
- Configuration-driven setup

**?? Service:** `ProductionReadyHttpClientService`

---

### 3. ? **Performance-Optimized HttpClientFactory**

**High-throughput scenarios** with connection pooling and streaming.

```csharp
// Configuration for high-performance scenarios
services.AddHttpClient("PerformanceClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        MaxConnectionsPerServer = 50,    // Higher connection limit
        UseCookies = false,             // Disable if not needed
        UseProxy = false,               // Disable if not required
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

// Usage with streaming for large payloads
using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
using var stream = await response.Content.ReadAsStreamAsync();
// Process stream directly without loading into memory
```

**? Benefits:**
- Optimized connection pooling
- Streaming for large payloads
- Compression handling
- Concurrent request throttling

**?? Service:** `PerformanceOptimizationService`

---

### 4. ?? **Typed Clients**

**Strongly-typed service clients** following Microsoft's typed client pattern.

```csharp
// Typed client implementation
public class ContactsClient
{
    private readonly HttpClient _client;

    public ContactsClient(HttpClient client)
    {
        _client = client;
        _client.BaseAddress = new Uri("https://localhost:44354/");
    }

    public async Task<IEnumerable<ContactViewModel>> GetContacts()
    {
        var response = await _client.GetAsync("api/contacts");
        // ... handle response
    }
}

// Configuration
services.AddHttpClient<ContactsClient>()
    .AddPolicyHandler(GetRetryPolicy());

// Usage (via dependency injection)
public class MyService
{
    private readonly ContactsClient _contactsClient;
    
    public MyService(ContactsClient contactsClient)
    {
        _contactsClient = contactsClient;
    }
}
```

**? Benefits:**
- Strongly-typed interface
- Encapsulated HTTP logic
- Easy to mock for testing
- Dependency injection integration

**?? Service:** `HttpClientFactoryManagementService`

---

### 5. ?? **Educational Custom Handlers (LEARNING ONLY)**

**?? NOT FOR PRODUCTION** - Custom implementations for understanding concepts.

```csharp
// Educational custom circuit breaker (DON'T USE IN PRODUCTION)
[Obsolete("Use Polly's CircuitBreakerAsync policy instead. This is for educational purposes only.")]
public class CircuitBreakerDelegatingHandler : DelegatingHandler
{
    // Custom implementation to understand circuit breaker concepts
    // Use Polly in production!
}

// Configuration (educational only)
services.AddHttpClient("EducationalClient")
    .AddHttpMessageHandler(() => new RetryPolicyDelegatingHandler(2))
    .AddHttpMessageHandler(() => new CircuitBreakerDelegatingHandler(5));
```

**?? Learning Value:**
- Understand how circuit breakers work internally
- Learn retry pattern concepts
- See what Polly does behind the scenes

**?? Service:** `ResiliencePatternService`

---

## ?? Microsoft's Recommended Patterns

Based on [Microsoft's official documentation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly):

### ? **Production Checklist**
- [x] **Use HttpClientFactory** instead of `new HttpClient()`
- [x] **Use Polly for resilience** (retry, circuit breaker, timeout)
- [x] **Configure appropriate timeouts**
- [x] **Enable compression** for bandwidth optimization
- [x] **Implement structured logging** with correlation IDs
- [x] **Use streaming** for large payloads
- [x] **Configure connection pooling**
- [x] **Handle timeout and cancellation** properly
- [x] **Use configuration-based setup**
- [x] **Implement health check patterns**
- [x] **Follow semantic versioning** for User-Agent headers

### ?? **Recommended Configuration**
```csharp
// Microsoft's recommended production setup
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
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)))
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    UseCookies = false,
    MaxConnectionsPerServer = 10
});
```

## ?? Service Examples

### Core HttpClient Services

| Service | Pattern | Status | Description |
|---------|---------|--------|-------------|
| **PollyResilienceService** | ?? Polly + HttpClientFactory | **Active** | Microsoft's official resilience patterns |
| **ProductionReadyHttpClientService** | ??? Enterprise | Ready | Logging + Security + Monitoring |
| **PerformanceOptimizationService** | ? Performance | Ready | High-throughput optimization |
| **AuthenticationService** | ?? Security | Ready | Bearer/Basic auth + token refresh |
| **FileUploadService** | ?? Upload | Ready | Multipart, stream, progress tracking |
| **CRUDService** | ?? Basic | Ready | Complete CRUD operations |
| **ConcurrencyService** | ? Concurrent | Ready | Parallel requests + throttling |
| **ErrorHandlingService** | ?? Error Handling | Ready | Timeout, cancellation, fault tolerance |
| **StreamService** | ?? Streaming | Ready | Large payload processing |
| **WebhookService** | ?? Webhooks | Ready | Signed webhooks + batch processing |
| **GraphQLService** | ?? GraphQL | Ready | Query execution + mutations |
| **CustomHeadersService** | ??? Headers | Ready | Custom headers + cache control |

### Switching Between Examples

In `Client/Program.cs`, uncomment the desired service:

```csharp
#region Microsoft Recommended (Polly)
// ?? RECOMMENDED: Microsoft's official Polly resilience patterns
serviceCollection.AddScoped<IService, PollyResilienceService>(); // Currently active

// ??? Production-ready patterns (Headers, Security, Monitoring)
//serviceCollection.AddScoped<IService, ProductionReadyHttpClientService>();

// ? Performance optimization patterns
//serviceCollection.AddScoped<IService, PerformanceOptimizationService>();
#endregion

#region Advanced Patterns
// ?? Authentication patterns (Bearer, Basic, Refresh tokens)
//serviceCollection.AddScoped<IService, AuthenticationService>();

// ?? File upload scenarios (Multipart, Stream, Progress)
//serviceCollection.AddScoped<IService, FileUploadService>();
#endregion
```

## ?? Testing

### Comprehensive Test Suite
- **52+ unit tests** with xUnit v3
- **100% pattern coverage** including edge cases
- **Moq integration** for HTTP mocking
- **Async testing patterns**

### Test Categories
```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=HttpClient"
dotnet test --filter "Category=Polly"
dotnet test --filter "Category=Authentication"
```

### Example Test Pattern
```csharp
[Fact]
public async Task PollyClient_WithTransientErrors_RetriesAutomatically()
{
    // Arrange
    _mockMessageHandler
        .Protected()
        .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

    // Act
    var result = await _pollyService.Run();

    // Assert
    // Verify Polly retried 3 times and eventually succeeded
    _mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(3), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
}
```

## ?? Configuration

### appsettings.json Examples
```json
{
  "HttpClientSettings": {
    "PollyClient": {
      "BaseAddress": "https://api.example.com/",
      "Timeout": "00:01:40",
      "RetryPolicy": {
        "RetryCount": 3,
        "BaseDelaySeconds": 1
      },
      "CircuitBreaker": {
        "FailureThreshold": 5,
        "DurationOfBreakSeconds": 30
      }
    },
    "ProductionClient": {
      "BaseAddress": "https://api.example.com/",
      "EnableLogging": true,
      "EnablePolly": true,
      "MaxConnectionsPerServer": 10
    }
  }
}
```

### Environment-Specific Configuration
```csharp
// Different configurations for different environments
services.AddHttpClient("ApiClient", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    
    client.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(environment.IsProduction() ? 60 : 30);
});
```

## ?? CI/CD

### GitHub Actions Pipeline
- ? **Multi-OS builds** (Ubuntu, Windows, macOS)
- ? **All tests pass** (52+/52+)
- ? **Code quality checks** (formatting, analysis)
- ? **Security scanning** (vulnerability detection)
- ? **Code coverage** collection and reporting

### Quality Gates
```yaml
# .github/workflows/ci.yml
- name: Build and Test
  run: |
    dotnet restore
    dotnet build --no-restore --configuration Release
    dotnet test --no-build --configuration Release
```

## ?? Performance Tips

### 1. **Connection Management**
```csharp
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    MaxConnectionsPerServer = 50,          // Higher for performance
    UseCookies = false,                    // Disable if not needed
    UseProxy = false,                      // Disable if not required
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
});
```

### 2. **Streaming for Large Payloads**
```csharp
// Memory-efficient processing
using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
using var stream = await response.Content.ReadAsStreamAsync();
// Process stream directly
```

### 3. **Concurrent Request Throttling**
```csharp
var semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
await semaphore.WaitAsync();
try
{
    // Make HTTP request
}
finally
{
    semaphore.Release();
}
```

### 4. **Polly Policies with Jitter**
```csharp
// Prevents thundering herd problem
.WaitAndRetryAsync(3, retryAttempt => 
    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + 
    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)))
```

## ?? Security Best Practices

### Authentication Patterns
```csharp
// Bearer Token
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

// API Key
request.Headers.Add("X-API-Key", apiKey);

// Custom Authentication
request.Headers.Authorization = new AuthenticationHeaderValue("CustomScheme", customToken);
```

### Security Headers
```csharp
request.Headers.Add("X-Requested-With", "HttpClient");
request.Headers.Add("X-Correlation-ID", correlationId);
request.Headers.Add("X-Client-Version", "1.0.0");
```

## ?? Monitoring and Observability

### Correlation ID Propagation
```csharp
var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
request.Headers.Add("X-Correlation-ID", correlationId);

// OpenTelemetry integration
if (Activity.Current != null)
{
    request.Headers.Add("traceparent", Activity.Current.Id);
    request.Headers.Add("tracestate", Activity.Current.TraceStateString);
}
```

### Structured Logging
```csharp
_logger.LogInformation("HTTP {Method} {Uri} [{RequestId}] completed with {StatusCode} in {ElapsedMs}ms",
    request.Method, request.RequestUri, requestId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
```

## ?? Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

**CI/CD Process**: The GitHub Actions pipeline will automatically:
- Build your changes across Ubuntu, Windows, and macOS
- Run all 52+ unit tests
- Verify code formatting and quality
- Check for security vulnerabilities
- Generate code coverage reports

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ?? Summary

This repository provides a **complete reference implementation** of Microsoft's HttpClient best practices for .NET 9:

- **?? Production-Ready**: Polly integration following Microsoft's official recommendations
- **?? Educational**: Custom implementations for learning purposes  
- **?? Comprehensive Testing**: 52+ unit tests covering all patterns
- **?? Complete Documentation**: Every pattern explained with examples
- **?? CI/CD Ready**: Automated testing and quality gates
- **? Performance Optimized**: High-throughput and streaming examples
- **??? Enterprise-Grade**: Security, logging, and monitoring patterns

**Start with `PollyResilienceService`** for Microsoft's recommended approach, then explore other patterns based on your needs! ??

## ?? Additional Resources

- [Microsoft HttpClient Guidelines](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [HttpClientFactory Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)
- [Microsoft Cloud Design Patterns](https://docs.microsoft.com/en-us/azure/architecture/patterns/)
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/core/deploying/performance)