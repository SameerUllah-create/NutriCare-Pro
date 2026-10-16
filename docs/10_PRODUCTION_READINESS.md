# Production Readiness Setup

## Runtime Hardening

Implemented:
- Serilog structured logging with console and rolling file sinks.
- Global exception handling with sanitized client responses and trace ids.
- Rate limiting with global IP limits and authenticated API policy.
- Output caching policy for safe public GET endpoints.
- Health checks at `/api/health`.
- Audit logging middleware for mutating API requests.
- Security headers for API and Next.js frontend.
- Configurable CORS through `AllowedOrigins`.

## OWASP Practices

Applied:
- No stack traces returned to clients.
- JWT authentication with role policies.
- Rate limiting for brute force and abuse reduction.
- HSTS, frame protection, content type protection, referrer policy, permissions policy.
- CORS allow-list instead of wildcard credentials.
- Upload modules validate file type, extension, size, and signatures.
- Refresh tokens are stored as hashes.
- Audit logs capture mutating requests.
- Production secrets are expected from environment variables or Azure secrets, not source control.

Recommended next:
- Add automated dependency scanning in GitHub Advanced Security or Dependabot.
- Move audit logging to an outbox/queue for very high traffic.
- Add distributed cache such as Azure Cache for Redis.
- Add WAF through Azure Front Door for public production traffic.
- Use Azure Key Vault references for all secrets.

## Docker

Build local production containers:

```powershell
docker compose up --build
```

Services:
- API: `http://localhost:7001`
- Portal: `http://localhost:3000`
- SQL Server: `localhost:1433`

## CI/CD

GitHub Actions workflow:
- Restores and builds backend.
- Installs and builds frontend.
- Builds API and portal Docker images.
- Pushes images to Azure Container Registry.
- Updates Azure Container Apps.

Required GitHub secrets:
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_CONTAINER_REGISTRY`
- `AZURE_CONTAINER_REGISTRY_NAME`
- `AZURE_API_CONTAINER_APP`
- `AZURE_PORTAL_CONTAINER_APP`

## Azure Deployment

Bicep template:
- Azure Container Registry
- Azure Container Apps environment
- API container app
- Portal container app
- Azure SQL Server and database
- Azure Storage account
- Log Analytics workspace

Deploy infrastructure:

```powershell
az deployment group create `
  --resource-group <resource-group> `
  --template-file infra/azure/main.bicep `
  --parameters `
    sqlAdminLogin=<admin-user> `
    sqlAdminPassword=<secure-password> `
    jwtSecretKey=<secure-jwt-secret> `
    containerRegistryName=<acr-name> `
    apiImage=<acr-login-server>/nutricare-api:latest `
    portalImage=<acr-login-server>/nutricare-portal:latest
```

## Health and Operations

Health endpoint:

```http
GET /api/health
```

Logs:
- Console logs for containers.
- Rolling file logs for local/server deployments.
- Azure Container Apps should stream console logs to Log Analytics.

## Environment Variables

Use `.env.example` as a local template. In Azure, configure values as Container App secrets:
- `ConnectionStrings__DefaultConnection`
- `Jwt__SecretKey`
- `SendGrid__ApiKey`
- `Notifications__Twilio__AccountSid`
- `Notifications__Twilio__AuthToken`
- `Notifications__Twilio__FromNumber`
- `AzureBlobStorage__ConnectionString`
- `AllowedOrigins__0`
