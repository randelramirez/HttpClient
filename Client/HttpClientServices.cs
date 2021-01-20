using Client.MessageHandlers;
using Client.TypedClients;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public static class HttpClientServices
    {
        public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
        {
            services.ConfigureNamedClient();
            services.ConfigureClientWithCustomHandler();
            services.ConfigureNamedClient();
            
            return services;
        }

        private static IServiceCollection ConfigureNamedClient(this IServiceCollection services)
        {
            // Using a named client
            services.AddHttpClient("ContactsClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = new TimeSpan(0, 0, 30);
                client.DefaultRequestHeaders.Clear();
            })
            .ConfigurePrimaryHttpMessageHandler(handler => new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
            });

            return services;
        }

        private static IServiceCollection ConfigureClientWithCustomHandler(this IServiceCollection services)
        {
            services.AddHttpClient("ContactsClientCustomHandler", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44354/");
                client.Timeout = new TimeSpan(0, 0, 30);
                client.DefaultRequestHeaders.Clear();
            })
            // We use 20 seconds at least for this demo so that we can differtiate timeout and cancelled exceptions 
            // (the httpclient have 30 seconds before timeout and then it throws a cancelled exception)
            .AddHttpMessageHandler(handler => new TimeOutDelegatingHandler(TimeSpan.FromSeconds(15)))
            .AddHttpMessageHandler(handler => new RetryPolicyDelegatingHandler(2))
            .ConfigurePrimaryHttpMessageHandler(handler =>
                new HttpClientHandler()
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip
                });

            return services;
        }

        private static IServiceCollection ConfigureTypedClient(this IServiceCollection services)
        {
            services.AddHttpClient<ContactsClient>()
            .ConfigurePrimaryHttpMessageHandler(handler =>
                 new HttpClientHandler()
                 {
                     AutomaticDecompression = System.Net.DecompressionMethods.GZip
                 });

            return services;
        }
    }
}
