# Security Deployment Checklist

**Project:** PasswordManager API
**Last Updated:** 2026-02-13

---

## 1. Secrets Management

- [ ] **JWT Secret Key**
  - [ ] 64+ character cryptographically random string generated
  - [ ] Stored in environment variable `JWT_SECRET_KEY`
  - [ ] NOT in any appsettings.json file
  - [ ] NOT in source control

- [ ] **appsettings.json Files**
  - [ ] Base `appsettings.json` contains NO secrets
  - [ ] `appsettings.Development.json` in .gitignore
  - [ ] `appsettings.Production.json` in .gitignore
  - [ ] Production secrets managed via environment variables or secrets manager

## 2. Version Control Security

- [ ] **.gitignore Configured**
  - [ ] `*.db` (database files)
  - [ ] `*.log` (log files)
  - [ ] `appsettings.*.json` (environment configs)
  - [ ] `bin/`, `obj/` (build artifacts)

- [ ] **Git History Clean**
  - [ ] Verified no secrets in commit history: `git log --all -S "ThisIsADevOnly"`
  - [ ] If found, history rewritten or new keys generated

## 3. HTTPS Configuration

- [ ] **SSL Certificate**
  - [ ] Valid SSL certificate installed on server/reverse proxy
  - [ ] Certificate not expired
  - [ ] Certificate covers deployment domain

- [ ] **HTTPS Enforcement**
  - [ ] `ASPNETCORE_ENVIRONMENT=Production` set
  - [ ] HTTP requests redirect to HTTPS (verified)
  - [ ] HSTS header present (verified with curl -I)
  - [ ] HSTS max-age >= 31536000 (1 year)

## 4. Application Configuration

- [ ] **Environment**
  - [ ] `ASPNETCORE_ENVIRONMENT=Production`
  - [ ] Development features disabled (OpenAPI/Swagger)

- [ ] **Database**
  - [ ] Production database path configured
  - [ ] Database file permissions: app user only (chmod 600)
  - [ ] Database directory writable by app user

- [ ] **Logging**
  - [ ] Log level set to "Warning" or "Error" in production
  - [ ] Log rotation configured
  - [ ] Logs don't contain passwords (verified via tests)

## 5. Security Features Enabled

- [ ] **Authentication & Authorization**
  - [ ] JWT authentication active on /api/passwords/* endpoints
  - [ ] Test: Request without token returns 401
  - [ ] Test: Request with expired token returns 401
  - [ ] Test: User can only access own passwords

- [ ] **Rate Limiting**
  - [ ] Rate limiter active on /api/auth/* endpoints
  - [ ] Test: 6 rapid requests returns 429 on 6th

- [ ] **Security Headers**
  - [ ] X-Content-Type-Options: nosniff
  - [ ] X-Frame-Options: DENY
  - [ ] Content-Security-Policy present
  - [ ] Server header removed

## 6. Testing

- [ ] **Security Tests Pass**
  ```bash
  dotnet test --filter Category=Security
  ```
  - [ ] All DecryptionSecurityTests pass
  - [ ] All AuthorizationTests pass

- [ ] **Manual Security Verification**
  - [ ] Test login with wrong password (returns 401)
  - [ ] Test accessing another user's password (returns 404)
  - [ ] Test with malformed JWT token (returns 401)
  - [ ] Verify passwords encrypted in database (open .db file, check EncryptedPassword column)

## 7. Monitoring & Incident Response

- [ ] **Logging Configured**
  - [ ] Failed auth attempts logged
  - [ ] Decryption errors logged
  - [ ] No sensitive data in logs

- [ ] **Incident Response Plan**
  - [ ] Contact for security issues documented
  - [ ] JWT key rotation procedure documented
  - [ ] Database backup/restore procedure tested

## 8. Dependency Security

- [ ] **NuGet Packages**
  ```bash
  dotnet list package --vulnerable
  ```
  - [ ] No vulnerable packages found
  - [ ] All packages up to date

## 9. Final Verification

- [ ] **Deployment Test**
  - [ ] Register new user
  - [ ] Login (receive JWT token)
  - [ ] Create password entry
  - [ ] Retrieve password entry (password decrypted correctly)
  - [ ] Update password entry
  - [ ] Delete password entry
  - [ ] All operations over HTTPS only

---

## Security Vulnerabilities Fixed

✓ **CRITICAL:** Hardcoded JWT key removed from appsettings.json
✓ **CRITICAL:** .gitignore created to protect sensitive files
✓ **HIGH:** HTTPS enforcement with HSTS headers
✓ **MEDIUM:** Rate limiting on authentication endpoints (5/min)
✓ **MEDIUM:** Security headers middleware implemented

---

**Sign-off:**

- Security review completed by: ___________________
- Date: ___________________
- Deployment approved by: ___________________
- Date: ___________________
