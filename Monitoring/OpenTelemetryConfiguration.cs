using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using System.Diagnostics;

namespace JwtApi.Monitoring;

/// <summary>
/// OpenTelemetry configuration for comprehensive observability
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static readonly ActivitySource ActivitySource = new("JwtApi.Activities");
    
    public static WebApplicationBuilder AddOpenTelemetry(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "jwt-api-dotnet";
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var environment = builder.Environment.EnvironmentName;
        var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;

        // Configure resource attributes
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment,
                ["service.instance.id"] = podName,
                ["service.namespace"] = "jwt-demo",
                ["pod.name"] = podName,
                ["k8s.pod.name"] = podName,
                ["k8s.namespace.name"] = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "jwt-demo",
                ["container.name"] = "jwt-api-dotnet",
                ["framework"] = "aspnetcore",
                ["language"] = "dotnet"
            });

        // Configure tracing
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(ActivitySource.Name)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnableGrpcAspNetCoreSupport = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", GetClientIpAddress(request));
                            activity.SetTag("http.user_agent", request.Headers.UserAgent.ToString());
                            activity.SetTag("pod.name", podName);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.size", response.ContentLength);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.type", exception.GetType().Name);
                            activity.SetTag("exception.message", exception.Message);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.size", request.Content?.Headers?.ContentLength);
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.size", response.Content?.Headers?.ContentLength);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.operation", GetDbOperation(command.CommandText));
                        };
                    });

                // Add exporters based on configuration
                ConfigureTraceExporters(tracingBuilder, builder.Configuration);
            })
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                // Add Prometheus exporter if enabled
                if (builder.Configuration.GetValue<bool>("Prometheus:Enabled", true))
                {
                    metricsBuilder.AddPrometheusExporter();
                }
            });

        // Configure logging with OpenTelemetry
        builder.Logging.AddOpenTelemetry(loggingBuilder =>
        {
            loggingBuilder.SetResourceBuilder(resourceBuilder);
            ConfigureLogExporters(loggingBuilder, builder.Configuration);
        });

        builder.Services.AddSingleton(ActivitySource);

        return builder;
    }

    private static void ConfigureTraceExporters(TracerProviderBuilder tracingBuilder, IConfiguration configuration)
    {
        // Jaeger exporter
        var jaegerEndpoint = configuration["OpenTelemetry:Jaeger:Endpoint"];
        if (!string.IsNullOrEmpty(jaegerEndpoint))
        {
            tracingBuilder.AddJaegerExporter(options =>
            {
                options.Endpoint = new Uri(jaegerEndpoint);
                options.Protocol = OpenTelemetry.Exporter.JaegerExportProtocol.HttpBinaryThrift;
            });
        }

        // OTLP exporter for SigNoz/Grafana
        var otlpEndpoint = configuration["OpenTelemetry:OTLP:Endpoint"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracingBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                
                var authHeader = configuration["OpenTelemetry:OTLP:Headers:Authorization"];
                if (!string.IsNullOrEmpty(authHeader))
                {
                    options.Headers = $"Authorization={authHeader}";
                }
            });
        }

        // Console exporter for development
        if (configuration.GetValue<bool>("OpenTelemetry:Console:Enabled", false))
        {
            tracingBuilder.AddConsoleExporter();
        }
    }

    private static void ConfigureLogExporters(OpenTelemetryLoggerOptions loggingBuilder, IConfiguration configuration)
    {
        // OTLP exporter for logs
        var otlpEndpoint = configuration["OpenTelemetry:OTLP:Endpoint"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            loggingBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
        }

        // Console exporter for development
        if (configuration.GetValue<bool>("OpenTelemetry:Console:Enabled", false))
        {
            loggingBuilder.AddConsoleExporter();
        }
    }

    /// <summary>
    /// Create custom activity for business operations
    /// </summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    /// <summary>
    /// Create custom activity with tags
    /// </summary>
    public static Activity? StartActivity(string name, Dictionary<string, object?> tags, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = ActivitySource.StartActivity(name, kind);
        if (activity != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
        return activity;
    }

    /// <summary>
    /// Extract client IP from HTTP request (memory optimized)
    /// </summary>
    private static string GetClientIpAddress(HttpRequest request)
    {
        // Check forwarded headers first
        if (request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstIp = forwardedFor.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstIp))
            {
                var commaIndex = firstIp.IndexOf(',');
                return commaIndex > 0 ? firstIp.Substring(0, commaIndex).Trim() : firstIp.Trim();
            }
        }

        if (request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            var ip = realIp.FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Extract database operation from SQL command (memory optimized)
    /// </summary>
    private static string GetDbOperation(string commandText)
    {
        if (string.IsNullOrEmpty(commandText))
            return "unknown";

        // Simple parsing to get operation type
        var trimmed = commandText.TrimStart();
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "SELECT";
        if (trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            return "INSERT";
        if (trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            return "UPDATE";
        if (trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            return "DELETE";
        if (trimmed.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
            return "CREATE";

        return "unknown";
    }
}

/// <summary>
/// Activity extensions for convenient telemetry
/// </summary>
public static class ActivityExtensions
{
    public static Activity? SetUserContext(this Activity? activity, int userId, string? username = null)
    {
        activity?.SetTag("user.id", userId);
        if (!string.IsNullOrEmpty(username))
        {
            activity?.SetTag("user.name", username);
        }
        return activity;
    }

    public static Activity? SetAuthContext(this Activity? activity, string authType, bool success)
    {
        activity?.SetTag("auth.type", authType);
        activity?.SetTag("auth.success", success);
        return activity;
    }

    public static Activity? SetDatabaseContext(this Activity? activity, string operation, string table)
    {
        activity?.SetTag("db.operation", operation);
        activity?.SetTag("db.table", table);
        return activity;
    }

    public static Activity? SetCacheContext(this Activity? activity, string operation, string key, bool hit)
    {
        activity?.SetTag("cache.operation", operation);
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.hit", hit);
        return activity;
    }

    public static Activity? SetMemoryContext(this Activity? activity, string poolType, int size)
    {
        activity?.SetTag("memory.pool_type", poolType);
        activity?.SetTag("memory.pool_size", size);
        return activity;
    }

    public static Activity? RecordException(this Activity? activity, Exception exception)
    {
        activity?.SetTag("exception.type", exception.GetType().Name);
        activity?.SetTag("exception.message", exception.Message);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            activity?.SetTag("exception.stacktrace", exception.StackTrace);
        }
        
        return activity;
    }
}