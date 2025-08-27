using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Client.MessageHandlers
{
    /// <summary>
    /// ?? EDUCATIONAL ONLY - Custom circuit breaker implementation for learning purposes.
    /// 
    /// ??  DO NOT USE IN PRODUCTION - Use Polly instead!
    /// 
    /// Microsoft's Recommendation: Use Polly's CircuitBreakerAsync policy
    /// https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
    /// 
    /// This implementation demonstrates the concepts behind circuit breakers but lacks:
    /// - Battle-tested reliability of Polly
    /// - Advanced features (metrics, monitoring)
    /// - Thread-safety optimizations
    /// - Integration with Microsoft.Extensions.Http
    /// - Proper state transitions in concurrent scenarios
    /// 
    /// Use this for understanding how circuit breakers work internally,
    /// but always use Polly's CircuitBreakerAsync in production code.
    /// </summary>
    [Obsolete("Use Polly's CircuitBreakerAsync policy instead. This is for educational purposes only.", false)]
    public class CircuitBreakerDelegatingHandler : DelegatingHandler
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitBreakerState _state = CircuitBreakerState.Closed;

        public CircuitBreakerDelegatingHandler(int failureThreshold = 5, TimeSpan? timeout = null)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout ?? TimeSpan.FromMinutes(1);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_state == CircuitBreakerState.Open)
            {
                // Check if we should attempt to close the circuit
                if (DateTime.UtcNow - _lastFailureTime > _timeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
            }

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                
                // Check if the response indicates success
                if (response.IsSuccessStatusCode)
                {
                    OnSuccess();
                    return response;
                }
                else
                {
                    OnFailure();
                    return response;
                }
            }
            catch (Exception)
            {
                OnFailure();
                throw;
            }
        }

        private void OnSuccess()
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }

        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
            }
        }
    }

    public enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
}