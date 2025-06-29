using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using JwtApi.Data;
using JwtApi.Services;
using JwtApi.Models;
using JwtApi.Middleware;
using JwtApi.Memory;
using System.Threading.RateLimiting;

// Configure Serilog for high-performance logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/jwt-api-.txt", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        buffered: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Configure JSON options for memory efficiency
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false; // Reduce memory footprint
    options.SerializerOptions.DefaultBufferSize = 4096; // Optimize buffer size
});

// Memory optimization: Configure Kestrel for high performance
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MaxRequestBodySize = 1_048_576; // 1MB limit
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// Database context with connection pooling for memory efficiency
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    
    // Memory optimization: Configure context pooling
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(false);
}, poolSize: 128); // Pool size for high concurrency

// Memory caching with size limits
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100_000; // Limit cache size
    options.CompactionPercentage = 0.25; // Remove 25% when limit reached
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

// Object pooling for memory efficiency
builder.Services.AddSingleton<ObjectPool<StringBuilder>>(serviceProvider =>
{
    var provider = new DefaultObjectPoolProvider();
    var policy = new StringBuilderPooledObjectPolicy();
    return provider.Create(policy);
});

// Custom memory-optimized services
builder.Services.AddSingleton<IMemoryOptimizedTokenService, MemoryOptimizedTokenService>();
builder.Services.AddSingleton<IHighPerformanceCache, HighPerformanceCache>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ArrayPool for buffer management
builder.Services.AddSingleton<ArrayPool<byte>>(ArrayPool<byte>.Shared);
builder.Services.AddSingleton<ArrayPool<char>>(ArrayPool<char>.Shared);

// Rate limiting for memory protection
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
    
    // Auth endpoint specific limits
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3
            }));
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-change-this-in-production-must-be-at-least-32-characters-long";
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "JwtApi",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "JwtApiUsers",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };
    
    // Memory optimization: Configure token caching
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            // Cache validated tokens to reduce validation overhead
            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            cache.Set($"token:{token.GetHashCode()}", context.Principal, TimeSpan.FromMinutes(15));
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Controllers with memory-optimized JSON
builder.Services.AddControllers(options =>
{
    options.ModelValidatorProviders.Clear(); // Use FluentValidation instead
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = false;
    options.JsonSerializerOptions.DefaultBufferSize = 4096;
});

// Swagger/OpenAPI with memory-efficient configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JWT API - Memory Optimized",
        Version = "v1",
        Description = "High-performance JWT Authentication API showcasing .NET memory optimization libraries",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@jwtapi.com"
        }
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck("memory", () =>
    {
        var allocatedBytes = GC.GetTotalMemory(false);
        var maxBytes = 1_000_000_000; // 1GB limit
        
        return allocatedBytes < maxBytes 
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"Memory usage: {allocatedBytes:N0} bytes")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"Memory usage too high: {allocatedBytes:N0} bytes");
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Memory optimization: Configure response compression
app.UseResponseCompression();

// Custom middleware for memory monitoring
app.UseMiddleware<MemoryMonitoringMiddleware>();
app.UseMiddleware<PodIdentificationMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JWT API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "JWT API - Memory Optimized";
        c.DefaultModelExpandDepth(2);
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseHttpsRedirection();
app.UseCors();

// Rate limiting
app.UseRateLimiter();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Health checks endpoint
app.MapHealthChecks("/health");

// Performance metrics endpoint
app.MapGet("/metrics", (IServiceProvider services) =>
{
    var memoryCache = services.GetRequiredService<IMemoryCache>();
    
    return Results.Ok(new
    {
        Timestamp = DateTime.UtcNow,
        MemoryUsage = new
        {
            TotalAllocated = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        },
        PodInfo = new
        {
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = Environment.WorkingSet,
            PodName = Environment.GetEnvironmentVariable("POD_NAME") ?? "unknown",
            PodIP = Environment.GetEnvironmentVariable("POD_IP") ?? "localhost"
        }
    });
})
.WithName("GetMetrics")
.WithTags("Monitoring")
.Produces(200);

app.MapControllers();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.EnsureCreatedAsync();
}

Log.Information("JWT API started with memory optimizations enabled");

app.Run();

// Ensure proper cleanup
Log.CloseAndFlush();