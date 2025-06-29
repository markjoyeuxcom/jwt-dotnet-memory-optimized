# Configuration Reference Guide

Complete reference for all configuration options, environment variables, and settings available in the JWT API.

## 📋 Configuration Hierarchy

The application uses a hierarchical configuration system with the following priority (highest to lowest):

1. **Command Line Arguments**
2. **Environment Variables**
3. **appsettings.{Environment}.json**
4. **appsettings.json**
5. **Default Values**

## 🔧 Core Configuration Sections

### JWT Configuration

Controls JSON Web Token generation and validation:

```json
{
  "Jwt": {
    "Key": "your-super-secret-jwt-key-change-this-in-production-must-be-at-least-32-characters-long",
    "Issuer": "JwtApi",
    "Audience": "JwtApiUsers",
    "ExpiryMinutes": "15",
    "RefreshExpiryDays": "7"
  }
}
```

| Setting | Environment Variable | Description | Default | Required |
|---------|---------------------|-------------|---------|----------|
| `Jwt:Key` | `JWT_KEY` | JWT signing key (minimum 32 characters) | Development key | Yes (Production) |
| `Jwt:Issuer` | `JWT_ISSUER` | Token issuer identifier | `JwtApi` | No |
| `Jwt:Audience` | `JWT_AUDIENCE` | Token audience identifier | `JwtApiUsers` | No |
| `Jwt:ExpiryMinutes` | `JWT_EXPIRY_MINUTES` | Access token expiry in minutes | `15` | No |
| `Jwt:RefreshExpiryDays` | `JWT_REFRESH_EXPIRY_DAYS` | Refresh token expiry in days | `7` | No |

### Database Configuration

Controls database connection and Entity Framework settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=JwtApiDb;Trusted_Connection=true;"
  },
  "UseInMemoryDatabase": false
}
```

| Setting | Environment Variable | Description | Default | Required |
|---------|---------------------|-------------|---------|----------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Database connection string | SQLite file | No |
| `UseInMemoryDatabase` | `UseInMemoryDatabase` | Use in-memory database | `false` | No |

**Connection String Examples:**

```bash
# SQL Server with Windows Authentication
ConnectionStrings__DefaultConnection="Server=localhost;Database=JwtApiDb;Trusted_Connection=true;"

# SQL Server with SQL Authentication
ConnectionStrings__DefaultConnection="Server=localhost;Database=JwtApiDb;User Id=sa;Password=YourPassword123;"

# Azure SQL Database
ConnectionStrings__DefaultConnection="Server=tcp:yourserver.database.windows.net,1433;Database=JwtApiDb;User ID=yourusername;Password=yourpassword;Encrypt=True;TrustServerCertificate=False;"

# SQLite
ConnectionStrings__DefaultConnection="Data Source=jwt-api.db"
```

### Logging Configuration

Structured logging with Serilog:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information",
      "JwtApi": "Debug"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/jwt-api-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200",
          "indexFormat": "jwt-api-{0:yyyy.MM.dd}"
        }
      }
    ]
  }
}
```

### Memory Optimization Configuration

Controls memory optimization features:

```json
{
  "Memory": {
    "MonitoringEnabled": true,
    "LogHighAllocations": false,
    "HighAllocationThreshold": 5000000
  },
  "Cache": {
    "SizeLimit": 100000,
    "CompactionPercentage": 0.25,
    "ExpirationScanFrequency": "00:05:00"
  }
}
```

| Setting | Environment Variable | Description | Default |
|---------|---------------------|-------------|---------|
| `Memory:MonitoringEnabled` | `Memory__MonitoringEnabled` | Enable memory monitoring middleware | `true` |
| `Memory:LogHighAllocations` | `Memory__LogHighAllocations` | Log requests with high memory allocation | `false` |
| `Memory:HighAllocationThreshold` | `Memory__HighAllocationThreshold` | Threshold for high allocation logging (bytes) | `5000000` |
| `Cache:SizeLimit` | `Cache__SizeLimit` | Maximum cache size (entries) | `100000` |
| `Cache:CompactionPercentage` | `Cache__CompactionPercentage` | Percentage to remove when limit reached | `0.25` |

### Performance Configuration

Application performance tuning:

```json
{
  "Performance": {
    "ConnectionPoolSize": 128,
    "MaxConcurrentConnections": 1000,
    "MaxRequestBodySize": 1048576,
    "KeepAliveTimeout": "00:02:00",
    "RequestHeadersTimeout": "00:00:30"
  }
}
```

### OpenTelemetry Configuration

Distributed tracing and observability:

```json
{
  "OpenTelemetry": {
    "ServiceName": "jwt-api",
    "ServiceVersion": "1.0.0",
    "Jaeger": {
      "Endpoint": "http://jaeger:14268/api/traces",
      "Protocol": "HttpBinaryThrift"
    },
    "Otlp": {
      "Endpoint": "http://otel-collector:4317",
      "Protocol": "Grpc"
    },
    "Console": {
      "Enabled": false
    }
  }
}
```

| Setting | Environment Variable | Description | Default |
|---------|---------------------|-------------|---------|
| `OpenTelemetry:Jaeger:Endpoint` | `OpenTelemetry__Jaeger__Endpoint` | Jaeger collector endpoint | Not set |
| `OpenTelemetry:Otlp:Endpoint` | `OpenTelemetry__Otlp__Endpoint` | OTLP collector endpoint | Not set |

### Rate Limiting Configuration

API rate limiting settings:

```json
{
  "RateLimiting": {
    "GlobalLimit": 100,
    "GlobalWindow": "00:01:00",
    "AuthLimit": 10,
    "AuthWindow": "00:15:00",
    "IpWhitelist": [
      "127.0.0.1",
      "::1"
    ]
  }
}
```

## 🌍 Environment Variables

### Core Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Application environment | `Development`, `Staging`, `Production` |
| `ASPNETCORE_URLS` | Application URLs | `http://localhost:5000;https://localhost:5001` |
| `DOTNET_ENVIRONMENT` | .NET environment | `Development`, `Production` |

### Performance Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DOTNET_TieredPGO` | Enable tiered compilation PGO | `1` |
| `DOTNET_ReadyToRun` | Use ReadyToRun images | `1` |
| `DOTNET_TC_QuickJitForLoops` | Quick JIT for loops | `1` |
| `DOTNET_gcServer` | Server garbage collection | `1` |
| `DOTNET_GCHeapCount` | GC heap count (0 = use all cores) | `0` |

### Kubernetes-Specific Variables

| Variable | Description | Source |
|----------|-------------|--------|
| `POD_NAME` | Pod name | `metadata.name` |
| `POD_IP` | Pod IP address | `status.podIP` |
| `NODE_NAME` | Node name | `spec.nodeName` |

## 📄 Configuration Files

### appsettings.json (Base Configuration)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "Jwt": {
    "Key": "development-jwt-key-not-for-production-use-only-for-local-development",
    "Issuer": "JwtApi",
    "Audience": "JwtApiUsers",
    "ExpiryMinutes": "15",
    "RefreshExpiryDays": "7"
  },
  
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=jwt-api.db"
  },
  
  "Memory": {
    "MonitoringEnabled": true,
    "LogHighAllocations": false,
    "HighAllocationThreshold": 5000000
  },
  
  "Performance": {
    "ConnectionPoolSize": 128,
    "MaxConcurrentConnections": 1000
  },
  
  "Cache": {
    "SizeLimit": 100000,
    "CompactionPercentage": 0.25
  }
}
```

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information",
      "JwtApi": "Debug"
    }
  },
  
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=jwt-api-dev.db"
  },
  
  "Jwt": {
    "Key": "development-jwt-key-not-for-production-use-only-for-local-development-testing-purposes",
    "Issuer": "JwtApi-Dev",
    "Audience": "JwtApiUsers-Dev",
    "ExpiryMinutes": "60",
    "RefreshExpiryDays": "30"
  },
  
  "Memory": {
    "MonitoringEnabled": true,
    "LogHighAllocations": true,
    "HighAllocationThreshold": 1000000
  },
  
  "Performance": {
    "ConnectionPoolSize": 32,
    "MaxConcurrentConnections": 100
  },
  
  "Cache": {
    "SizeLimit": 10000,
    "CompactionPercentage": 0.5
  },
  
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/jwt-api-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  
  "Jwt": {
    "ExpiryMinutes": "15",
    "RefreshExpiryDays": "7"
  },
  
  "Memory": {
    "MonitoringEnabled": true,
    "LogHighAllocations": false,
    "HighAllocationThreshold": 10000000
  },
  
  "Performance": {
    "ConnectionPoolSize": 128,
    "MaxConcurrentConnections": 1000
  },
  
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/jwt-api-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200",
          "indexFormat": "jwt-api-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true
        }
      }
    ]
  }
}
```

## 🐳 Docker Configuration

### Environment Variables for Docker

```bash
# Core Application
ASPNETCORE_ENVIRONMENT=Production
UseInMemoryDatabase=false

# JWT Configuration
JWT_KEY=production-jwt-key-must-be-at-least-32-characters-long-and-secure
JWT_ISSUER=MyCompany.Api
JWT_AUDIENCE=MyCompany.Users

# Database
ConnectionStrings__DefaultConnection="Server=sqlserver;Database=JwtApiDb;User Id=sa;Password=YourPassword123;"

# Performance
DOTNET_TieredPGO=1
DOTNET_ReadyToRun=1
DOTNET_gcServer=1

# Monitoring
OpenTelemetry__Jaeger__Endpoint=http://jaeger:14268/api/traces
Serilog__WriteTo__1__Args__nodeUris=http://elasticsearch:9200
```

### Docker Compose Configuration

```yaml
version: '3.8'
services:
  jwt-api:
    image: jwt-api:latest
    environment:
      # Application
      - ASPNETCORE_ENVIRONMENT=Production
      - UseInMemoryDatabase=false
      
      # JWT
      - JWT_KEY=${JWT_PRODUCTION_KEY}
      - JWT_ISSUER=MyCompany.Api
      - JWT_AUDIENCE=MyCompany.Users
      
      # Database
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=JwtApiDb;User Id=sa;Password=${SA_PASSWORD};
      
      # Performance
      - DOTNET_TieredPGO=1
      - DOTNET_ReadyToRun=1
      - Memory__MonitoringEnabled=true
      
      # Monitoring
      - OpenTelemetry__Jaeger__Endpoint=http://jaeger:14268/api/traces
```

## ☸️ Kubernetes Configuration

### ConfigMap

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: jwt-api-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  UseInMemoryDatabase: "false"
  
  # JWT Configuration
  Jwt__Issuer: "MyCompany.Api"
  Jwt__Audience: "MyCompany.Users"
  Jwt__ExpiryMinutes: "15"
  
  # Performance
  DOTNET_TieredPGO: "1"
  DOTNET_ReadyToRun: "1"
  DOTNET_gcServer: "1"
  
  # Memory
  Memory__MonitoringEnabled: "true"
  Memory__HighAllocationThreshold: "10000000"
  
  # Cache
  Cache__SizeLimit: "100000"
  Cache__CompactionPercentage: "0.25"
```

### Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: jwt-api-secrets
type: Opaque
data:
  JWT_KEY: <base64-encoded-jwt-key>
  ConnectionStrings__DefaultConnection: <base64-encoded-connection-string>
```

```bash
# Create secrets from command line
kubectl create secret generic jwt-api-secrets \
  --from-literal=JWT_KEY="your-production-jwt-key" \
  --from-literal=ConnectionStrings__DefaultConnection="Server=..." \
  --namespace jwt-api
```

## 🔍 Configuration Validation

### Validation Rules

The application validates configuration at startup:

1. **JWT Key**: Must be at least 32 characters in production
2. **Database**: Connection string must be valid format
3. **Memory Limits**: Must be positive numbers
4. **Performance Settings**: Must be within acceptable ranges

### Validation Examples

```csharp
// Automatic validation in Program.cs
builder.Services.Configure<JwtOptions>(options =>
{
    builder.Configuration.GetSection("Jwt").Bind(options);
});

builder.Services.AddOptions<JwtOptions>()
    .ValidateDataAnnotations()
    .Validate(options => 
    {
        if (builder.Environment.IsProduction() && options.Key.Length < 32)
        {
            return false;
        }
        return true;
    }, "JWT key must be at least 32 characters in production");
```

## 🧪 Configuration Testing

### Test Configuration Override

For testing, create `appsettings.Test.json`:

```json
{
  "UseInMemoryDatabase": true,
  "Jwt": {
    "Key": "test-jwt-key-for-unit-testing-must-be-at-least-32-characters-long",
    "ExpiryMinutes": "5"
  },
  "Memory": {
    "MonitoringEnabled": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### Configuration in Tests

```csharp
public class TestStartup : Startup
{
    public TestStartup(IConfiguration configuration) : base(configuration) { }
    
    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Override services for testing
        services.Configure<JwtOptions>(options =>
        {
            options.Key = "test-key-for-testing-at-least-32-characters";
            options.ExpiryMinutes = 5;
        });
    }
}
```

## 🔧 Configuration Troubleshooting

### Common Issues

**1. Configuration Not Loading**
```bash
# Check environment
echo $ASPNETCORE_ENVIRONMENT

# Verify configuration file exists
ls -la appsettings.*.json

# Check configuration binding
# Add logging in Program.cs to see loaded config
```

**2. Environment Variables Not Applied**
```bash
# Check if environment variables are set
env | grep JWT

# Verify naming convention (double underscore for sections)
export JWT__KEY="your-key"  # Correct
export JWT:KEY="your-key"   # Incorrect for environment variables
```

**3. JWT Key Validation Fails**
```bash
# Check key length
echo -n "$JWT_KEY" | wc -c  # Should be >= 32

# Verify base64 encoding if needed
echo "$JWT_KEY" | base64 -d | wc -c
```

### Configuration Debugging

Add configuration logging:

```csharp
var config = builder.Configuration;
var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Config");

logger.LogInformation("JWT Key length: {Length}", config["Jwt:Key"]?.Length ?? 0);
logger.LogInformation("Database: {Connection}", config.GetConnectionString("DefaultConnection"));
logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
```

## 📋 Configuration Checklist

### Development Setup
- [ ] appsettings.Development.json configured
- [ ] Database connection working
- [ ] JWT key set (can be development key)
- [ ] Logging configured for debugging
- [ ] Memory monitoring enabled

### Production Deployment
- [ ] Production JWT key set (32+ characters)
- [ ] Database connection string secured
- [ ] Secrets properly managed
- [ ] Logging configured for production
- [ ] Performance settings optimized
- [ ] Monitoring endpoints configured
- [ ] Environment variables documented

### Security
- [ ] JWT keys not in source control
- [ ] Database passwords in secrets
- [ ] HTTPS enforced in production
- [ ] Sensitive logs filtered
- [ ] Configuration validation enabled

## 📚 Next Steps

1. **API Reference**: [api-reference.md](api-reference.md)
2. **Performance Tuning**: [performance.md](performance.md)
3. **Troubleshooting**: [troubleshooting.md](troubleshooting.md)
4. **Monitoring Setup**: [monitoring.md](monitoring.md)