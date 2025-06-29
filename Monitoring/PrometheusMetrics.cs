using Prometheus;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JwtApi.Monitoring;

/// <summary>
/// Comprehensive Prometheus metrics for .NET JWT API showcasing memory optimization
/// </summary>
public class PrometheusMetrics : BackgroundService
{
    private readonly ILogger<PrometheusMetrics> _logger;
    private readonly string _podName;
    private readonly string _environment;

    // HTTP Metrics
    private readonly Counter _httpRequestsTotal;
    private readonly Histogram _httpRequestDuration;
    private readonly Histogram _httpRequestSize;
    private readonly Histogram _httpResponseSize;

    // Authentication Metrics
    private readonly Counter _authAttempts;
    private readonly Histogram _authDuration;
    private readonly Counter _tokenOperations;
    private readonly Histogram _tokenGenerationDuration;
    private readonly Gauge _activeTokens;

    // Database Metrics
    private readonly Gauge _dbConnections;
    private readonly Histogram _dbQueryDuration;
    private readonly Counter _dbErrors;

    // Cache Metrics
    private readonly Counter _cacheOperations;
    private readonly Histogram _cacheDuration;
    private readonly Gauge _cacheSize;
    private readonly Gauge _cacheHitRate;

    // Memory Optimization Metrics
    private readonly Gauge _objectPoolUtilization;
    private readonly Gauge _arrayPoolUtilization;
    private readonly Counter _memoryAllocations;
    private readonly Histogram _gcDuration;
    private readonly Gauge _memoryUsage;

    // Performance Metrics
    private readonly Gauge _threadPoolThreads;
    private readonly Gauge _completionPortThreads;
    private readonly Histogram _requestQueueTime;
    private readonly Counter _exceptions;

    // Business Metrics
    private readonly Gauge _activeUsers;
    private readonly Gauge _userSessions;
    private readonly Counter _rateLimitHits;

    // Session Affinity Metrics
    private readonly Counter _sessionAffinityRequests;

    public PrometheusMetrics(ILogger<PrometheusMetrics> logger)
    {
        _logger = logger;
        _podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Initialize HTTP metrics
        _httpRequestsTotal = Metrics
            .CreateCounter("jwt_api_dotnet_http_requests_total", "Total HTTP requests",
                new[] { "method", "route", "status_code", "pod_name" });

        _httpRequestDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_http_request_duration_seconds", "HTTP request duration",
                new[] { "method", "route", "status_code", "pod_name" },
                new double[] { 0.001, 0.005, 0.015, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 1, 2, 5 });

        _httpRequestSize = Metrics
            .CreateHistogram("jwt_api_dotnet_http_request_size_bytes", "HTTP request size",
                new[] { "method", "route", "pod_name" },
                new double[] { 100, 1000, 10000, 100000, 1000000 });

        _httpResponseSize = Metrics
            .CreateHistogram("jwt_api_dotnet_http_response_size_bytes", "HTTP response size",
                new[] { "method", "route", "status_code", "pod_name" },
                new double[] { 100, 1000, 10000, 100000, 1000000 });

        // Initialize authentication metrics
        _authAttempts = Metrics
            .CreateCounter("jwt_api_dotnet_auth_attempts_total", "Authentication attempts",
                new[] { "type", "status", "pod_name" });

        _authDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_auth_duration_seconds", "Authentication duration",
                new[] { "type", "status", "pod_name" },
                new double[] { 0.01, 0.05, 0.1, 0.2, 0.5, 1, 2 });

        _tokenOperations = Metrics
            .CreateCounter("jwt_api_dotnet_token_operations_total", "Token operations",
                new[] { "operation", "type", "status", "pod_name" });

        _tokenGenerationDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_token_generation_duration_seconds", "Token generation duration",
                new[] { "type", "pod_name" },
                new double[] { 0.001, 0.005, 0.01, 0.02, 0.05, 0.1 });

        _activeTokens = Metrics
            .CreateGauge("jwt_api_dotnet_active_tokens_total", "Active tokens",
                new[] { "type", "pod_name" });

        // Initialize database metrics
        _dbConnections = Metrics
            .CreateGauge("jwt_api_dotnet_db_connections_active", "Active database connections",
                new[] { "pool", "pod_name" });

        _dbQueryDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_db_query_duration_seconds", "Database query duration",
                new[] { "operation", "table", "status", "pod_name" },
                new double[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1 });

        _dbErrors = Metrics
            .CreateCounter("jwt_api_dotnet_db_errors_total", "Database errors",
                new[] { "operation", "error_type", "pod_name" });

        // Initialize cache metrics
        _cacheOperations = Metrics
            .CreateCounter("jwt_api_dotnet_cache_operations_total", "Cache operations",
                new[] { "operation", "result", "pod_name" });

        _cacheDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_cache_operation_duration_seconds", "Cache operation duration",
                new[] { "operation", "pod_name" },
                new double[] { 0.0001, 0.0005, 0.001, 0.005, 0.01, 0.05 });

        _cacheSize = Metrics
            .CreateGauge("jwt_api_dotnet_cache_size_bytes", "Cache size",
                new[] { "cache_type", "pod_name" });

        _cacheHitRate = Metrics
            .CreateGauge("jwt_api_dotnet_cache_hit_rate", "Cache hit rate",
                new[] { "cache_type", "pod_name" });

        // Initialize memory optimization metrics
        _objectPoolUtilization = Metrics
            .CreateGauge("jwt_api_dotnet_object_pool_utilization", "Object pool utilization",
                new[] { "pool_type", "pod_name" });

        _arrayPoolUtilization = Metrics
            .CreateGauge("jwt_api_dotnet_array_pool_utilization", "Array pool utilization",
                new[] { "array_size", "pod_name" });

        _memoryAllocations = Metrics
            .CreateCounter("jwt_api_dotnet_memory_allocations_total", "Memory allocations",
                new[] { "allocation_type", "pod_name" });

        _gcDuration = Metrics
            .CreateHistogram("jwt_api_dotnet_gc_duration_seconds", "Garbage collection duration",
                new[] { "generation", "pod_name" },
                new double[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1 });

        _memoryUsage = Metrics
            .CreateGauge("jwt_api_dotnet_memory_usage_bytes", "Memory usage",
                new[] { "type", "pod_name" });

        // Initialize performance metrics
        _threadPoolThreads = Metrics
            .CreateGauge("jwt_api_dotnet_threadpool_threads", "Thread pool threads",
                new[] { "type", "pod_name" });

        _completionPortThreads = Metrics
            .CreateGauge("jwt_api_dotnet_completion_port_threads", "Completion port threads",
                new[] { "pod_name" });

        _requestQueueTime = Metrics
            .CreateHistogram("jwt_api_dotnet_request_queue_time_seconds", "Request queue time",
                new[] { "pod_name" },
                new double[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1 });

        _exceptions = Metrics
            .CreateCounter("jwt_api_dotnet_exceptions_total", "Application exceptions",
                new[] { "type", "endpoint", "pod_name" });

        // Initialize business metrics
        _activeUsers = Metrics
            .CreateGauge("jwt_api_dotnet_active_users_total", "Active users",
                new[] { "pod_name" });

        _userSessions = Metrics
            .CreateGauge("jwt_api_dotnet_user_sessions_total", "User sessions",
                new[] { "pod_name" });

        _rateLimitHits = Metrics
            .CreateCounter("jwt_api_dotnet_rate_limit_hits_total", "Rate limit hits",
                new[] { "endpoint", "client_ip", "pod_name" });

        // Initialize session affinity metrics
        _sessionAffinityRequests = Metrics
            .CreateCounter("jwt_api_dotnet_session_affinity_requests_total", "Session affinity requests",
                new[] { "pod_name", "client_ip" });

        _logger.LogInformation("📊 Prometheus metrics initialized for pod {PodName}", _podName);
    }

    // HTTP Metrics Methods
    public void RecordHttpRequest(string method, string route, int statusCode, double duration, long requestSize, long responseSize)
    {
        var labels = new[] { method, route, statusCode.ToString(), _podName };
        var routeLabels = new[] { method, route, _podName };

        _httpRequestsTotal.WithLabels(labels).Inc();
        _httpRequestDuration.WithLabels(labels).Observe(duration);
        _httpRequestSize.WithLabels(routeLabels).Observe(requestSize);
        _httpResponseSize.WithLabels(labels).Observe(responseSize);
    }

    // Authentication Metrics Methods
    public void RecordAuthAttempt(string type, bool success, double duration)
    {
        var status = success ? "success" : "failure";
        _authAttempts.WithLabels(type, status, _podName).Inc();
        _authDuration.WithLabels(type, status, _podName).Observe(duration);
    }

    public void RecordTokenOperation(string operation, string type, bool success, double? duration = null)
    {
        var status = success ? "success" : "failure";
        _tokenOperations.WithLabels(operation, type, status, _podName).Inc();
        
        if (duration.HasValue && operation == "generate")
        {
            _tokenGenerationDuration.WithLabels(type, _podName).Observe(duration.Value);
        }
    }

    public void UpdateActiveTokens(string type, int count)
    {
        _activeTokens.WithLabels(type, _podName).Set(count);
    }

    // Database Metrics Methods
    public void RecordDbOperation(string operation, string table, bool success, double duration, string? errorType = null)
    {
        var status = success ? "success" : "error";
        _dbQueryDuration.WithLabels(operation, table, status, _podName).Observe(duration);

        if (!success && !string.IsNullOrEmpty(errorType))
        {
            _dbErrors.WithLabels(operation, errorType, _podName).Inc();
        }
    }

    public void UpdateDbConnections(string pool, int count)
    {
        _dbConnections.WithLabels(pool, _podName).Set(count);
    }

    // Cache Metrics Methods
    public void RecordCacheOperation(string operation, string result, double duration)
    {
        _cacheOperations.WithLabels(operation, result, _podName).Inc();
        _cacheDuration.WithLabels(operation, _podName).Observe(duration);
    }

    public void UpdateCacheMetrics(string cacheType, long size, double hitRate)
    {
        _cacheSize.WithLabels(cacheType, _podName).Set(size);
        _cacheHitRate.WithLabels(cacheType, _podName).Set(hitRate);
    }

    // Memory Optimization Metrics Methods
    public void UpdateObjectPoolUtilization(string poolType, double utilization)
    {
        _objectPoolUtilization.WithLabels(poolType, _podName).Set(utilization);
    }

    public void UpdateArrayPoolUtilization(string arraySize, double utilization)
    {
        _arrayPoolUtilization.WithLabels(arraySize, _podName).Set(utilization);
    }

    public void RecordMemoryAllocation(string allocationType)
    {
        _memoryAllocations.WithLabels(allocationType, _podName).Inc();
    }

    public void RecordGcDuration(int generation, double duration)
    {
        _gcDuration.WithLabels($"gen{generation}", _podName).Observe(duration);
    }

    // Performance Metrics Methods
    public void RecordException(string exceptionType, string endpoint)
    {
        _exceptions.WithLabels(exceptionType, endpoint, _podName).Inc();
    }

    public void UpdateBusinessMetrics(int activeUsers, int userSessions)
    {
        _activeUsers.WithLabels(_podName).Set(activeUsers);
        _userSessions.WithLabels(_podName).Set(userSessions);
    }

    public void RecordRateLimitHit(string endpoint, string clientIp)
    {
        _rateLimitHits.WithLabels(endpoint, clientIp, _podName).Inc();
    }

    public void RecordSessionAffinityRequest(string clientIp)
    {
        _sessionAffinityRequests.WithLabels(_podName, clientIp).Inc();
    }

    // Background monitoring
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📊 Starting background metrics collection");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                UpdateSystemMetrics();
                UpdateMemoryMetrics();
                UpdateThreadMetrics();
                
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background metrics collection");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("📊 Background metrics collection stopped");
    }

    private void UpdateSystemMetrics()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            // Memory metrics
            _memoryUsage.WithLabels("working_set", _podName).Set(process.WorkingSet64);
            _memoryUsage.WithLabels("private_memory", _podName).Set(process.PrivateMemorySize64);
            _memoryUsage.WithLabels("virtual_memory", _podName).Set(process.VirtualMemorySize64);

            // GC metrics
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            var totalMemory = GC.GetTotalMemory(false);

            _memoryUsage.WithLabels("gc_total_memory", _podName).Set(totalMemory);
            _memoryUsage.WithLabels("gc_gen0_collections", _podName).Set(gen0Collections);
            _memoryUsage.WithLabels("gc_gen1_collections", _podName).Set(gen1Collections);
            _memoryUsage.WithLabels("gc_gen2_collections", _podName).Set(gen2Collections);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update system metrics");
        }
    }

    private void UpdateMemoryMetrics()
    {
        try
        {
            // Get memory allocation information
            var allocatedBytes = GC.GetTotalAllocatedBytes(false);
            _memoryUsage.WithLabels("allocated_bytes", _podName).Set(allocatedBytes);

            // Monitor memory pressure
            var memoryPressure = GC.GetTotalMemory(false);
            _memoryUsage.WithLabels("memory_pressure", _podName).Set(memoryPressure);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update memory allocation metrics");
        }
    }

    private void UpdateThreadMetrics()
    {
        try
        {
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            var usedWorkerThreads = maxWorkerThreads - availableWorkerThreads;
            var usedCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads;

            _threadPoolThreads.WithLabels("worker_available", _podName).Set(availableWorkerThreads);
            _threadPoolThreads.WithLabels("worker_used", _podName).Set(usedWorkerThreads);
            _threadPoolThreads.WithLabels("worker_max", _podName).Set(maxWorkerThreads);

            _completionPortThreads.WithLabels(_podName).Set(usedCompletionPortThreads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update thread pool metrics");
        }
    }
}