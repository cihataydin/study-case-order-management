namespace Shared.Middlewares
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents the request response logging middleware.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        private bool _useJsonFormat;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestResponseLoggingMiddleware"/> class.
        /// </summary>
        public RequestResponseLoggingMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the request response logging middleware.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true ||
                path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            _useJsonFormat = _configuration.GetValue<bool>("Logging:UseJsonFormat", false);
            await LogRequest(context);

            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
                await LogResponse(context);
            }
            finally
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task LogRequest(HttpContext context)
        {
            var request = context.Request;
            var requestBody = await GetRequestBody(request);

            object bodyObj = new { };
            if (!string.IsNullOrEmpty(requestBody))
            {
                try { bodyObj = JsonSerializer.Deserialize<object>(requestBody) ?? new { }; }
                catch { bodyObj = requestBody; }
            }

            var logObject = new
            {
                method = request.Method,
                path = request.Path,
                baseUrl = $"{request.Scheme}://{request.Host}",
                ip = context.Connection.RemoteIpAddress?.ToString(),
                body = bodyObj,
                headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                traceId = context.TraceIdentifier,
            };

            var contextPrefix = _useJsonFormat ? string.Empty : "[RequestLoggerMiddleware]";
            _logger.LogInformation("{Context} {Request}", contextPrefix, JsonSerializer.Serialize(logObject));
        }

        private async Task LogResponse(HttpContext context)
        {
            var response = context.Response;
            var responseBody = await GetResponseBody(response);

            object bodyObj = new { };
            if (!string.IsNullOrEmpty(responseBody))
            {
                try { bodyObj = JsonSerializer.Deserialize<object>(responseBody) ?? new { }; }
                catch { bodyObj = responseBody; }
            }

            var logObject = new
            {
                statusCode = response.StatusCode,
                responseBody = bodyObj,
                method = context.Request.Method,
                ip = context.Connection.RemoteIpAddress?.ToString(),
                path = context.Request.Path,
                baseUrl = $"{context.Request.Scheme}://{context.Request.Host}",
                body = new { },  // Response does not have a request body to log here
                headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                traceId = context.TraceIdentifier,
            };

            var contextPrefix = _useJsonFormat ? string.Empty : "[ResponseLoggerMiddleware]";
            _logger.LogInformation("{Context} {Response}", contextPrefix, JsonSerializer.Serialize(logObject));
        }

        private async Task<string> GetRequestBody(HttpRequest request)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }

        private async Task<string> GetResponseBody(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return body;
        }
    }
}
