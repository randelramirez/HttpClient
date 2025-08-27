using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Client.MessageHandlers
{
    /// <summary>
    /// 🎓 EDUCATIONAL ONLY - Custom retry policy implementation for learning purposes.
    /// 
    /// ⚠️  DO NOT USE IN PRODUCTION - Use Polly instead!
    /// 
    /// Microsoft's Recommendation: Use Polly's WaitAndRetryAsync policy
    /// https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
    /// 
    /// This implementation demonstrates basic retry concepts but lacks:
    /// - Exponential backoff with jitter (prevents thundering herd)
    /// - Proper exception handling for transient errors
    /// - Advanced retry conditions
    /// - Integration with Microsoft.Extensions.Http
    /// - Comprehensive logging and metrics
    /// 
    /// Use this for understanding how retries work internally,
    /// but always use Polly's WaitAndRetryAsync in production code.
    /// </summary>
    [Obsolete("Use Polly's WaitAndRetryAsync policy instead. This is for educational purposes only.", false)]
    public class RetryPolicyDelegatingHandler : DelegatingHandler
    {
        private readonly int maximumAmountOfRetries = 3;

        public RetryPolicyDelegatingHandler(int maximumAmountOfRetries)
            : base()
        {
            this.maximumAmountOfRetries = maximumAmountOfRetries;
        }

        public RetryPolicyDelegatingHandler(HttpMessageHandler innerHandler, int maximumAmountOfRetries) : 
            base(innerHandler)
        {
            this.maximumAmountOfRetries = maximumAmountOfRetries;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage? response = null;
            for (int i = 0; i < maximumAmountOfRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Retrying...");
                Console.ResetColor();
            }
            return response!;
        }
    }
}
