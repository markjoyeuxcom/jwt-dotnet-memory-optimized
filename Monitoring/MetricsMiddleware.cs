using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace JwtApi.Monitoring;

/// <summary>
/// Middleware for collecting HTTP metrics with memory-optimized string operations
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PrometheusMetrics _metrics;
    private readonly ILogger<MetricsMiddleware> _logger;
    
    public MetricsMiddleware(RequestDelegate next, PrometheusMetrics metrics, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        
        // Track request size (using memory-optimized approach)
        long requestSize = 0;
        if (context.Request.ContentLength.HasValue)
        {
            requestSize = context.Request.ContentLength.Value;
        }
        else if (context.Request.Headers.TryGetValue("Content-Length", out StringValues contentLength))
        {
            long.TryParse(contentLength.FirstOrDefault(), out requestSize);
        }

        // Track session affinity for load balancing analysis
        var clientIp = GetClientIpAddress(context);
        _metrics.RecordSessionAffinityRequest(clientIp);

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;
        long responseSize = 0;

        try
        {
            // Use memory stream to capture response size without affecting performance
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            await _next(context);

            stopwatch.Stop();
            responseSize = responseBodyStream.Length;

            // Copy response back to original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);

            // Record metrics
            var duration = stopwatch.Elapsed.TotalSeconds;
            var route = GetRouteTemplate(context);
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;

            _metrics.RecordHttpRequest(method, route, statusCode, duration, requestSize, responseSize);

            // Log performance warnings for slow requests
            if (duration > 1.0) // More than 1 second
            {
                _logger.LogWarning("Slow request detected: {Method} {Route} took {Duration:F3}s",
                    method, route, duration);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            context.Response.Body = originalBodyStream;

            var route = GetRouteTemplate(context);
            _metrics.RecordException(ex.GetType().Name, route);
            
            _logger.LogError(ex, "Error processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);
            
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// Extract client IP address using memory-optimized string operations
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP headers first (common in load balancer scenarios)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstIp = forwardedFor.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstIp))
            {
                // Take only the first IP if there are multiple (memory efficient)
                var commaIndex = firstIp.IndexOf(',');
                return commaIndex > 0 ? firstIp.Substring(0, commaIndex).Trim() : firstIp.Trim();
            }
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            var ip = realIp.FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Get route template for metrics (memory-optimized)
    /// </summary>
    private static string GetRouteTemplate(HttpContext context)
    {
        // Try to get the route template for more accurate metrics
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return routeEndpoint.RoutePattern.RawText ?? context.Request.Path.Value ?? "unknown";
        }

        // Fallback to path, but normalize to avoid high cardinality
        var path = context.Request.Path.Value ?? "unknown";
        
        // Normalize paths to avoid metric explosion (memory-efficient approach)
        if (path.StartsWith("/api/"))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return $"/{segments[0]}/{segments[1]}";
            }
        }

        return path;
    }
}

/// <summary>
/// Extension methods for registering metrics middleware
/// </summary>
public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseMetricsCollection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}