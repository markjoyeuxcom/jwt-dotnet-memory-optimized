# Monitoring & Observability Guide

Comprehensive guide for setting up monitoring, observability, and alerting for the JWT API using Prometheus, Grafana, Jaeger, OpenTelemetry, and the ELK stack.

## 📊 Overview

The JWT API provides extensive observability through:

- **Metrics**: Prometheus-compatible metrics for performance monitoring
- **Tracing**: OpenTelemetry distributed tracing with Jaeger integration
- **Logging**: Structured logging with Serilog and Elasticsearch integration
- **Health Checks**: Application and dependency health monitoring

## 🎯 Metrics

### Available Metrics

The API exposes comprehensive metrics at the `/metrics` endpoint:

#### HTTP Metrics
```
# Request duration histogram
jwt_api_dotnet_http_request_duration_seconds

# Request size histogram  
jwt_api_dotnet_http_request_size_bytes

# Response size histogram
jwt_api_dotnet_http_response_size_bytes

# Total HTTP requests counter
jwt_api_dotnet_http_requests_total
```

#### Authentication Metrics
```
# Authentication attempts counter
jwt_api_dotnet_auth_attempts_total

# Authentication failures counter
jwt_api_dotnet_auth_failures_total

# Token generation duration
jwt_api_dotnet_token_generation_duration_seconds

# Active sessions gauge
jwt_api_dotnet_active_sessions
```

#### Database Metrics
```
# Database query duration
jwt_api_dotnet_db_query_duration_seconds

# Database connection pool usage
jwt_api_dotnet_db_connections_active

# Database errors counter
jwt_api_dotnet_db_errors_total
```

#### Memory and Performance Metrics
```
# Memory allocations per request
jwt_api_dotnet_memory_allocated_bytes

# Cache hit rate
jwt_api_dotnet_cache_hit_rate

# Object pool usage
jwt_api_dotnet_object_pool_usage

# Thread pool metrics
jwt_api_dotnet_threadpool_active_threads
```

### Custom Business Metrics
```
# User registrations counter
jwt_api_dotnet_user_registrations_total

# Session affinity tracking
jwt_api_dotnet_session_affinity_total

# Rate limiting violations
jwt_api_dotnet_rate_limit_violations_total
```

## 🔧 Prometheus Setup

### Prometheus Configuration

`monitoring/prometheus.yml`:
```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

rule_files:
  - "jwt-api-rules.yml"

scrape_configs:
  - job_name: 'jwt-api'
    static_configs:
      - targets: ['jwt-api:80']
    metrics_path: '/metrics'
    scrape_interval: 5s
    scrape_timeout: 5s
    honor_labels: true
    
  - job_name: 'jwt-api-kubernetes'
    kubernetes_sd_configs:
      - role: endpoints
        namespaces:
          names:
            - jwt-api
    relabel_configs:
      - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
      - source_labels: [__address__, __meta_kubernetes_service_annotation_prometheus_io_port]
        action: replace
        regex: ([^:]+)(?::\d+)?;(\d+)
        replacement: $1:$2
        target_label: __address__
      - action: labelmap
        regex: __meta_kubernetes_service_label_(.+)
      - source_labels: [__meta_kubernetes_namespace]
        action: replace
        target_label: kubernetes_namespace
      - source_labels: [__meta_kubernetes_service_name]
        action: replace
        target_label: kubernetes_name

alerting:
  alertmanagers:
    - static_configs:
        - targets:
          - alertmanager:9093
```

### Alert Rules

`monitoring/jwt-api-rules.yml`:
```yaml
groups:
  - name: jwt-api.rules
    rules:
      # High error rate
      - alert: HighErrorRate
        expr: rate(jwt_api_dotnet_http_requests_total{status_code!~"2.."}[5m]) > 0.1
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value }} errors per second"

      # High response time
      - alert: HighResponseTime
        expr: histogram_quantile(0.95, rate(jwt_api_dotnet_http_request_duration_seconds_bucket[5m])) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High response time detected"
          description: "95th percentile response time is {{ $value }}s"

      # High memory usage
      - alert: HighMemoryUsage
        expr: jwt_api_dotnet_memory_allocated_bytes > 500000000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage detected"
          description: "Memory usage is {{ $value }} bytes"

      # Authentication failures
      - alert: HighAuthFailureRate
        expr: rate(jwt_api_dotnet_auth_failures_total[5m]) > 0.05
        for: 3m
        labels:
          severity: warning
        annotations:
          summary: "High authentication failure rate"
          description: "Auth failure rate is {{ $value }} failures per second"

      # Database connection issues
      - alert: DatabaseConnectionIssues
        expr: jwt_api_dotnet_db_connections_active == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "No active database connections"
          description: "Database appears to be unavailable"

      # Pod down
      - alert: JwtApiDown
        expr: up{job="jwt-api"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "JWT API instance is down"
          description: "JWT API instance {{ $labels.instance }} is down"
```

### Docker Compose for Monitoring Stack

`docker-compose.monitoring.yml`:
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
    volumes:
      - ./logs:/app/logs

  # Metrics
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - ./monitoring/jwt-api-rules.yml:/etc/prometheus/jwt-api-rules.yml
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'
      - '--web.enable-lifecycle'
      - '--web.enable-admin-api'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana-dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/grafana-datasources:/etc/grafana/provisioning/datasources

  alertmanager:
    image: prom/alertmanager:latest
    ports:
      - "9093:9093"
    volumes:
      - ./monitoring/alertmanager.yml:/etc/alertmanager/alertmanager.yml
      - alertmanager_data:/alertmanager

  # Tracing
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # HTTP collector
      - "14250:14250"  # gRPC collector
    environment:
      - COLLECTOR_OTLP_ENABLED=true

  # Logging
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
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
  prometheus_data:
  grafana_data:
  alertmanager_data:
  elasticsearch_data:
```

## 📈 Grafana Dashboards

### JWT API Dashboard

`monitoring/grafana-dashboards/jwt-api-dashboard.json`:
```json
{
  "dashboard": {
    "id": null,
    "title": "JWT API Monitoring",
    "tags": ["jwt-api"],
    "timezone": "browser",
    "panels": [
      {
        "title": "Request Rate",
        "type": "stat",
        "targets": [
          {
            "expr": "rate(jwt_api_dotnet_http_requests_total[5m])",
            "legendFormat": "Requests/sec"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "reqps",
            "min": 0
          }
        }
      },
      {
        "title": "Response Time",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.50, rate(jwt_api_dotnet_http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "50th percentile"
          },
          {
            "expr": "histogram_quantile(0.90, rate(jwt_api_dotnet_http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "90th percentile"
          },
          {
            "expr": "histogram_quantile(0.95, rate(jwt_api_dotnet_http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "95th percentile"
          }
        ]
      },
      {
        "title": "Error Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(jwt_api_dotnet_http_requests_total{status_code!~\"2..\"}[5m])",
            "legendFormat": "Error rate"
          }
        ]
      },
      {
        "title": "Memory Usage",
        "type": "graph",
        "targets": [
          {
            "expr": "jwt_api_dotnet_memory_allocated_bytes",
            "legendFormat": "{{pod_name}}"
          }
        ]
      },
      {
        "title": "Active Sessions",
        "type": "stat",
        "targets": [
          {
            "expr": "jwt_api_dotnet_active_sessions",
            "legendFormat": "Active Sessions"
          }
        ]
      },
      {
        "title": "Authentication Metrics",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(jwt_api_dotnet_auth_attempts_total[5m])",
            "legendFormat": "Attempts/sec"
          },
          {
            "expr": "rate(jwt_api_dotnet_auth_failures_total[5m])",
            "legendFormat": "Failures/sec"
          }
        ]
      }
    ],
    "time": {
      "from": "now-1h",
      "to": "now"
    },
    "refresh": "5s"
  }
}
```

### Performance Dashboard

Create additional dashboards for:
- **Memory Performance**: GC metrics, object pool usage, cache performance
- **Database Performance**: Query duration, connection pool, error rates
- **Business Metrics**: User registrations, session affinity, API usage patterns

## 🔍 Distributed Tracing

### OpenTelemetry Configuration

The API is instrumented with OpenTelemetry for distributed tracing:

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
      "Enabled": true
    }
  }
}
```

### Trace Instrumentation

The application automatically traces:

- **HTTP requests** (incoming and outgoing)
- **Database operations** via Entity Framework
- **Authentication flows**
- **Cache operations**
- **Custom business operations**

### Custom Span Creation

Add custom spans in your code:

```csharp
using var activity = ActivitySource.StartActivity("CustomOperation");
activity?.SetTag("user.id", userId);
activity?.SetTag("operation.type", "business-logic");

try
{
    // Your business logic
    await DoSomethingAsync();
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    throw;
}
```

## 📝 Structured Logging

### Serilog Configuration

The application uses Serilog with multiple sinks:

```json
{
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
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/jwt-api-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "buffered": true,
          "flushToDiskInterval": "00:00:01"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200",
          "indexFormat": "jwt-api-{0:yyyy.MM.dd}",
          "autoRegisterTemplate": true,
          "autoRegisterTemplateVersion": "ESv7"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithProcessId",
      "WithThreadId"
    ]
  }
}
```

### Log Correlation

Logs include correlation IDs for request tracking:

```json
{
  "@timestamp": "2024-01-15T10:30:45.123Z",
  "@level": "Information",
  "@message": "User login successful",
  "UserId": 123,
  "Email": "user@example.com",
  "CorrelationId": "abc123-def456",
  "RequestPath": "/api/auth/login",
  "ResponseTime": 145,
  "ClientIP": "192.168.1.100",
  "UserAgent": "Mozilla/5.0...",
  "PodName": "jwt-api-deployment-xyz"
}
```

### Kibana Setup

1. **Index Patterns**: Create index patterns for `jwt-api-*`
2. **Visualizations**: Create charts for error rates, response times, user activity
3. **Dashboards**: Combine visualizations into operational dashboards
4. **Alerts**: Set up Watcher alerts for error patterns

## 🚨 Alerting

### AlertManager Configuration

`monitoring/alertmanager.yml`:
```yaml
global:
  smtp_smarthost: 'localhost:587'
  smtp_from: 'alerts@company.com'

route:
  group_by: ['alertname']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 1h
  receiver: 'web.hook'

receivers:
  - name: 'web.hook'
    email_configs:
      - to: 'admin@company.com'
        subject: 'JWT API Alert: {{ .GroupLabels.alertname }}'
        body: |
          {{ range .Alerts }}
          Alert: {{ .Annotations.summary }}
          Description: {{ .Annotations.description }}
          {{ end }}
    
    slack_configs:
      - api_url: 'YOUR_SLACK_WEBHOOK_URL'
        channel: '#alerts'
        title: 'JWT API Alert'
        text: '{{ range .Alerts }}{{ .Annotations.summary }}{{ end }}'

inhibit_rules:
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['alertname', 'dev', 'instance']
```

### PagerDuty Integration

```yaml
receivers:
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: 'JWT API Alert: {{ .GroupLabels.alertname }}'
```

## 🏥 Health Checks

### Built-in Health Checks

The API provides comprehensive health checks at `/health`:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0045678",
      "data": {
        "connectionString": "Server=...;Database=JwtApiDb"
      }
    },
    "memory": {
      "status": "Healthy",
      "duration": "00:00:00.0001234",
      "data": {
        "allocatedBytes": 15728640,
        "maxBytes": 1073741824
      }
    }
  }
}
```

### Custom Health Checks

Add custom health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck("memory", () =>
    {
        var allocatedBytes = GC.GetTotalMemory(false);
        var maxBytes = 1_000_000_000; // 1GB limit
        
        return allocatedBytes < maxBytes 
            ? HealthCheckResult.Healthy($"Memory usage: {allocatedBytes:N0} bytes")
            : HealthCheckResult.Unhealthy($"Memory usage too high: {allocatedBytes:N0} bytes");
    })
    .AddCheck("external-api", async () =>
    {
        // Check external dependencies
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync("https://api.external.com/health");
        return response.IsSuccessStatusCode 
            ? HealthCheckResult.Healthy() 
            : HealthCheckResult.Unhealthy("External API unavailable");
    });
```

## 📊 Monitoring in Different Environments

### Development

```bash
# Start monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Access UIs
echo "Grafana: http://localhost:3000 (admin/admin)"
echo "Prometheus: http://localhost:9090"
echo "Jaeger: http://localhost:16686"
echo "Kibana: http://localhost:5601"
```

### Kubernetes

```bash
# Install monitoring stack with Helm
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add grafana https://grafana.github.io/helm-charts
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts

# Install Prometheus
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring --create-namespace

# Install Jaeger
helm install jaeger jaegertracing/jaeger \
  --namespace monitoring

# Apply service monitors
kubectl apply -f k8s/monitoring/
```

## 🔧 Troubleshooting Monitoring

### Common Issues

**1. No Metrics Available**
```bash
# Check metrics endpoint
curl http://localhost:8080/metrics

# Verify Prometheus scraping
kubectl logs -n monitoring deployment/prometheus-server
```

**2. Missing Traces**
```bash
# Check OpenTelemetry configuration
kubectl logs -n jwt-api deployment/jwt-api | grep -i telemetry

# Verify Jaeger collector
kubectl logs -n monitoring deployment/jaeger-collector
```

**3. Log Ingestion Issues**
```bash
# Check Elasticsearch health
curl http://localhost:9200/_cluster/health

# Verify log format
kubectl logs -n jwt-api deployment/jwt-api --tail=10
```

## 📚 Best Practices

1. **Set appropriate retention policies** for metrics and logs
2. **Use correlation IDs** for request tracing
3. **Monitor business metrics** not just technical metrics
4. **Set up proper alerting** to avoid alert fatigue
5. **Regular review** of dashboards and alerts
6. **Document runbooks** for common alert scenarios
7. **Test monitoring** in disaster recovery scenarios

## 📋 Monitoring Checklist

### Setup
- [ ] Prometheus collecting metrics
- [ ] Grafana dashboards configured
- [ ] Jaeger receiving traces
- [ ] Elasticsearch ingesting logs
- [ ] Health checks responding
- [ ] Alerts configured
- [ ] Runbooks documented

### Operations
- [ ] Regular dashboard reviews
- [ ] Alert tuning
- [ ] Capacity planning
- [ ] Performance baseline updates
- [ ] Monitoring stack updates
- [ ] Backup verification

## 📚 Next Steps

1. **Configuration Reference**: [configuration.md](configuration.md)
2. **Performance Tuning**: [performance.md](performance.md)
3. **Troubleshooting**: [troubleshooting.md](troubleshooting.md)
4. **API Reference**: [api-reference.md](api-reference.md)