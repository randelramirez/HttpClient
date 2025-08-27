using Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Client.Services
{
    /// <summary>
    /// Common utilities for consistent error handling and content processing across all services
    /// </summary>
    public static class HttpResponseHelper
    {
        /// <summary>
        /// Safely deserialize HTTP response content with proper error handling
        /// </summary>
        public static async Task<T?> SafeDeserializeAsync<T>(HttpResponseMessage response) where T : class
        {
            try
            {
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                
                return contentType switch
                {
                    "application/json" => SafeJsonDeserialize<T>(content),
                    "application/xml" or "text/xml" => SafeXmlDeserialize<T>(content),
                    _ => null // Unsupported content type
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deserialization error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Safely deserialize JSON with validation
        /// </summary>
        public static T? SafeJsonDeserialize<T>(string content) where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content) || !IsValidJsonStart(content))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<T>(content);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON deserialization error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Safely deserialize XML with validation
        /// </summary>
        public static T? SafeXmlDeserialize<T>(string content) where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                var serializer = new XmlSerializer(typeof(T));
                using var reader = new StringReader(content);
                return (T?)serializer.Deserialize(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"XML deserialization error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Quick validation of JSON content start
        /// </summary>
        private static bool IsValidJsonStart(string content)
        {
            var trimmed = content.Trim();
            return trimmed.StartsWith("{") || trimmed.StartsWith("[");
        }

        /// <summary>
        /// Handle common HTTP error scenarios consistently
        /// </summary>
        public static bool HandleCommonErrors(HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.NotFound:
                    Console.WriteLine("The requested resource was not found.");
                    return true; // Handled gracefully

                case System.Net.HttpStatusCode.Unauthorized:
                    Console.WriteLine("Unauthorized access - authentication required.");
                    return true; // Handled gracefully

                case System.Net.HttpStatusCode.Forbidden:
                    Console.WriteLine("Access forbidden - insufficient permissions.");
                    return true; // Handled gracefully

                case System.Net.HttpStatusCode.RequestTimeout:
                    Console.WriteLine("Request timed out.");
                    return true; // Handled gracefully

                case System.Net.HttpStatusCode.TooManyRequests:
                    Console.WriteLine("Rate limit exceeded - too many requests.");
                    return true; // Handled gracefully

                case System.Net.HttpStatusCode.InternalServerError:
                case System.Net.HttpStatusCode.BadGateway:
                case System.Net.HttpStatusCode.ServiceUnavailable:
                case System.Net.HttpStatusCode.GatewayTimeout:
                    Console.WriteLine($"Server error: {response.StatusCode} - {response.ReasonPhrase}");
                    return false; // Let retry logic handle these

                default:
                    return false; // Not handled - let calling code decide
            }
        }
    }
}
