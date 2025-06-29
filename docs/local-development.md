# Local Development Guide

This guide covers setting up the JWT API for local development, including all necessary tools, configuration options, and debugging techniques.

## 📋 Prerequisites

### Required Software

1. **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
   ```bash
   # Verify installation
   dotnet --version
   # Should show: 8.0.x
   ```

2. **IDE/Editor** (Choose one)
   - [Visual Studio 2022](https://visualstudio.microsoft.com/) (Windows/Mac)
   - [Visual Studio Code](https://code.visualstudio.com/) with C# extension
   - [JetBrains Rider](https://www.jetbrains.com/rider/)

3. **Optional Tools**
   - [SQL Server](https://www.microsoft.com/en-us/sql-server) (for production-like testing)
   - [Docker Desktop](https://www.docker.com/products/docker-desktop) (for containerized services)
   - [Postman](https://www.postman.com/) or [Insomnia](https://insomnia.rest/) (API testing)

## 🚀 Quick Setup

### 1. Clone and Restore

```bash
# Clone the repository
git clone <repository-url>
cd jwt-dotnet-memory-optimized

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build
```

### 2. Run with In-Memory Database (Recommended for Development)

```bash
# Option 1: Command line argument
dotnet run --UseInMemoryDatabase=true

# Option 2: Environment variable
export UseInMemoryDatabase=true
dotnet run

# Option 3: Via appsettings.Development.json (see Configuration section)
```

### 3. Access the Application

- **API Base URL**: http://localhost:5000 or https://localhost:5001
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health
- **Metrics**: http://localhost:5000/metrics

## ⚙️ Configuration

### Development Configuration Files

The application uses a hierarchical configuration system:

1. **`appsettings.json`** - Base configuration
2. **`appsettings.Development.json`** - Development overrides
3. **Environment variables** - Highest priority

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
    "LogHighAllocations": false,
    "HighAllocationThreshold": 5000000
  },
  
  "Performance": {
    "ConnectionPoolSize": 32,
    "MaxConcurrentConnections": 100
  },
  
  "Cache": {
    "SizeLimit": 10000,
    "CompactionPercentage": 0.5
  }
}
```

### Environment Variables for Development

Create a `.env` file or set these in your IDE:

```bash
# Database Configuration
UseInMemoryDatabase=true                    # Use in-memory DB for development
ASPNETCORE_ENVIRONMENT=Development

# JWT Configuration
JWT_KEY=development-jwt-key-not-for-production-use-only-for-local-development-testing-purposes
JWT_ISSUER=JwtApi-Dev
JWT_AUDIENCE=JwtApiUsers-Dev
JWT_EXPIRY_MINUTES=60

# Performance Optimization
DOTNET_TieredPGO=1
DOTNET_ReadyToRun=1
DOTNET_TC_QuickJitForLoops=1

# Logging
ASPNETCORE_DETAILEDERRORS=true
Logging__LogLevel__Default=Debug
```

## 🗄️ Database Options

### Option 1: In-Memory Database (Recommended for Development)

**Pros:**
- No setup required
- Fast startup
- Isolated test data
- No dependencies

**Cons:**
- Data lost on restart
- Limited testing of EF migrations

**Setup:**
```bash
# Set environment variable
export UseInMemoryDatabase=true
dotnet run
```

### Option 2: SQLite Database

**Pros:**
- Persistent data
- No server required
- Good for migration testing

**Cons:**
- Limited SQL Server feature compatibility

**Setup:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=jwt-api-dev.db"
  }
}
```

### Option 3: SQL Server LocalDB

**Pros:**
- Full SQL Server compatibility
- Good for production-like testing

**Cons:**
- Windows only
- Requires SQL Server installation

**Setup:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=JwtApiDb;Trusted_Connection=true;"
  }
}
```

### Option 4: SQL Server in Docker

**Pros:**
- Cross-platform
- Production-like environment
- Easy cleanup

**Setup:**
```bash
# Start SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123" \
  -p 1433:1433 --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest

# Update appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=JwtApiDb;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
  }
}
```

## 🧪 Testing the API

### Using Swagger UI

1. Navigate to http://localhost:5000/swagger
2. Expand the Authentication endpoints
3. Try the following flow:

#### Register a User
```json
POST /api/auth/register
{
  "username": "testuser",
  "email": "test@example.com",
  "password": "TestPassword123",
  "confirmPassword": "TestPassword123",
  "firstName": "Test",
  "lastName": "User"
}
```

#### Login
```json
POST /api/auth/login
{
  "email": "test@example.com",
  "password": "TestPassword123"
}
```

#### Use JWT Token
Copy the `accessToken` from login response and use it in the Authorization header:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Using cURL

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "TestPassword123",
    "confirmPassword": "TestPassword123",
    "firstName": "Test",
    "lastName": "User"
  }'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "TestPassword123"
  }'

# Use token (replace TOKEN with actual token)
curl -H "Authorization: Bearer TOKEN" \
  http://localhost:5000/api/auth/verify
```

## 🔧 Development Tools

### Hot Reload

.NET 8 supports hot reload for rapid development:

```bash
# Start with hot reload
dotnet watch run

# Hot reload will automatically apply code changes
# Supports:
# - Controller changes
# - Service modifications
# - Configuration updates
```

### Memory Profiling

The API includes built-in memory monitoring:

```bash
# Check memory headers in responses
curl -v http://localhost:5000/health

# Headers included:
# X-Memory-Initial: 15728640
# X-Memory-Final: 15892480
# X-Memory-Delta: 163840
# X-Response-Time: 45
```

### Debugging

#### Visual Studio/Rider
1. Set breakpoints in code
2. Press F5 or click Debug
3. Use the debugger to step through code

#### Visual Studio Code
1. Install C# extension
2. Use `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (web)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net8.0/JwtApi.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "UseInMemoryDatabase": "true"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    }
  ]
}
```

## 📊 Monitoring Development

### Logs

Logs are written to:
- **Console**: Real-time output
- **File**: `logs/jwt-api-<date>.txt`

View logs:
```bash
# Follow logs in real-time
tail -f logs/jwt-api-$(date +%Y%m%d).txt

# Filter for specific levels
grep "ERR" logs/jwt-api-$(date +%Y%m%d).txt
grep "WRN" logs/jwt-api-$(date +%Y%m%d).txt
```

### Health Checks

```bash
# Basic health check
curl http://localhost:5000/health

# Expected response: "Healthy"
```

### Metrics

```bash
# Get performance metrics
curl http://localhost:5000/metrics | jq

# Response includes:
# - Memory usage
# - GC collections
# - Pod information
# - Timestamps
```

## 🐛 Common Development Issues

### 1. Port Already in Use

```bash
# Find process using port 5000
lsof -i :5000
# Or on Windows
netstat -ano | findstr :5000

# Kill process or change port
dotnet run --urls "http://localhost:5001"
```

### 2. Database Connection Issues

```bash
# Use in-memory database to isolate issues
export UseInMemoryDatabase=true
dotnet run
```

### 3. JWT Key Too Short

```
Error: JWT key must be at least 32 characters
```

Solution: Use the development key or set a longer one:
```bash
export JWT_KEY="development-jwt-key-not-for-production-use-only-for-local-development-testing-purposes"
```

### 4. Missing NuGet Packages

```bash
# Clear package cache and restore
dotnet nuget locals all --clear
dotnet restore --force
```

## 🚀 Advanced Development

### Custom Configuration

Add custom settings to `appsettings.Development.json`:

```json
{
  "CustomSettings": {
    "FeatureFlags": {
      "EnableDetailedMetrics": true,
      "EnableSlowQueryLogging": true,
      "EnableMemoryProfiling": true
    },
    "Development": {
      "SeedTestData": true,
      "MockExternalServices": true,
      "EnableSwaggerInProduction": true
    }
  }
}
```

### Performance Testing

```bash
# Install dotnet-counters for performance monitoring
dotnet tool install --global dotnet-counters

# Monitor application
dotnet-counters monitor --process-id $(pgrep -f JwtApi) System.Runtime

# Key metrics to watch:
# - Gen 0/1/2 GC Count
# - Heap Size
# - ThreadPool Queue Length
# - Exception Count
```

### Load Testing

```bash
# Install Apache Bench
sudo apt-get install apache2-utils  # Ubuntu
brew install httpie                  # macOS

# Simple load test
ab -n 1000 -c 10 http://localhost:5000/health

# More advanced with authentication
# First get a token, then:
ab -n 1000 -c 10 -H "Authorization: Bearer YOUR_TOKEN" \
  http://localhost:5000/api/auth/verify
```

## 📚 Next Steps

1. **Read the API Reference**: [api-reference.md](api-reference.md)
2. **Configure Monitoring**: [monitoring.md](monitoring.md)
3. **Deploy with Docker**: [docker-deployment.md](docker-deployment.md)
4. **Production Deployment**: [kubernetes.md](kubernetes.md)
5. **Performance Tuning**: [performance.md](performance.md)

## 💡 Development Tips

1. **Use in-memory database** for fast development cycles
2. **Enable hot reload** with `dotnet watch run`
3. **Monitor memory headers** to understand performance impact
4. **Use Swagger UI** for interactive API testing
5. **Check logs regularly** for warnings and errors
6. **Test with real data** occasionally using SQL Server
7. **Profile memory usage** during feature development
8. **Set up IDE debugging** for efficient troubleshooting