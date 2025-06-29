# Docker Deployment Guide

Complete guide for containerizing and deploying the JWT API using Docker, including development, staging, and production scenarios.

## 📋 Prerequisites

- [Docker](https://www.docker.com/get-started) (version 20.10+)
- [Docker Compose](https://docs.docker.com/compose/) (optional, for multi-service deployments)

## 🐳 Docker Image

### Dockerfile Overview

The application uses a multi-stage Dockerfile optimized for production:

```dockerfile
# Multi-stage build for optimized production image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Memory optimization environment variables
ENV DOTNET_TieredPGO=1
ENV DOTNET_ReadyToRun=1
ENV DOTNET_TC_QuickJitForLoops=1
ENV DOTNET_gcServer=1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["JwtApi.csproj", "."]
RUN dotnet restore "JwtApi.csproj" \
    --runtime linux-x64 \
    --no-cache \
    --verbosity minimal

COPY . .
RUN dotnet build "JwtApi.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    --verbosity minimal \
    -p:TreatWarningsAsErrors=false

FROM build AS publish
RUN dotnet publish "JwtApi.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained false \
    --verbosity minimal \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false \
    -p:TreatWarningsAsErrors=false

FROM base AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY --from=publish /app/publish .

# Create logs directory and set permissions
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "JwtApi.dll"]
```

## 🚀 Quick Start

### Build and Run

```bash
# Build the image
docker build -t jwt-api .

# Run with in-memory database (development)
docker run -d -p 8080:80 \
  -e UseInMemoryDatabase=true \
  --name jwt-api-dev \
  jwt-api

# Test the API
curl http://localhost:8080/health
```

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `UseInMemoryDatabase` | Use in-memory DB instead of SQL Server | `false` | `true` |
| `JWT_KEY` | JWT signing key (min 32 chars) | Dev key | `your-secure-jwt-key-32-chars-min` |
| `ConnectionStrings__DefaultConnection` | Database connection string | SQLite | `Server=db;Database=JwtApi;...` |
| `ASPNETCORE_ENVIRONMENT` | Application environment | `Production` | `Development`, `Staging` |
| `Jwt__Issuer` | JWT token issuer | `JwtApi` | `MyCompany.Api` |
| `Jwt__Audience` | JWT token audience | `JwtApiUsers` | `MyApp.Users` |
| `Jwt__ExpiryMinutes` | Access token expiry | `15` | `30` |
| `DOTNET_TieredPGO` | Enable tiered compilation PGO | `1` | `0` to disable |

### Memory Optimization Variables

```bash
# Garbage Collection Optimization
DOTNET_gcServer=1                    # Server GC for better throughput
DOTNET_GCHeapCount=0                 # Use all available cores
DOTNET_GCConcurrent=1                # Enable concurrent GC

# Compilation Optimization
DOTNET_TieredPGO=1                   # Profile-guided optimization
DOTNET_ReadyToRun=1                  # Use ReadyToRun images
DOTNET_TC_QuickJitForLoops=1         # Quick JIT for loops

# Thread Pool Optimization
DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=70
```

## 📊 Deployment Scenarios

### 1. Development Deployment

**Use Case**: Local development, testing, quick demos

```bash
# Build development image
docker build -t jwt-api:dev .

# Run with in-memory database
docker run -d \
  --name jwt-api-dev \
  -p 8080:80 \
  -e UseInMemoryDatabase=true \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e Logging__LogLevel__Default=Debug \
  jwt-api:dev

# Access the application
echo "API: http://localhost:8080"
echo "Swagger: http://localhost:8080/swagger"
echo "Health: http://localhost:8080/health"
```

### 2. Staging Deployment with SQL Server

**Use Case**: Integration testing, staging environment

```bash
# Run SQL Server container
docker run -d \
  --name jwt-api-sqlserver \
  -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=StrongPassword123!" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Run JWT API connected to SQL Server
docker run -d \
  --name jwt-api-staging \
  -p 8080:80 \
  --link jwt-api-sqlserver:sqlserver \
  -e ASPNETCORE_ENVIRONMENT=Staging \
  -e JWT_KEY="staging-jwt-key-must-be-at-least-32-characters-long-and-secure" \
  -e ConnectionStrings__DefaultConnection="Server=sqlserver;Database=JwtApiDb;User Id=sa;Password=StrongPassword123!;TrustServerCertificate=True" \
  jwt-api:latest

# Verify deployment
curl http://localhost:8080/health
```

### 3. Production Deployment

**Use Case**: Production environment with external database

```bash
# Run with production configuration
docker run -d \
  --name jwt-api-prod \
  -p 80:80 \
  --restart unless-stopped \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e JWT_KEY="${JWT_PRODUCTION_KEY}" \
  -e ConnectionStrings__DefaultConnection="${PRODUCTION_DB_CONNECTION}" \
  -e Jwt__Issuer="MyCompany.Api" \
  -e Jwt__Audience="MyApp.Users" \
  --memory=512m \
  --memory-swap=1g \
  --cpus=1.0 \
  jwt-api:latest
```

## 🐙 Docker Compose

### Development Stack

Create `docker-compose.dev.yml`:

```yaml
version: '3.8'

services:
  jwt-api:
    build: .
    ports:
      - "8080:80"
    environment:
      - UseInMemoryDatabase=true
      - ASPNETCORE_ENVIRONMENT=Development
      - Logging__LogLevel__Default=Debug
    volumes:
      - ./logs:/app/logs
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

```bash
# Run development stack
docker-compose -f docker-compose.dev.yml up -d

# View logs
docker-compose -f docker-compose.dev.yml logs -f jwt-api

# Stop and cleanup
docker-compose -f docker-compose.dev.yml down
```

### Production Stack with SQL Server

Create `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourProductionPassword123!
    volumes:
      - sqlserver_data:/var/opt/mssql
    ports:
      - "1433:1433"
    healthcheck:
      test: ["CMD", "/opt/mssql-tools/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", "YourProductionPassword123!", "-Q", "SELECT 1"]
      interval: 30s
      timeout: 10s
      retries: 5

  jwt-api:
    build: .
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - JWT_KEY=production-jwt-key-must-be-at-least-32-characters-long-and-secure-change-this
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=JwtApiDb;User Id=sa;Password=YourProductionPassword123!;TrustServerCertificate=True
      - Jwt__Issuer=MyCompany.Api
      - Jwt__Audience=MyApp.Users
    depends_on:
      sqlserver:
        condition: service_healthy
    volumes:
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: '1.0'
        reservations:
          memory: 256M
          cpus: '0.5'

volumes:
  sqlserver_data:
```

```bash
# Deploy production stack
docker-compose -f docker-compose.prod.yml up -d

# Check service health
docker-compose -f docker-compose.prod.yml ps

# Scale the API service
docker-compose -f docker-compose.prod.yml up -d --scale jwt-api=3
```

### Full Observability Stack

Create `docker-compose.observability.yml`:

```yaml
version: '3.8'

services:
  # Application
  jwt-api:
    build: .
    ports:
      - "8080:80"
    environment:
      - UseInMemoryDatabase=true
      - ASPNETCORE_ENVIRONMENT=Development
      - OpenTelemetry__Jaeger__Endpoint=http://jaeger:14268/api/traces
      - Serilog__WriteTo__1__Name=Elasticsearch
      - Serilog__WriteTo__1__Args__nodeUris=http://elasticsearch:9200
    depends_on:
      - elasticsearch
      - jaeger
    volumes:
      - ./logs:/app/logs

  # Monitoring
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana_data:/var/lib/grafana

  # Tracing
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"
      - "14268:14268"
    environment:
      - COLLECTOR_OTLP_ENABLED=true

  # Logging
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data

  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    ports:
      - "5601:5601"
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    depends_on:
      - elasticsearch

volumes:
  grafana_data:
  elasticsearch_data:
```

Create `monitoring/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'jwt-api'
    static_configs:
      - targets: ['jwt-api:80']
    metrics_path: '/metrics'
    scrape_interval: 5s
```

```bash
# Start full observability stack
docker-compose -f docker-compose.observability.yml up -d

# Access services
echo "API: http://localhost:8080"
echo "Prometheus: http://localhost:9090"
echo "Grafana: http://localhost:3000 (admin/admin)"
echo "Jaeger: http://localhost:16686"
echo "Kibana: http://localhost:5601"
```

## 🔒 Security

### Non-Root User

The Docker image runs as a non-root user for security:

```dockerfile
# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser
```

### Secrets Management

**Development:**
```bash
# Use environment variables
docker run -e JWT_KEY="dev-key" jwt-api
```

**Production:**
```bash
# Use Docker secrets
echo "production-jwt-key" | docker secret create jwt_key -
docker service create \
  --secret jwt_key \
  --env JWT_KEY_FILE=/run/secrets/jwt_key \
  jwt-api
```

### Security Scanning

```bash
# Scan image for vulnerabilities
docker scan jwt-api:latest

# Use specific security scanners
trivy image jwt-api:latest
```

## 📊 Monitoring

### Health Checks

Built-in Docker health checks:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1
```

### Container Metrics

```bash
# Monitor container resources
docker stats jwt-api

# Get detailed container info
docker inspect jwt-api

# View container logs
docker logs -f jwt-api
```

### Memory Usage

Monitor memory optimization in action:

```bash
# Check memory headers
curl -v http://localhost:8080/health

# Monitor GC metrics
curl http://localhost:8080/metrics | jq '.memoryUsage'
```

## 🚀 Performance Optimization

### Build Optimization

```dockerfile
# Multi-stage build reduces image size
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ... build steps

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# Only runtime dependencies
```

### Runtime Optimization

```bash
# Memory limits
docker run --memory=512m --memory-swap=1g jwt-api

# CPU limits
docker run --cpus=1.0 jwt-api

# Combined resource limits
docker run \
  --memory=512m \
  --memory-swap=1g \
  --cpus=1.0 \
  --restart=unless-stopped \
  jwt-api
```

### Layer Caching

Optimize Dockerfile for better layer caching:

```dockerfile
# Copy project file first for dependency caching
COPY ["JwtApi.csproj", "."]
RUN dotnet restore

# Copy source code after dependencies
COPY . .
RUN dotnet build
```

## 🔧 Troubleshooting

### Common Issues

**1. Container Exits Immediately**
```bash
# Check container logs
docker logs jwt-api

# Run interactively
docker run -it jwt-api /bin/bash
```

**2. Port Already in Use**
```bash
# Find process using port
lsof -i :8080

# Use different port
docker run -p 8081:80 jwt-api
```

**3. Database Connection Issues**
```bash
# Test with in-memory database
docker run -e UseInMemoryDatabase=true jwt-api

# Check network connectivity
docker exec jwt-api curl sqlserver:1433
```

**4. Memory Issues**
```bash
# Increase memory limit
docker run --memory=1g jwt-api

# Monitor memory usage
docker stats jwt-api
```

### Debugging

```bash
# Run with debug environment
docker run -it \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e Logging__LogLevel__Default=Debug \
  jwt-api

# Access container shell
docker exec -it jwt-api /bin/bash

# View real-time logs
docker logs -f jwt-api
```

## 📋 Deployment Checklist

### Pre-Deployment

- [ ] Update JWT_KEY for production
- [ ] Configure database connection string
- [ ] Set appropriate environment variables
- [ ] Configure resource limits
- [ ] Set up health checks
- [ ] Configure logging and monitoring

### Post-Deployment

- [ ] Verify health check endpoint
- [ ] Test authentication flow
- [ ] Check application logs
- [ ] Monitor resource usage
- [ ] Verify database connectivity
- [ ] Test API endpoints

## 📚 Next Steps

1. **Kubernetes Deployment**: [kubernetes.md](kubernetes.md)
2. **Monitoring Setup**: [monitoring.md](monitoring.md)
3. **Performance Tuning**: [performance.md](performance.md)
4. **Troubleshooting**: [troubleshooting.md](troubleshooting.md)
5. **Configuration Reference**: [configuration.md](configuration.md)