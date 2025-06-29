using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using Serilog.Enrichers.Span;
using System.Diagnostics;

namespace JwtApi.Monitoring;

/// <summary>
/// Structured logging configuration with ELK stack integration
/// </summary>
public static class StructuredLogging
{
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment.EnvironmentName;
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "jwt-api-dotnet";
        var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("PodName", podName)
            .Enrich.WithProperty("Environment", environment)
            .Enrich.With<OpenTelemetryTraceEnricher>()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                "{Properties:j} {NewLine}{Exception}")
            .WriteTo.File("logs/jwt-api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: 
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] " +
                "{Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        // Configure Elasticsearch if endpoint is provided
        var elasticsearchUrl = builder.Configuration["Elasticsearch:Url"];
        if (!string.IsNullOrEmpty(elasticsearchUrl))
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("PodName", podName)
                .Enrich.WithProperty("Environment", environment)
                .Enrich.With<OpenTelemetryTraceEnricher>()
                .WriteTo.Console()
                .WriteTo.File("logs/jwt-api-.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
                {
                    IndexFormat = $"jwt-api-dotnet-logs-{DateTime.UtcNow:yyyy-MM}",
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    TypeName = null,
                    BatchAction = ElasticOpType.Index,
                    CustomFormatter = new ElasticsearchJsonFormatter(),
                    EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog,
                    QueueSizeLimit = 5000,
                    Period = TimeSpan.FromSeconds(2),
                    InlineFields = true,
                    MinimumLogEventLevel = LogEventLevel.Information
                })
                .CreateLogger();

            Log.Information("📊 Elasticsearch logging configured: {ElasticsearchUrl}", elasticsearchUrl);
        }

        builder.Host.UseSerilog();
        return builder;
    }

    /// <summary>
    /// Add structured logging middleware
    /// </summary>
    public static IApplicationBuilder UseStructuredLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) => GetLogLevel(httpContext, elapsed, ex);
            options.EnrichDiagnosticContext = EnrichFromRequest;
        });

        return app;
    }

    private static LogEventLevel GetLogLevel(HttpContext httpContext, double elapsed, Exception? ex)
    {
        if (ex != null)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 500)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 400)
            return LogEventLevel.Warning;

        if (elapsed > 5000) // More than 5 seconds
            return LogEventLevel.Warning;

        return LogEventLevel.Information;
    }

    private static void EnrichFromRequest(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        // Basic request information
        diagnosticContext.Set("RequestHost", request.Host.Value);
        diagnosticContext.Set("RequestScheme", request.Scheme);
        diagnosticContext.Set("RequestProtocol", request.Protocol);
        diagnosticContext.Set("RequestContentType", request.ContentType);
        diagnosticContext.Set("RequestContentLength", request.ContentLength);
        diagnosticContext.Set("ResponseContentType", response.ContentType);
        diagnosticContext.Set("ResponseContentLength", response.ContentLength);

        // Client information
        var clientIp = GetClientIpAddress(httpContext);
        diagnosticContext.Set("ClientIP", clientIp);
        diagnosticContext.Set("UserAgent", request.Headers.UserAgent.FirstOrDefault());

        // User context
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("id")?.Value);
            diagnosticContext.Set("Username", httpContext.User.FindFirst("username")?.Value);
        }

        // Request ID for tracing
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);

        // Pod information for Kubernetes
        var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
        var podIp = Environment.GetEnvironmentVariable("POD_IP") ?? "127.0.0.1";
        diagnosticContext.Set("PodName", podName);
        diagnosticContext.Set("PodIP", podIp);

        // Route information
        var endpoint = httpContext.GetEndpoint();
        if (endpoint != null)
        {
            diagnosticContext.Set("EndpointName", endpoint.DisplayName);
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                diagnosticContext.Set("RoutePattern", routeEndpoint.RoutePattern.RawText);
            }
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP headers first
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstIp = forwardedFor.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstIp))
            {
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

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Enricher to add OpenTelemetry trace context to logs
/// </summary>
public class OpenTelemetryTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
            
            // Add baggage items if any
            foreach (var baggage in activity.Baggage)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty($"Baggage.{baggage.Key}", baggage.Value));
            }
        }
    }
}

/// <summary>
/// Static methods for structured logging in business logic
/// </summary>
public static class LoggerExtensions
{
    public static void LogAuthenticationAttempt(this ILogger logger, string authType, bool success, 
        string? userId = null, string? clientIp = null)
    {
        using (LogContext.PushProperty("AuthType", authType))
        using (LogContext.PushProperty("AuthSuccess", success))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("ClientIP", clientIp))
        {
            if (success)
            {
                logger.LogInformation("Authentication successful for {AuthType}", authType);
            }
            else
            {
                logger.LogWarning("Authentication failed for {AuthType}", authType);
            }
        }
    }

    public static void LogDatabaseOperation(this ILogger logger, string operation, string table, 
        bool success, double duration, Exception? exception = null)
    {
        using (LogContext.PushProperty("DbOperation", operation))
        using (LogContext.PushProperty("DbTable", table))
        using (LogContext.PushProperty("DbSuccess", success))
        using (LogContext.PushProperty("DbDuration", duration))
        {
            if (success)
            {
                logger.LogDebug("Database operation {DbOperation} on {DbTable} completed in {DbDuration}ms", 
                    operation, table, duration);
            }
            else
            {
                logger.LogError(exception, "Database operation {DbOperation} on {DbTable} failed after {DbDuration}ms", 
                    operation, table, duration);
            }
        }
    }

    public static void LogCacheOperation(this ILogger logger, string operation, string key, 
        bool hit, double duration)
    {
        using (LogContext.PushProperty("CacheOperation", operation))
        using (LogContext.PushProperty("CacheKey", key))
        using (LogContext.PushProperty("CacheHit", hit))
        using (LogContext.PushProperty("CacheDuration", duration))
        {
            logger.LogDebug("Cache {CacheOperation} for key {CacheKey}: {CacheResult} in {CacheDuration}ms", 
                operation, key, hit ? "HIT" : "MISS", duration);
        }
    }

    public static void LogMemoryOptimization(this ILogger logger, string poolType, int utilized, int total)
    {
        using (LogContext.PushProperty("MemoryPoolType", poolType))
        using (LogContext.PushProperty("MemoryUtilized", utilized))
        using (LogContext.PushProperty("MemoryTotal", total))
        {
            var utilizationPercent = total > 0 ? (utilized * 100.0 / total) : 0;
            logger.LogDebug("Memory pool {MemoryPoolType} utilization: {Utilized}/{Total} ({UtilizationPercent:F1}%)", 
                poolType, utilized, total, utilizationPercent);
        }
    }

    public static void LogBusinessEvent(this ILogger logger, string eventName, object? eventData = null)
    {
        using (LogContext.PushProperty("BusinessEvent", eventName))
        using (LogContext.PushProperty("EventData", eventData, destructureObjects: true))
        {
            logger.LogInformation("Business event: {BusinessEvent}", eventName);
        }
    }

    public static void LogSecurityEvent(this ILogger logger, string eventType, string severity, 
        string? clientIp = null, object? details = null)
    {
        using (LogContext.PushProperty("SecurityEventType", eventType))
        using (LogContext.PushProperty("SecuritySeverity", severity))
        using (LogContext.PushProperty("ClientIP", clientIp))
        using (LogContext.PushProperty("SecurityDetails", details, destructureObjects: true))
        {
            logger.LogWarning("Security event: {SecurityEventType} (severity: {SecuritySeverity})", 
                eventType, severity);
        }
    }

    public static void LogPerformanceMetric(this ILogger logger, string metric, double value, 
        string unit, object? context = null)
    {
        using (LogContext.PushProperty("PerformanceMetric", metric))
        using (LogContext.PushProperty("PerformanceValue", value))
        using (LogContext.PushProperty("PerformanceUnit", unit))
        using (LogContext.PushProperty("PerformanceContext", context, destructureObjects: true))
        {
            logger.LogInformation("Performance metric {PerformanceMetric}: {PerformanceValue} {PerformanceUnit}", 
                metric, value, unit);
        }
    }
}