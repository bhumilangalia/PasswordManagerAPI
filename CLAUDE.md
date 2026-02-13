# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ASP.NET Core Minimal API for a password manager application. Uses JWT authentication, SQLite database with Entity Framework Core, and ASP.NET Data Protection API for password encryption.

## Development Commands

### Running the Application
```bash
dotnet run
```
The API runs on `http://localhost:5103` by default.

### Building
```bash
dotnet build
```

### Code Quality and Linting
```bash
# Check formatting (without fixing)
dotnet format --verify-no-changes

# Auto-fix formatting issues
dotnet format

# Build with Roslyn analyzers (warnings will be shown)
dotnet build
```

**Automatic Formatting:**
- VS Code is configured to format on save via `.vscode/settings.json`
- Code style rules defined in `.editorconfig`
- Roslyn analyzers enabled in build via project settings

**Key Rules:**
- Private fields must start with underscore (`_fieldName`)
- All if/else statements must use braces
- Using directives outside namespace
- File-scoped namespace declarations preferred

### Database
The SQLite database (`passwordmanager.db`) is automatically created on startup via `db.Database.EnsureCreated()` in Program.cs:47-51.

### Testing

**Unit Tests:**
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~PasswordCipherServiceTests"

# Run with detailed output
dotnet test --verbosity detailed
```

**Test Project:** `PasswordManagerApi.Tests/`
- 21 unit tests for PasswordCipherService (encryption/decryption scenarios)
- Uses xUnit, FluentAssertions, and Microsoft.AspNetCore.Mvc.Testing
- In-memory database for integration tests

**API Testing:**
Use the `PasswordManagerApi.http` file with REST Client extension or similar tools. Contains pre-configured requests for all endpoints.

## Architecture

### Minimal API Structure
This project uses ASP.NET Core Minimal APIs (no controllers). All endpoints are defined in Program.cs using route groups:
- `/api/auth` group (lines 63-126): Registration and login
- `/api/passwords` group (lines 128-310): Password CRUD operations with authorization

### Data Models (Models/)
- **AppUser**: User account with hashed password. Username is normalized and indexed for uniqueness.
- **PasswordEntry**: Stores encrypted passwords with metadata (title, login username, website, notes). Each entry belongs to a user via `UserId` foreign key with cascade delete.

### Database (Data/)
**AppDbContext.cs** configures:
- Unique index on `AppUser.NormalizedUsername`
- One-to-many relationship: AppUser → PasswordEntries with cascade delete

### Services (Services/)

**IPasswordCipherService / PasswordCipherService**
- Encrypts/decrypts password entry passwords using ASP.NET Data Protection API
- Uses purpose string: `"PasswordManagerApi.PasswordCipher.v1"`
- Critical: Encrypted passwords are stored in DB; always decrypt when returning to client

**IJwtTokenService / JwtTokenService**
- Creates JWT tokens for authenticated users
- Token lifetime: 2 hours
- Includes claims: NameIdentifier (user ID), Name (username)

### DTOs (DTOs/)
Request/response objects:
- Auth: `RegisterRequest`, `LoginRequest`, `AuthResponse`
- Passwords: `CreatePasswordEntryRequest`, `UpdatePasswordEntryRequest`, `PasswordEntryResponse`

### Configuration (appsettings.json)

**ConnectionStrings:DefaultConnection**: SQLite database path
**Jwt:Key**: ⚠️ **REMOVED from appsettings.json for security** - must be provided via `JWT_SECRET_KEY` environment variable or appsettings.Development.json
**Jwt:Issuer/Audience**: JWT validation parameters

## Security Architecture

### User Authentication
1. Passwords hashed using `Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>`
2. JWT bearer tokens required for `/api/passwords/*` endpoints (via `.RequireAuthorization()`)
3. User identity extracted from `ClaimTypes.NameIdentifier` in token claims

### Password Entry Encryption
1. User's stored passwords encrypted via `IPasswordCipherService.Encrypt()` before saving to DB
2. Decrypted via `IPasswordCipherService.Decrypt()` when retrieved
3. Encryption uses ASP.NET Data Protection API (machine-key-based symmetric encryption)

### Authorization
All password endpoints verify:
1. User is authenticated (JWT token present and valid)
2. User owns the password entry being accessed (UserId matches claim)

## Key Implementation Patterns

### Endpoint Authorization
```csharp
var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
if (!int.TryParse(userIdClaim, out var userId)) { return Results.Unauthorized(); }
// Then verify entry.UserId == userId
```

### Password Storage Flow
```
User Password (plaintext) → Encrypt → EncryptedPassword (DB) → Decrypt → plaintext (response)
```

### Username Normalization
Usernames are normalized (trimmed, lowercased) and stored in `NormalizedUsername` field for case-insensitive lookups.

## Important Notes

- Database is created automatically on startup; no migrations needed for development
- JWT key is provided via environment variable (`JWT_SECRET_KEY`) or appsettings.Development.json for development
- ClockSkew set to TimeSpan.Zero for exact token expiration
- All timestamps use UTC
- Password entries cascade delete when user is deleted

## Security Configuration

### Environment Variables (Production)

**Required:**
- `JWT_SECRET_KEY`: 64+ character random string for JWT token signing
  - Generate: `openssl rand -base64 64` (Linux/macOS)
  - Generate: `[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))` (PowerShell)
- `ASPNETCORE_ENVIRONMENT`: Set to "Production" for production deployments

**Optional:**
- `ASPNETCORE_URLS`: Configure HTTPS binding (default: https://+:5001)

### Security Features

**Enforced in Production:**
1. HTTPS redirection (HTTP → HTTPS automatically)
2. HSTS headers (max-age=365 days, includeSubDomains, preload)
3. Security headers (X-Content-Type-Options, X-Frame-Options, CSP, etc.)
4. Rate limiting on auth endpoints (5 requests/minute)

**Always Active:**
1. JWT bearer token authentication
2. Password encryption at rest (ASP.NET Data Protection API)
3. Authorization checks (users can only access their own data)
4. Parameterized queries (Entity Framework LINQ)

### File Security (.gitignore)

**Never commit:**
- `*.db` (databases with user data)
- `*.log` (log files)
- `appsettings.Development.json`, `appsettings.Production.json` (environment-specific configs with secrets)
- JWT secret keys

### Deployment Checklist

See `DEPLOYMENT.md` and `SECURITY-CHECKLIST.md` for complete production deployment checklists and security verification procedures.
