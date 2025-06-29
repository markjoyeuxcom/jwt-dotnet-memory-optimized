# Kubernetes Deployment Guide

Complete guide for deploying the JWT API on Kubernetes, including production-ready manifests, scaling, monitoring, and best practices.

## 📋 Prerequisites

- Kubernetes cluster (1.24+)
- `kubectl` configured to access your cluster
- Container registry access (Docker Hub, ACR, ECR, etc.)
- Optional: Helm 3.x for package management

## 🚀 Quick Start

### 1. Build and Push Image

```bash
# Build the image
docker build -t your-registry/jwt-api:latest .

# Push to registry
docker push your-registry/jwt-api:latest
```

### 2. Deploy with In-Memory Database

```bash
# Apply all manifests
kubectl apply -f k8s/

# Wait for deployment
kubectl rollout status deployment/jwt-api

# Get service URL
kubectl get service jwt-api
```

## 📁 Kubernetes Manifests

### Namespace

`k8s/namespace.yaml`:
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: jwt-api
  labels:
    name: jwt-api
    environment: production
```

### ConfigMap

`k8s/configmap.yaml`:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: jwt-api-config
  namespace: jwt-api
data:
  # Application configuration
  ASPNETCORE_ENVIRONMENT: "Production"
  UseInMemoryDatabase: "false"
  
  # JWT Configuration
  Jwt__Issuer: "MyCompany.JwtApi"
  Jwt__Audience: "MyCompany.Users"
  Jwt__ExpiryMinutes: "15"
  Jwt__RefreshExpiryDays: "7"
  
  # Performance Optimization
  DOTNET_TieredPGO: "1"
  DOTNET_ReadyToRun: "1"
  DOTNET_TC_QuickJitForLoops: "1"
  DOTNET_gcServer: "1"
  
  # Logging
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft: "Warning"
  Logging__LogLevel__System: "Warning"
  
  # OpenTelemetry
  OpenTelemetry__Jaeger__Endpoint: "http://jaeger-collector:14268/api/traces"
  
  # Serilog
  Serilog__WriteTo__1__Name: "Elasticsearch"
  Serilog__WriteTo__1__Args__nodeUris: "http://elasticsearch:9200"
  Serilog__WriteTo__1__Args__indexFormat: "jwt-api-{0:yyyy.MM.dd}"
```

### Secrets

`k8s/secret.yaml`:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: jwt-api-secrets
  namespace: jwt-api
type: Opaque
data:
  # Base64 encoded values
  JWT_KEY: cHJvZHVjdGlvbi1qd3Qta2V5LW11c3QtYmUtYXQtbGVhc3QtMzItY2hhcmFjdGVycy1sb25nLWFuZC1zZWN1cmU=
  ConnectionStrings__DefaultConnection: U2VydmVyPXNxbHNlcnZlcjtEYXRhYmFzZT1Kd3RBcGlEYjtVc2VyIElkPXNhO1Bhc3N3b3JkPVlvdXJQYXNzd29yZDEyMzs=
```

```bash
# Create secrets from command line
kubectl create secret generic jwt-api-secrets \
  --from-literal=JWT_KEY="production-jwt-key-must-be-at-least-32-characters-long-and-secure" \
  --from-literal=ConnectionStrings__DefaultConnection="Server=sqlserver;Database=JwtApiDb;User Id=sa;Password=YourPassword123;" \
  --namespace jwt-api
```

### Deployment

`k8s/deployment.yaml`:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: jwt-api
  namespace: jwt-api
  labels:
    app: jwt-api
    version: v1
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: jwt-api
  template:
    metadata:
      labels:
        app: jwt-api
        version: v1
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "80"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: jwt-api
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: jwt-api
        image: your-registry/jwt-api:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 80
          name: http
          protocol: TCP
        env:
        # Pod Information
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP
        - name: NODE_NAME
          valueFrom:
            fieldRef:
              fieldPath: spec.nodeName
        # Configuration from ConfigMap
        envFrom:
        - configMapRef:
            name: jwt-api-config
        - secretRef:
            name: jwt-api-secrets
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 30
        volumeMounts:
        - name: logs
          mountPath: /app/logs
        securityContext:
          allowPrivilegeEscalation: false
          readOnlyRootFilesystem: true
          capabilities:
            drop:
            - ALL
      volumes:
      - name: logs
        emptyDir: {}
      nodeSelector:
        kubernetes.io/os: linux
      tolerations:
      - key: "node.kubernetes.io/unreachable"
        operator: "Exists"
        effect: "NoExecute"
        tolerationSeconds: 6000
```

### Service

`k8s/service.yaml`:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: jwt-api
  namespace: jwt-api
  labels:
    app: jwt-api
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "80"
    prometheus.io/path: "/metrics"
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
    name: http
  selector:
    app: jwt-api
```

### ServiceAccount

`k8s/serviceaccount.yaml`:
```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: jwt-api
  namespace: jwt-api
  labels:
    app: jwt-api

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: jwt-api
rules:
- apiGroups: [""]
  resources: ["pods", "services", "endpoints"]
  verbs: ["get", "list"]

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: jwt-api
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: jwt-api
subjects:
- kind: ServiceAccount
  name: jwt-api
  namespace: jwt-api
```

### Horizontal Pod Autoscaler

`k8s/hpa.yaml`:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: jwt-api-hpa
  namespace: jwt-api
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: jwt-api
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 10
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
      - type: Pods
        value: 2
        periodSeconds: 60
      selectPolicy: Max
```

### Pod Disruption Budget

`k8s/pdb.yaml`:
```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: jwt-api-pdb
  namespace: jwt-api
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: jwt-api
```

### Ingress

`k8s/ingress.yaml`:
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: jwt-api-ingress
  namespace: jwt-api
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/backend-protocol: "HTTP"
    nginx.ingress.kubernetes.io/proxy-body-size: "1m"
    nginx.ingress.kubernetes.io/proxy-connect-timeout: "10"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "120"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "120"
    # Rate limiting
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
    # Security headers
    nginx.ingress.kubernetes.io/configuration-snippet: |
      add_header X-Frame-Options DENY always;
      add_header X-Content-Type-Options nosniff always;
      add_header X-XSS-Protection "1; mode=block" always;
      add_header Referrer-Policy "strict-origin-when-cross-origin" always;
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.yourdomain.com
    secretName: jwt-api-tls
  rules:
  - host: api.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: jwt-api
            port:
              number: 80
```

## 🗄️ Database Deployment

### SQL Server StatefulSet

`k8s/database/sqlserver-statefulset.yaml`:
```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: sqlserver
  namespace: jwt-api
spec:
  serviceName: sqlserver
  replicas: 1
  selector:
    matchLabels:
      app: sqlserver
  template:
    metadata:
      labels:
        app: sqlserver
    spec:
      securityContext:
        runAsUser: 10001
        runAsGroup: 0
        fsGroup: 10001
      containers:
      - name: sqlserver
        image: mcr.microsoft.com/mssql/server:2022-latest
        ports:
        - containerPort: 1433
        env:
        - name: ACCEPT_EULA
          value: "Y"
        - name: SA_PASSWORD
          valueFrom:
            secretKeyRef:
              name: sqlserver-secret
              key: SA_PASSWORD
        - name: MSSQL_PID
          value: "Express"
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        volumeMounts:
        - name: sqlserver-data
          mountPath: /var/opt/mssql
        livenessProbe:
          exec:
            command:
            - /opt/mssql-tools/bin/sqlcmd
            - -S
            - localhost
            - -U
            - sa
            - -P
            - $(SA_PASSWORD)
            - -Q
            - SELECT 1
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          exec:
            command:
            - /opt/mssql-tools/bin/sqlcmd
            - -S
            - localhost
            - -U
            - sa
            - -P
            - $(SA_PASSWORD)
            - -Q
            - SELECT 1
          initialDelaySeconds: 10
          periodSeconds: 10
  volumeClaimTemplates:
  - metadata:
      name: sqlserver-data
    spec:
      accessModes: ["ReadWriteOnce"]
      storageClassName: fast-ssd
      resources:
        requests:
          storage: 100Gi
```

### SQL Server Service

`k8s/database/sqlserver-service.yaml`:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: sqlserver
  namespace: jwt-api
spec:
  selector:
    app: sqlserver
  ports:
  - port: 1433
    targetPort: 1433
  type: ClusterIP
```

## 📊 Monitoring Integration

### ServiceMonitor for Prometheus

`k8s/monitoring/servicemonitor.yaml`:
```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: jwt-api
  namespace: jwt-api
  labels:
    app: jwt-api
spec:
  selector:
    matchLabels:
      app: jwt-api
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
    scrapeTimeout: 10s
```

### Grafana Dashboard ConfigMap

`k8s/monitoring/grafana-dashboard.yaml`:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: jwt-api-dashboard
  namespace: monitoring
  labels:
    grafana_dashboard: "1"
data:
  jwt-api-dashboard.json: |
    {
      "dashboard": {
        "title": "JWT API Metrics",
        "panels": [
          {
            "title": "Request Rate",
            "type": "graph",
            "targets": [
              {
                "expr": "rate(jwt_api_dotnet_http_requests_total[5m])",
                "legendFormat": "{{method}} {{route}}"
              }
            ]
          },
          {
            "title": "Memory Usage",
            "type": "graph",
            "targets": [
              {
                "expr": "jwt_api_dotnet_memory_allocated_bytes",
                "legendFormat": "Allocated Memory"
              }
            ]
          }
        ]
      }
    }
```

## 🚀 Deployment Commands

### Create All Resources

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Create secrets (update values first)
kubectl apply -f k8s/secret.yaml

# Create configuration
kubectl apply -f k8s/configmap.yaml

# Create RBAC
kubectl apply -f k8s/serviceaccount.yaml

# Deploy application
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml

# Configure scaling and resilience
kubectl apply -f k8s/hpa.yaml
kubectl apply -f k8s/pdb.yaml

# Configure ingress (update domain first)
kubectl apply -f k8s/ingress.yaml

# Deploy database (optional)
kubectl apply -f k8s/database/
```

### Verify Deployment

```bash
# Check all resources
kubectl get all -n jwt-api

# Check deployment status
kubectl rollout status deployment/jwt-api -n jwt-api

# Check pod logs
kubectl logs -f deployment/jwt-api -n jwt-api

# Check service endpoints
kubectl get endpoints jwt-api -n jwt-api

# Test health endpoint
kubectl port-forward service/jwt-api 8080:80 -n jwt-api
curl http://localhost:8080/health
```

## 📈 Scaling

### Manual Scaling

```bash
# Scale to 5 replicas
kubectl scale deployment jwt-api --replicas=5 -n jwt-api

# Check scaling progress
kubectl get pods -n jwt-api -w
```

### Automatic Scaling

The HPA automatically scales based on CPU and memory:

```bash
# Check HPA status
kubectl get hpa jwt-api-hpa -n jwt-api

# Describe HPA for details
kubectl describe hpa jwt-api-hpa -n jwt-api

# Generate load to test scaling
kubectl run -i --tty load-generator --rm --image=busybox --restart=Never -- \
  /bin/sh -c "while sleep 0.01; do wget -q -O- http://jwt-api/health; done"
```

## 🔄 Updates and Rollbacks

### Rolling Updates

```bash
# Update image
kubectl set image deployment/jwt-api jwt-api=your-registry/jwt-api:v2 -n jwt-api

# Watch rollout
kubectl rollout status deployment/jwt-api -n jwt-api

# Check rollout history
kubectl rollout history deployment/jwt-api -n jwt-api
```

### Rollbacks

```bash
# Rollback to previous version
kubectl rollout undo deployment/jwt-api -n jwt-api

# Rollback to specific revision
kubectl rollout undo deployment/jwt-api --to-revision=2 -n jwt-api
```

## 🔒 Security

### Pod Security Standards

`k8s/security/pod-security-policy.yaml`:
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: jwt-api
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

### Network Policies

`k8s/security/network-policy.yaml`:
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: jwt-api-network-policy
  namespace: jwt-api
spec:
  podSelector:
    matchLabels:
      app: jwt-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 80
  - from:
    - namespaceSelector:
        matchLabels:
          name: monitoring
    ports:
    - protocol: TCP
      port: 80
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: sqlserver
    ports:
    - protocol: TCP
      port: 1433
  - to: []
    ports:
    - protocol: TCP
      port: 53
    - protocol: UDP
      port: 53
```

## 🔧 Troubleshooting

### Common Issues

**1. Pod Not Starting**
```bash
# Check pod status
kubectl describe pod -l app=jwt-api -n jwt-api

# Check logs
kubectl logs -l app=jwt-api -n jwt-api --previous

# Check events
kubectl get events -n jwt-api --sort-by='.lastTimestamp'
```

**2. Service Not Accessible**
```bash
# Check service endpoints
kubectl get endpoints jwt-api -n jwt-api

# Test service connectivity
kubectl run debug --image=busybox -it --rm --restart=Never -- \
  wget -qO- http://jwt-api.jwt-api.svc.cluster.local/health
```

**3. Database Connection Issues**
```bash
# Check database pod
kubectl logs sqlserver-0 -n jwt-api

# Test database connectivity
kubectl run sqlcmd --image=mcr.microsoft.com/mssql-tools -it --rm --restart=Never -- \
  sqlcmd -S sqlserver.jwt-api.svc.cluster.local -U sa -P YourPassword123
```

### Debug Commands

```bash
# Get all resources in namespace
kubectl get all -n jwt-api

# Describe deployment for issues
kubectl describe deployment jwt-api -n jwt-api

# Check resource usage
kubectl top pods -n jwt-api

# Access pod shell
kubectl exec -it deployment/jwt-api -n jwt-api -- /bin/bash

# Port forward for local testing
kubectl port-forward service/jwt-api 8080:80 -n jwt-api
```

## 🎯 Production Checklist

### Pre-Deployment

- [ ] Update container image tag
- [ ] Configure production secrets
- [ ] Set resource limits and requests
- [ ] Configure persistent storage for database
- [ ] Set up TLS certificates
- [ ] Configure monitoring and alerting
- [ ] Set up backup strategy
- [ ] Configure network policies
- [ ] Set up log aggregation

### Post-Deployment

- [ ] Verify all pods are running
- [ ] Test health endpoints
- [ ] Verify database connectivity
- [ ] Test API functionality
- [ ] Check monitoring dashboards
- [ ] Verify autoscaling works
- [ ] Test disaster recovery
- [ ] Document runbooks

## 📚 Next Steps

1. **Monitoring Setup**: [monitoring.md](monitoring.md)
2. **Configuration Reference**: [configuration.md](configuration.md)
3. **Performance Tuning**: [performance.md](performance.md)
4. **Troubleshooting**: [troubleshooting.md](troubleshooting.md)
5. **API Reference**: [api-reference.md](api-reference.md)