# Troubleshooting Guide

Comprehensive troubleshooting guide for common issues, debugging techniques, and solutions for the JWT API.

## 🔍 Common Issues

### 1. Application Startup Issues

#### Issue: Application fails to start

**Symptoms:**
- Application exits immediately
- "Failed to bind to address" errors
- Configuration validation errors

**Diagnosis:**
```bash
# Check application logs
dotnet run --verbosity diagnostic

# Check port availability
lsof -i :5000
netstat -tulpn | grep :5000

# Verify configuration
dotnet run --launch-profile Development
```

**Solutions:**

**A. Port already in use:**
```bash
# Kill process using port
sudo lsof -t -i:5000 | xargs kill -9

# Or use different port
dotnet run --urls "http://localhost:5001"
```

**B. Configuration issues:**
```bash
# Check JWT key length
echo -n "$JWT_KEY" | wc -c  # Should be >= 32

# Verify configuration file exists
ls -la appsettings*.json

# Check environment variables
env | grep -E "(JWT|ASPNETCORE|ConnectionStrings)"
```

**C. Missing dependencies:**
```bash
# Restore NuGet packages
dotnet restore --force

# Clear package cache
dotnet nuget locals all --clear
dotnet restore
```

### 2. Database Connection Issues

#### Issue: Cannot connect to database

**Symptoms:**
- "Database connection failed" errors
- Health check shows database as unhealthy
- Entity Framework exceptions

**Diagnosis:**
```bash
# Test database connectivity
# For SQL Server:
sqlcmd -S localhost -U sa -P YourPassword123 -Q "SELECT 1"

# For Docker SQL Server:
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P YourPassword123 -Q "SELECT 1"

# Check connection string format
echo "$ConnectionStrings__DefaultConnection"
```

**Solutions:**

**A. Use in-memory database for development:**
```bash
export UseInMemoryDatabase=true
dotnet run
```

**B. Fix SQL Server connection:**
```bash
# Start SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123" \
  -p 1433:1433 --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest

# Update connection string
export ConnectionStrings__DefaultConnection="Server=localhost;Database=JwtApiDb;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
```

**C. Connection string format issues:**
```bash
# Correct format for SQL Server
ConnectionStrings__DefaultConnection="Server=localhost;Database=JwtApiDb;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"

# For Windows Authentication
ConnectionStrings__DefaultConnection="Server=localhost;Database=JwtApiDb;Trusted_Connection=true;"

# For Azure SQL
ConnectionStrings__DefaultConnection="Server=tcp:yourserver.database.windows.net,1433;Database=JwtApiDb;User ID=yourusername;Password=yourpassword;Encrypt=True;"
```

### 3. Authentication Issues

#### Issue: JWT token validation fails

**Symptoms:**
- 401 Unauthorized responses
- "Invalid token" errors
- Token appears expired immediately

**Diagnosis:**
```bash
# Check JWT key configuration
echo "JWT Key length: $(echo -n "$JWT_KEY" | wc -c)"

# Decode JWT token (without verification)
echo "TOKEN_HERE" | cut -d. -f2 | base64 -d | jq

# Check system time
date
timedatectl status  # Linux
```

**Solutions:**

**A. JWT key too short:**
```bash
# Set proper JWT key (minimum 32 characters)
export JWT_KEY="your-super-secret-jwt-key-change-this-in-production-must-be-at-least-32-characters-long"
```

**B. Time synchronization issues:**
```bash
# Synchronize system time
sudo ntpdate -s time.nist.gov  # Linux
sudo sntp -sS time.apple.com   # macOS

# Or disable clock skew validation for development
# In appsettings.Development.json:
"Jwt": {
  "ClockSkew": "00:05:00"  // 5 minute tolerance
}
```

**C. Token issuer/audience mismatch:**
```json
{
  "Jwt": {
    "Issuer": "JwtApi",
    "Audience": "JwtApiUsers",
    "ValidateIssuer": true,
    "ValidateAudience": true
  }
}
```

### 4. Memory and Performance Issues

#### Issue: High memory usage or memory leaks

**Symptoms:**
- Memory usage continuously increasing
- OutOfMemoryException
- Slow response times

**Diagnosis:**
```bash
# Monitor memory usage
dotnet-counters monitor --process-id $(pgrep -f JwtApi) System.Runtime

# Check GC collections
curl -s http://localhost:5000/metrics | jq '.memoryUsage'

# Monitor via response headers
curl -v http://localhost:5000/health 2>&1 | grep -E "X-Memory|X-GC"
```

**Solutions:**

**A. Enable server GC:**
```bash
export DOTNET_gcServer=1
export DOTNET_GCHeapCount=0
```

**B. Tune cache settings:**
```json
{
  "Cache": {
    "SizeLimit": 50000,
    "CompactionPercentage": 0.5,
    "ExpirationScanFrequency": "00:02:00"
  }
}
```

**C. Review memory monitoring threshold:**
```json
{
  "Memory": {
    "HighAllocationThreshold": 10000000,
    "LogHighAllocations": true
  }
}
```

### 5. Docker Issues

#### Issue: Docker container fails to start

**Symptoms:**
- Container exits with code 1
- "No such file or directory" errors
- Permission denied errors

**Diagnosis:**
```bash
# Check container logs
docker logs jwt-api

# Run container interactively
docker run -it jwt-api /bin/bash

# Check file permissions
docker run jwt-api ls -la /app
```

**Solutions:**

**A. File permission issues:**
```dockerfile
# In Dockerfile, ensure proper ownership
RUN chown -R appuser:appuser /app
USER appuser
```

**B. Missing environment variables:**
```bash
# Set required environment variables
docker run -e UseInMemoryDatabase=true \
  -e JWT_KEY="development-key-at-least-32-characters" \
  jwt-api
```

**C. Port binding issues:**
```bash
# Check if port is already in use
docker ps | grep 8080

# Use different port
docker run -p 8081:80 jwt-api
```

### 6. Kubernetes Issues

#### Issue: Pods not starting or crashing

**Symptoms:**
- Pods in CrashLoopBackOff state
- ImagePullBackOff errors
- Configuration errors

**Diagnosis:**
```bash
# Check pod status
kubectl get pods -n jwt-api

# Describe pod for events
kubectl describe pod -l app=jwt-api -n jwt-api

# Check logs
kubectl logs -l app=jwt-api -n jwt-api --previous

# Check events
kubectl get events -n jwt-api --sort-by='.lastTimestamp'
```

**Solutions:**

**A. Image pull issues:**
```bash
# Verify image exists
docker pull your-registry/jwt-api:latest

# Check image pull secrets
kubectl get secrets -n jwt-api
kubectl describe secret regcred -n jwt-api
```

**B. Configuration issues:**
```bash
# Check ConfigMap
kubectl describe configmap jwt-api-config -n jwt-api

# Check Secrets
kubectl describe secret jwt-api-secrets -n jwt-api

# Verify environment variables in pod
kubectl exec -it deployment/jwt-api -n jwt-api -- env | grep JWT
```

**C. Resource constraints:**
```bash
# Check resource usage
kubectl top pods -n jwt-api

# Describe node for capacity
kubectl describe node

# Adjust resource requests/limits
kubectl patch deployment jwt-api -n jwt-api -p '{"spec":{"template":{"spec":{"containers":[{"name":"jwt-api","resources":{"requests":{"memory":"512Mi","cpu":"500m"}}}]}}}}'
```

## 🔧 Debugging Techniques

### 1. Enable Debug Logging

**Development:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "JwtApi": "Trace",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

**Production debugging (temporary):**
```bash
# Set environment variable
kubectl set env deployment/jwt-api -n jwt-api Logging__LogLevel__Default=Debug

# Revert after debugging
kubectl set env deployment/jwt-api -n jwt-api Logging__LogLevel__Default=Information
```

### 2. Memory Profiling

**Enable detailed memory monitoring:**
```json
{
  "Memory": {
    "MonitoringEnabled": true,
    "LogHighAllocations": true,
    "HighAllocationThreshold": 1000000
  }
}
```

**Use dotnet diagnostic tools:**
```bash
# Install tools
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-dump
dotnet tool install --global dotnet-trace

# Monitor real-time metrics
dotnet-counters monitor --process-id $(pgrep -f JwtApi) System.Runtime

# Capture memory dump
dotnet-dump collect --process-id $(pgrep -f JwtApi)

# Analyze dump
dotnet-dump analyze core_20240115_103045
```

### 3. Database Query Debugging

**Enable SQL logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Check database connections:**
```bash
# Monitor active connections
kubectl exec -it sqlserver-0 -n jwt-api -- \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourPassword123 \
  -Q "SELECT COUNT(*) as ActiveConnections FROM sys.dm_exec_connections"
```

### 4. Network Connectivity Testing

**Test internal service connectivity:**
```bash
# From within cluster
kubectl run debug --image=busybox -it --rm --restart=Never -- \
  wget -qO- http://jwt-api.jwt-api.svc.cluster.local/health

# Test database connectivity
kubectl run debug --image=busybox -it --rm --restart=Never -- \
  nc -zv sqlserver.jwt-api.svc.cluster.local 1433
```

### 5. Performance Debugging

**Enable detailed performance metrics:**
```bash
# Add performance headers to all responses
curl -v http://localhost:8080/health 2>&1 | grep -E "X-.*"

# Monitor specific endpoints
curl -w "@curl-format.txt" -o /dev/null -s http://localhost:8080/api/auth/login

# curl-format.txt content:
#     time_namelookup:  %{time_namelookup}\n
#        time_connect:  %{time_connect}\n
#     time_appconnect:  %{time_appconnect}\n
#    time_pretransfer:  %{time_pretransfer}\n
#       time_redirect:  %{time_redirect}\n
#  time_starttransfer:  %{time_starttransfer}\n
#                     ----------\n
#          time_total:  %{time_total}\n
```

## 🚨 Emergency Procedures

### 1. Service Recovery

**Quick recovery steps:**
```bash
# Restart deployment
kubectl rollout restart deployment/jwt-api -n jwt-api

# Scale down and up
kubectl scale deployment jwt-api --replicas=0 -n jwt-api
kubectl scale deployment jwt-api --replicas=3 -n jwt-api

# Rollback to previous version
kubectl rollout undo deployment/jwt-api -n jwt-api
```

### 2. Database Recovery

**Database connection issues:**
```bash
# Switch to in-memory database temporarily
kubectl patch deployment jwt-api -n jwt-api -p '{"spec":{"template":{"spec":{"containers":[{"name":"jwt-api","env":[{"name":"UseInMemoryDatabase","value":"true"}]}]}}}}'

# Restart database
kubectl delete pod sqlserver-0 -n jwt-api
```

### 3. Memory Issues

**High memory usage:**
```bash
# Trigger garbage collection
curl -X POST http://localhost:8080/api/admin/gc

# Restart pods with memory issues
kubectl delete pod -l app=jwt-api -n jwt-api --field-selector=status.phase=Running

# Temporarily increase memory limits
kubectl patch deployment jwt-api -n jwt-api -p '{"spec":{"template":{"spec":{"containers":[{"name":"jwt-api","resources":{"limits":{"memory":"1Gi"}}}]}}}}'
```

## 📊 Monitoring and Alerting

### 1. Health Check Monitoring

**Continuous health monitoring:**
```bash
#!/bin/bash
while true; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/health)
  if [ "$STATUS" != "200" ]; then
    echo "$(date): Health check failed with status $STATUS"
    # Send alert
  else
    echo "$(date): Health check OK"
  fi
  sleep 30
done
```

### 2. Log Analysis

**Search for common error patterns:**
```bash
# Authentication failures
kubectl logs -l app=jwt-api -n jwt-api | grep -i "authentication failed"

# Memory issues
kubectl logs -l app=jwt-api -n jwt-api | grep -i "memory"

# Database errors
kubectl logs -l app=jwt-api -n jwt-api | grep -i "database"

# Recent errors
kubectl logs -l app=jwt-api -n jwt-api --since=1h | grep -i error
```

### 3. Performance Monitoring

**Key metrics to monitor:**
```bash
# Response times
curl -s http://localhost:8080/metrics | jq '.performance.averageResponseTime'

# Memory usage
curl -s http://localhost:8080/metrics | jq '.memoryUsage.totalAllocated'

# Active connections
curl -s http://localhost:8080/metrics | jq '.performance.activeConnections'

# Cache hit rate
curl -s http://localhost:8080/metrics | jq '.performance.cacheHitRate'
```

## 🔍 Diagnostic Scripts

### 1. Complete Health Check Script

```bash
#!/bin/bash

echo "=== JWT API Diagnostic Report ==="
echo "Date: $(date)"
echo

# Basic connectivity
echo "1. Basic Connectivity:"
if curl -s http://localhost:8080/health > /dev/null; then
  echo "   ✓ API is responding"
else
  echo "   ✗ API is not responding"
fi

# Authentication test
echo "2. Authentication Test:"
AUTH_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPassword123"}' \
  -w "%{http_code}")

if echo "$AUTH_RESPONSE" | grep -q "accessToken"; then
  echo "   ✓ Authentication working"
else
  echo "   ✗ Authentication failed"
fi

# Memory metrics
echo "3. Memory Metrics:"
METRICS=$(curl -s http://localhost:8080/metrics)
if [ $? -eq 0 ]; then
  echo "   Memory: $(echo "$METRICS" | jq -r '.memoryUsage.totalAllocated') bytes"
  echo "   GC Gen0: $(echo "$METRICS" | jq -r '.memoryUsage.gen0Collections')"
  echo "   GC Gen1: $(echo "$METRICS" | jq -r '.memoryUsage.gen1Collections')"
  echo "   GC Gen2: $(echo "$METRICS" | jq -r '.memoryUsage.gen2Collections')"
else
  echo "   ✗ Could not retrieve metrics"
fi

# Database connectivity
echo "4. Database Status:"
HEALTH=$(curl -s http://localhost:8080/health)
DB_STATUS=$(echo "$HEALTH" | jq -r '.entries.database.status')
if [ "$DB_STATUS" = "Healthy" ]; then
  echo "   ✓ Database healthy"
else
  echo "   ✗ Database issues: $DB_STATUS"
fi

echo
echo "=== End Diagnostic Report ==="
```

### 2. Performance Benchmark Script

```bash
#!/bin/bash

echo "=== Performance Benchmark ==="

# Warm up
echo "Warming up..."
for i in {1..10}; do
  curl -s http://localhost:8080/health > /dev/null
done

# Benchmark health endpoint
echo "Benchmarking health endpoint..."
ab -n 1000 -c 10 http://localhost:8080/health

# Benchmark with authentication
echo "Getting auth token..."
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPassword123"}' | \
  jq -r '.accessToken')

if [ "$TOKEN" != "null" ] && [ -n "$TOKEN" ]; then
  echo "Benchmarking authenticated endpoint..."
  ab -n 1000 -c 10 -H "Authorization: Bearer $TOKEN" \
    http://localhost:8080/api/auth/verify
else
  echo "Could not get auth token for benchmark"
fi

echo "=== Benchmark Complete ==="
```

## 📚 Additional Resources

### 1. Log Files Locations

- **Local Development**: `logs/jwt-api-YYYYMMDD.txt`
- **Docker**: `/app/logs/jwt-api-YYYYMMDD.txt`
- **Kubernetes**: `kubectl logs -l app=jwt-api -n jwt-api`

### 2. Configuration Files

- **Local**: `appsettings.json`, `appsettings.Development.json`
- **Docker**: Environment variables and mounted volumes
- **Kubernetes**: ConfigMaps and Secrets

### 3. Useful Commands Reference

```bash
# Local development
dotnet run --verbosity diagnostic
dotnet watch run

# Docker
docker logs jwt-api --follow
docker exec -it jwt-api /bin/bash
docker stats jwt-api

# Kubernetes
kubectl get pods -n jwt-api
kubectl describe pod POD_NAME -n jwt-api
kubectl logs POD_NAME -n jwt-api --follow
kubectl exec -it POD_NAME -n jwt-api -- /bin/bash
kubectl port-forward service/jwt-api 8080:80 -n jwt-api
```

## 🆘 When to Escalate

Escalate issues when:

1. **Critical Production Impact**: Service completely unavailable
2. **Data Loss Risk**: Database corruption or connection failures
3. **Security Incidents**: Authentication bypass or token compromise
4. **Performance Degradation**: Response times > 5 seconds consistently
5. **Memory Leaks**: Memory usage growing unbounded
6. **Infrastructure Issues**: Kubernetes cluster problems

Include in escalation:
- Diagnostic report output
- Recent logs (last 1 hour)
- Configuration changes made
- Steps already attempted
- Current system metrics

## 📚 Next Steps

1. **Performance Tuning**: [performance.md](performance.md)
2. **Configuration Reference**: [configuration.md](configuration.md)
3. **Monitoring Setup**: [monitoring.md](monitoring.md)
4. **API Reference**: [api-reference.md](api-reference.md)