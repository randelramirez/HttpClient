using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Client.MessageHandlers
{
    /// <summary>
    /// Logging handler that demonstrates Microsoft's recommended approach for logging HTTP requests/responses.
    /// Based on Microsoft.Extensions.Http.Logging patterns.
    /// </summary>
    public class LoggingDelegatingHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingDelegatingHandler> _logger;

        public LoggingDelegatingHandler(ILogger<LoggingDelegatingHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid();
            
            // Log outgoing request
            _logger.LogInformation(
                "HTTP {Method} {Uri} [{RequestId}] started",
                request.Method,
                request.RequestUri,
                requestId);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "HTTP {Method} {Uri} [{RequestId}] headers: {Headers}",
                    request.Method,
                    request.RequestUri,
                    requestId,
                    request.Headers);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                
                stopwatch.Stop();
                
                // Log response
                _logger.LogInformation(
                    "HTTP {Method} {Uri} [{RequestId}] completed with {StatusCode} in {ElapsedMs}ms",
                    request.Method,
                    request.RequestUri,
                    requestId,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "HTTP {Method} {Uri} [{RequestId}] failed with {StatusCode} {ReasonPhrase}",
                        request.Method,
                        request.RequestUri,
                        requestId,
                        (int)response.StatusCode,
                        response.ReasonPhrase);
                }

                return response;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                stopwatch.Stop();
                _logger.LogError(
                    "HTTP {Method} {Uri} [{RequestId}] timed out after {ElapsedMs}ms",
                    request.Method,
                    request.RequestUri,
                    requestId,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "HTTP {Method} {Uri} [{RequestId}] failed after {ElapsedMs}ms",
                    request.Method,
                    request.RequestUri,
                    requestId,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}