# HttpClient .NET 9 Patterns

A .NET 9 solution collecting sixteen `HttpClient` usage patterns — named and
typed clients, `IHttpClientFactory`, Polly resilience, custom delegating
handlers, authentication, streaming, uploads, GraphQL, webhooks, and
concurrency — each as a runnable demonstration with tests.

The recurring theme is *the recommended way vs. the hand-rolled way*: several
patterns are implemented twice, once with Polly and once with custom handlers,
with the custom versions marked `[Obsolete]` and labelled educational.

## Projects

| Project       | What it is                                                    |
| ------------- | -------------------------------------------------------------- |
| `API`         | The contacts API the demonstrations call                        |
| `Client`      | Console app — runs one demonstration at a time                  |
| `Client.Test` | xUnit tests, 157 `[Fact]`/`[Theory]` cases                      |
| `Core`        | `Contact` and the view models                                   |
| `Persistence` | `DataContext` for the API                                       |

All five target **net9.0**.

## Build and test

```bash
dotnet restore HttpClient.sln
dotnet test HttpClient.sln
```

CI (`.github/workflows/ci.yml`) builds and tests on Ubuntu, Windows, and macOS;
`code-quality.yml` runs separately.

## Run the API

```bash
dotnet run --project API --launch-profile SelfHost
```

`SelfHost` is the only profile and listens on `https://localhost:5001`.

> ⚠️ **Port mismatch.** Every named client in `Client/HttpClientServices.cs` has
> its `BaseAddress` set to `https://localhost:44354/` — an IIS Express port that
> the `SelfHost` profile doesn't use. Before running the console client against
> the local API, change either the client registrations or the API's
> `applicationUrl` so they agree.

## Run a demonstration

`Client/Program.cs` picks the demonstration by which `IService` implementation is
registered. All sixteen are registered as concrete types; exactly one is also
registered as `IService`, and the rest sit as commented-out lines grouped by
theme. The checked-in selection is **`PollyResilienceService`**.

```bash
dotnet run --project Client
```

To run a different one, comment out the `PollyResilienceService` registration
and uncomment another.

### The sixteen demonstrations

**Basic**
- `CRUDService` — GET/POST/PUT/PATCH/DELETE against the contacts API
- `SampleService` — response-handling variations

**IHttpClientFactory**
- `HttpClientFactoryManagementService` — factory lifetimes and named clients
- `HttpCustomMessageHandlerService` — custom `DelegatingHandler`s in the pipeline

**Advanced**
- `AuthenticationService` — Bearer, Basic, and refresh-token flows
- `FileUploadService` — multipart, stream, and progress-reporting uploads
- `ErrorHandlingService` — fault handling and consistent error surfaces
- `ConcurrencyService` — parallel requests and throttling
- `CustomHeadersService` — per-request configuration
- `WebhookService` — webhook delivery and batch processing
- `GraphQLService` — GraphQL over `HttpClient`
- `StreamService` — streaming large payloads without buffering

**Hand-rolled (educational)**
- `ResiliencePatternService` — retry/circuit-breaker written by hand
- `PerformanceOptimizationService` — compression, pooling, connection limits
- `ProductionReadyHttpClientService` — headers, security, monitoring

**Recommended**
- `PollyResilienceService` — the Polly-based equivalent, and the default

## Client registrations

`HttpClientServices.AddHttpClientServices()` sets up five clients plus one
educational one:

| Client                        | Timeout | Policies / handlers                          | Notes                                   |
| ----------------------------- | ------- | -------------------------------------------- | --------------------------------------- |
| `ContactsClient` (named)      | 30s     | —                                            | GZip/Deflate decompression               |
| `PollyClient`                 | 100s    | Retry + circuit breaker + timeout            | `MaxConnectionsPerServer = 10`           |
| `ProductionClient`            | 100s    | Logging handler + retry + circuit breaker    | Adds `X-Client-Version`                  |
| `PerformanceClient`           | 15s     | —                                            | `MaxConnectionsPerServer = 50`, no proxy |
| `ContactsClient` (typed)      | —       | Logging handler + retry                      | Configured in the class constructor      |
| `ContactsClientCustomHandler` | 30s     | `TimeOutDelegatingHandler`, `RetryPolicyDelegatingHandler` | `[Obsolete]`, educational only |

The `HttpClient` timeout is deliberately longer than the Polly timeout policy on
the resilient clients — the outer `Timeout` has to outlast the whole
retry sequence, or it cancels the retries it was meant to allow.

### Polly policies

Defined once and shared across clients:

- **Retry** — `HandleTransientHttpError()` (which covers `HttpRequestException`,
  5xx, and 408), 3 attempts, exponential backoff `2^n` seconds plus 0–100 ms of
  jitter so simultaneous clients don't retry in lockstep.
- **Circuit breaker** — opens after 5 handled failures, stays open 30 seconds.
- **Timeout** — 10 seconds per attempt.

Order matters: `.AddPolicyHandler` wraps outermost-first, so retry sits outside
the circuit breaker, which sits outside the per-attempt timeout.

### Custom handlers

`Client/MessageHandlers/` holds the hand-written equivalents —
`RetryPolicyDelegatingHandler`, `TimeOutDelegatingHandler`,
`CircuitBreakerDelegatingHandler`, and `LoggingDelegatingHandler`. The first
three are `[Obsolete]` on purpose: they exist to show what Polly does for you,
not to be used. `LoggingDelegatingHandler` is not obsolete and is wired into the
production and typed clients.

## Tests

`Client.Test/` has one test class per service. `HandlersStub/` provides
`HttpMessageHandler` stubs that return fixed 200, 401, and 404 responses, so
the services can be exercised without a running API — which is why
`dotnet test` passes regardless of the port mismatch above.
