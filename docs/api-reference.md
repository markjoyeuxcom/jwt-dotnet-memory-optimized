# API Reference Guide

Complete API documentation with examples, request/response formats, authentication flows, and testing commands.

## 📡 Base Information

- **Base URL**: `http://localhost:8080` (Docker) or `http://localhost:5000` (Development)
- **API Version**: v1
- **Content-Type**: `application/json`
- **Authentication**: Bearer JWT tokens

## 🔐 Authentication Endpoints

### POST /api/auth/register

Register a new user account.

**Request:**
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "string",
  "email": "string",
  "password": "string",
  "confirmPassword": "string",
  "firstName": "string",
  "lastName": "string"
}
```

**Validation Rules:**
- Username: 3-50 characters, alphanumeric and underscore only
- Email: Valid email format
- Password: 8+ characters, at least one uppercase, lowercase, digit, and special character
- ConfirmPassword: Must match password
- FirstName/LastName: Optional, 1-100 characters

**Success Response (201):**
```json
{
  "message": "User registered successfully",
  "userId": 123,
  "podInfo": {
    "podName": "jwt-api-pod-1",
    "podIP": "10.244.0.1",
    "machineName": "jwt-api-pod-1",
    "timestamp": "2024-01-15T10:30:45.123Z"
  }
}
```

**Error Response (400):**
```json
{
  "error": "User with this username or email already exists"
}
```

**cURL Example:**
```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "johndoe",
    "email": "john.doe@example.com",
    "password": "SecurePassword123!",
    "confirmPassword": "SecurePassword123!",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

### POST /api/auth/login

Authenticate user and receive JWT tokens.

**Request:**
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "string",
  "password": "string"
}
```

**Success Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4=",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": 123,
    "username": "johndoe",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "displayName": "John Doe",
    "initials": "JD",
    "role": "User",
    "isActive": true,
    "createdAt": "2024-01-15T09:00:00Z",
    "lastLoginAt": "2024-01-15T10:30:45Z"
  },
  "podInfo": {
    "podName": "jwt-api-pod-1",
    "podIP": "10.244.0.1",
    "machineName": "jwt-api-pod-1",
    "timestamp": "2024-01-15T10:30:45.123Z"
  }
}
```

**Error Response (401):**
```json
{
  "error": "Invalid email or password"
}
```

**cURL Example:**
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePassword123!"
  }'
```

### POST /api/auth/refresh

Refresh access token using refresh token.

**Request:**
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "string"
}
```

**Success Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4=",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": 123,
    "username": "johndoe",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "displayName": "John Doe",
    "initials": "JD",
    "role": "User",
    "isActive": true,
    "createdAt": "2024-01-15T09:00:00Z",
    "lastLoginAt": "2024-01-15T10:30:45Z"
  }
}
```

**cURL Example:**
```bash
curl -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "your-refresh-token-here"
  }'
```

### POST /api/auth/logout

Logout user and invalidate refresh token.

**Request:**
```http
POST /api/auth/logout
Content-Type: application/json
Authorization: Bearer {accessToken}

{
  "refreshToken": "string"
}
```

**Success Response (200):**
```json
{
  "message": "Logout successful"
}
```

**cURL Example:**
```bash
curl -X POST http://localhost:8080/api/auth/logout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "refreshToken": "your-refresh-token-here"
  }'
```

### GET /api/auth/verify

Verify access token and get user information.

**Request:**
```http
GET /api/auth/verify
Authorization: Bearer {accessToken}
```

**Success Response (200):**
```json
{
  "valid": true,
  "user": {
    "id": 123,
    "username": "johndoe",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "displayName": "John Doe",
    "initials": "JD",
    "role": "User",
    "isActive": true,
    "createdAt": "2024-01-15T09:00:00Z",
    "lastLoginAt": "2024-01-15T10:30:45Z"
  },
  "tokenInfo": {
    "issuedAt": "2024-01-15T10:30:45Z",
    "expiresAt": "2024-01-15T10:45:45Z",
    "issuer": "JwtApi",
    "audience": "JwtApiUsers"
  }
}
```

**cURL Example:**
```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  http://localhost:8080/api/auth/verify
```

## 🧪 Testing Endpoints

### GET /api/auth/test/session-affinity

Test session affinity and load balancing.

**Request:**
```http
GET /api/auth/test/session-affinity
Authorization: Bearer {accessToken}
```

**Response (200):**
```json
{
  "message": "Session affinity test successful",
  "user": {
    "id": 123,
    "username": "johndoe"
  },
  "sessionInfo": {
    "sessionId": "session-abc123",
    "createdAt": "2024-01-15T10:30:45Z",
    "ipAddress": "192.168.1.100",
    "userAgent": "Mozilla/5.0..."
  },
  "podInfo": {
    "podName": "jwt-api-pod-1",
    "podIP": "10.244.0.1",
    "machineName": "jwt-api-pod-1",
    "nodeInfo": {
      "nodeName": "k8s-node-1",
      "zone": "us-west-2a"
    }
  },
  "performanceMetrics": {
    "memoryUsage": 15728640,
    "responseTime": 45,
    "cacheStats": {
      "hits": 1234,
      "misses": 89,
      "hitRate": 0.933
    }
  }
}
```

**cURL Example:**
```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  http://localhost:8080/api/auth/test/session-affinity
```

## 📊 Monitoring Endpoints

### GET /health

Application health check.

**Request:**
```http
GET /health
```

**Response (200 - Healthy):**
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

**Response (503 - Unhealthy):**
```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:00.0523456",
  "entries": {
    "database": {
      "status": "Unhealthy",
      "duration": "00:00:00.0500000",
      "exception": "Unable to connect to database",
      "data": {}
    }
  }
}
```

**cURL Example:**
```bash
curl http://localhost:8080/health
```

### GET /metrics

Prometheus-compatible metrics.

**Request:**
```http
GET /metrics
```

**Response (200):**
```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "memoryUsage": {
    "totalAllocated": 15728640,
    "gen0Collections": 12,
    "gen1Collections": 3,
    "gen2Collections": 1
  },
  "podInfo": {
    "machineName": "jwt-api-pod-1",
    "processorCount": 4,
    "workingSet": 67108864,
    "podName": "jwt-api-pod-1",
    "podIP": "10.244.0.1"
  },
  "performance": {
    "requestsPerSecond": 125.5,
    "averageResponseTime": 45.2,
    "activeConnections": 23,
    "cacheHitRate": 0.933
  }
}
```

**cURL Example:**
```bash
curl http://localhost:8080/metrics
```

## 🔄 Complete Authentication Flow

### 1. User Registration and Login

```bash
#!/bin/bash

# 1. Register a new user
echo "Registering user..."
REGISTER_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "SecurePassword123!",
    "confirmPassword": "SecurePassword123!",
    "firstName": "Test",
    "lastName": "User"
  }')

echo "Registration response: $REGISTER_RESPONSE"

# 2. Login with the user
echo "Logging in..."
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }')

# 3. Extract tokens
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken')
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.refreshToken')

echo "Access Token: $ACCESS_TOKEN"
echo "Refresh Token: $REFRESH_TOKEN"

# 4. Use access token
echo "Verifying token..."
curl -H "Authorization: Bearer $ACCESS_TOKEN" \
  http://localhost:8080/api/auth/verify | jq

# 5. Test session affinity
echo "Testing session affinity..."
curl -H "Authorization: Bearer $ACCESS_TOKEN" \
  http://localhost:8080/api/auth/test/session-affinity | jq

# 6. Refresh token
echo "Refreshing token..."
REFRESH_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}")

NEW_ACCESS_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.accessToken')
NEW_REFRESH_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.refreshToken')

echo "New Access Token: $NEW_ACCESS_TOKEN"

# 7. Logout
echo "Logging out..."
curl -X POST http://localhost:8080/api/auth/logout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $NEW_ACCESS_TOKEN" \
  -d "{\"refreshToken\": \"$NEW_REFRESH_TOKEN\"}" | jq
```

### 2. Load Testing Script

```bash
#!/bin/bash

# Load test the API
echo "Starting load test..."

# Function to make authenticated request
make_request() {
  local token=$1
  curl -s -H "Authorization: Bearer $token" \
    http://localhost:8080/api/auth/verify > /dev/null
}

# Get token first
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }')

ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken')

# Run concurrent requests
echo "Running 100 concurrent requests..."
for i in {1..100}; do
  make_request "$ACCESS_TOKEN" &
done

wait
echo "Load test completed"

# Check metrics
echo "Final metrics:"
curl -s http://localhost:8080/metrics | jq '.performance'
```

## 📝 Response Headers

All API responses include performance and monitoring headers:

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
X-Request-ID: abc123-def456-ghi789
X-Memory-Initial: 15728640
X-Memory-Final: 15892480
X-Memory-Delta: 163840
X-Response-Time: 45
X-GC-Gen0: 12
X-GC-Gen1: 3
X-GC-Gen2: 1
X-Pod-Name: jwt-api-pod-1
X-Pod-IP: 10.244.0.1
X-Machine-Name: jwt-api-pod-1
X-Server-Framework: .NET 8.0
X-Memory-Optimized: true
```

## ❌ Error Responses

### Common HTTP Status Codes

| Status | Description | Example |
|--------|-------------|---------|
| 200 | Success | Request completed successfully |
| 201 | Created | User registered successfully |
| 400 | Bad Request | Invalid request format or validation errors |
| 401 | Unauthorized | Invalid credentials or expired token |
| 403 | Forbidden | Valid token but insufficient permissions |
| 404 | Not Found | Endpoint not found |
| 409 | Conflict | User already exists |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Service temporarily unavailable |

### Error Response Format

```json
{
  "error": "Error description",
  "details": "Additional error details (optional)",
  "timestamp": "2024-01-15T10:30:45.123Z",
  "traceId": "abc123-def456-ghi789",
  "podInfo": {
    "podName": "jwt-api-pod-1",
    "timestamp": "2024-01-15T10:30:45.123Z"
  }
}
```

### Validation Error Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is required."],
    "Password": ["Password must be at least 8 characters long."]
  },
  "traceId": "abc123-def456-ghi789"
}
```

## 🧪 Testing with Different Tools

### Postman Collection

Create a Postman collection with the following requests:

1. **Register User**
   - Method: POST
   - URL: `{{baseUrl}}/api/auth/register`
   - Body: JSON with user data
   - Tests: Verify status code, extract userId

2. **Login**
   - Method: POST
   - URL: `{{baseUrl}}/api/auth/login`
   - Body: JSON with credentials
   - Tests: Extract and save tokens

3. **Verify Token**
   - Method: GET
   - URL: `{{baseUrl}}/api/auth/verify`
   - Headers: Authorization with Bearer token
   - Tests: Verify user data

### HTTPie Examples

```bash
# Register
http POST localhost:8080/api/auth/register \
  username=testuser \
  email=test@example.com \
  password=SecurePassword123! \
  confirmPassword=SecurePassword123! \
  firstName=Test \
  lastName=User

# Login
http POST localhost:8080/api/auth/login \
  email=test@example.com \
  password=SecurePassword123!

# Verify (replace TOKEN with actual token)
http GET localhost:8080/api/auth/verify \
  Authorization:"Bearer TOKEN"
```

### JavaScript/Node.js Example

```javascript
const axios = require('axios');

const baseURL = 'http://localhost:8080';
const api = axios.create({ baseURL });

async function testAPI() {
  try {
    // Register
    const registerResponse = await api.post('/api/auth/register', {
      username: 'testuser',
      email: 'test@example.com',
      password: 'SecurePassword123!',
      confirmPassword: 'SecurePassword123!',
      firstName: 'Test',
      lastName: 'User'
    });
    console.log('Registration:', registerResponse.data);

    // Login
    const loginResponse = await api.post('/api/auth/login', {
      email: 'test@example.com',
      password: 'SecurePassword123!'
    });
    
    const { accessToken, refreshToken } = loginResponse.data;
    console.log('Login successful');

    // Verify
    const verifyResponse = await api.get('/api/auth/verify', {
      headers: { Authorization: `Bearer ${accessToken}` }
    });
    console.log('Verify:', verifyResponse.data);

    // Test session affinity
    const sessionResponse = await api.get('/api/auth/test/session-affinity', {
      headers: { Authorization: `Bearer ${accessToken}` }
    });
    console.log('Session affinity:', sessionResponse.data);

  } catch (error) {
    console.error('Error:', error.response?.data || error.message);
  }
}

testAPI();
```

## 📚 Next Steps

1. **Troubleshooting Guide**: [troubleshooting.md](troubleshooting.md)
2. **Performance Tuning**: [performance.md](performance.md)
3. **Configuration Reference**: [configuration.md](configuration.md)
4. **Monitoring Setup**: [monitoring.md](monitoring.md)