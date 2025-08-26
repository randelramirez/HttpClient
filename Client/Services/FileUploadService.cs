using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Client.Services
{
    public class FileUploadService : IService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public FileUploadService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task Run()
        {
            await UploadFileWithMultipartFormData();
            await UploadFileAsStream();
            await UploadWithProgressTracking();
        }

        private async Task UploadFileWithMultipartFormData()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Create a sample file content
            var fileContent = "This is a sample file content for testing upload.";
            var fileBytes = Encoding.UTF8.GetBytes(fileContent);

            using var content = new MultipartFormDataContent();
            using var fileStreamContent = new ByteArrayContent(fileBytes);
            
            fileStreamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileStreamContent, "file", "sample.txt");
            content.Add(new StringContent("Sample upload description"), "description");

            try
            {
                var response = await httpClient.PostAsync("api/files/upload", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("File uploaded successfully via multipart form data");
                    Console.WriteLine($"Server response: {responseContent}");
                }
                else
                {
                    Console.WriteLine($"File upload failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"File upload error: {ex.Message}");
            }
        }

        private async Task UploadFileAsStream()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Create a sample file stream
            var fileContent = "This is content uploaded as a stream.";
            var fileBytes = Encoding.UTF8.GetBytes(fileContent);
            
            using var stream = new MemoryStream(fileBytes);
            using var streamContent = new StreamContent(stream);
            
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            streamContent.Headers.ContentDisposition = 
                new ContentDispositionHeaderValue("attachment") { FileName = "stream-upload.txt" };

            try
            {
                var response = await httpClient.PostAsync("api/files/upload-stream", streamContent);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("File uploaded successfully as stream");
                }
                else
                {
                    Console.WriteLine($"Stream upload failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Stream upload error: {ex.Message}");
            }
        }

        private async Task UploadWithProgressTracking()
        {
            var httpClient = httpClientFactory.CreateClient("ContactsClient");

            // Create a larger sample file for progress tracking
            var fileContent = new string('A', 10000); // 10KB of 'A' characters
            var fileBytes = Encoding.UTF8.GetBytes(fileContent);

            var progressContent = new ProgressableStreamContent(new ByteArrayContent(fileBytes));
            progressContent.Progress += (sent, total) =>
            {
                var percentage = (double)sent / total * 100;
                Console.WriteLine($"Upload progress: {percentage:F1}%");
            };

            try
            {
                var response = await httpClient.PostAsync("api/files/upload-with-progress", progressContent);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("File uploaded successfully with progress tracking");
                }
                else
                {
                    Console.WriteLine($"Progress upload failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Progress upload error: {ex.Message}");
            }
        }
    }

    // Custom content class for progress tracking
    public class ProgressableStreamContent : HttpContent
    {
        private readonly HttpContent _innerContent;
        
        public event Action<long, long> Progress;

        public ProgressableStreamContent(HttpContent innerContent)
        {
            _innerContent = innerContent;
            
            foreach (var header in _innerContent.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            const int bufferSize = 4096;
            var buffer = new byte[bufferSize];

            using var innerStream = await _innerContent.ReadAsStreamAsync();
            var totalLength = innerStream.Length;
            var totalBytesRead = 0L;

            int bytesRead;
            while ((bytesRead = await innerStream.ReadAsync(buffer, 0, bufferSize)) != 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                Progress?.Invoke(totalBytesRead, totalLength);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _innerContent.Headers.ContentLength ?? 0;
            return _innerContent.Headers.ContentLength.HasValue;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerContent?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}