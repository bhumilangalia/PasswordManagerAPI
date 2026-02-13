# Production Deployment Guide

## Required Environment Variables

### JWT Configuration (CRITICAL)

```bash
export JWT_SECRET_KEY="<64-character-random-string>"
export ASPNETCORE_ENVIRONMENT="Production"
```

**Generate secure JWT key:**

```bash
# Linux/macOS
openssl rand -base64 64

# Windows PowerShell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

## Pre-Deployment Checklist

- [ ] JWT_SECRET_KEY environment variable configured (64+ chars)
- [ ] ASPNETCORE_ENVIRONMENT=Production
- [ ] HTTPS certificate installed and configured
- [ ] Database file permissions restricted (chmod 600)
- [ ] .gitignore verified (*.db, *.log, appsettings.*.json excluded)
- [ ] Security tests passing: `dotnet test --filter Category=Security`

## Deployment Commands

```bash
# Build release
dotnet publish -c Release -o ./publish

# Run in production
cd publish
export JWT_SECRET_KEY="<your-secret-key>"
export ASPNETCORE_ENVIRONMENT="Production"
dotnet PasswordManagerApi.dll
```

## Security Validation

After deployment verify:

1. **HTTP redirects to HTTPS**
   ```bash
   curl -I http://your-domain.com/
   # Should return 301/308 with Location: https://...
   ```

2. **HSTS header present**
   ```bash
   curl -I https://your-domain.com/
   # Should include: Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
   ```

3. **Security headers present**
   ```bash
   curl -I https://your-domain.com/api/passwords
   # Should include: X-Content-Type-Options, X-Frame-Options, Content-Security-Policy
   ```

4. **Invalid tokens return 401**
   ```bash
   curl https://your-domain.com/api/passwords
   # Should return 401 Unauthorized
   ```

5. **Rate limiting active**
   ```bash
   # Send 6 rapid requests to login endpoint
   for i in {1..6}; do
     curl -X POST https://your-domain.com/api/auth/login \
       -H "Content-Type: application/json" \
       -d '{"username":"test","password":"test"}'
   done
   # 6th request should return 429 Too Many Requests
   ```

6. **Database file permissions**
   ```bash
   ls -l passwordmanager.db
   # Should show: -rw------- (chmod 600)
   ```

## Secrets Management Options

### Option 1: Environment Variables (Simple)
```bash
export JWT_SECRET_KEY="<secret>"
```

### Option 2: Docker Secrets
```yaml
# docker-compose.yml
services:
  api:
    secrets:
      - jwt_secret
secrets:
  jwt_secret:
    external: true
```

### Option 3: Azure Key Vault / AWS Secrets Manager
Use cloud provider SDK to retrieve secrets at startup.

## Monitoring

Monitor these security events:
- Failed authentication attempts (401 responses to /api/auth/login)
- Decryption errors (500 responses from password endpoints)
- Rate limit violations (429 responses)
- Token expiration patterns

## Rollback Procedure

If deployment fails:

1. Stop the application
2. Restore previous version from backup
3. Verify JWT_SECRET_KEY matches previous deployment
4. Restart application
5. Verify all endpoints respond correctly

## Support

For deployment issues, check:
- Application logs: `app.log`
- System logs: `journalctl -u passwordmanager-api`
- Environment variables: `printenv | grep JWT`
