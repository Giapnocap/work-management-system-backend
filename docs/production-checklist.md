# Production Readiness Checklist

Use this checklist before deploying the backend outside a local development environment.

## Configuration

- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Keep development secrets in .NET User Secrets.
- Keep production secrets in environment variables or a deployment secret store.
- Use `appsettings.Local.json` only for non-secret machine-specific Development overrides; the application does not load it in Production.
- Do not commit `appsettings.Local.json`.
- Supply `Jwt:Key` as a strong secret outside source control; production startup fails when it is missing.
- Keep `Jwt:ExpirationMinutes` between 5 and 60 in production. The backend does not implement refresh tokens.
- Passwords must contain at least eight characters, an uppercase letter, a lowercase letter, and a digit, and must not exceed the BCrypt 72-byte UTF-8 boundary.
- Set `ConnectionStrings:Default` for the target SQL Server database.
- Set `Cors:AllowedOrigins` to the real frontend URL(s).
- Restrict `AllowedHosts`; production startup rejects `*`.
- Use only HTTPS origins in production CORS configuration.
- Use a certificate-validated encrypted SQL Server connection. Production startup rejects `Encrypt=False` and `TrustServerCertificate=True`.
- Keep `DemoSeed:Enabled=false` in production.
- Set `ReverseProxy:Enabled=true` only behind a reverse proxy, and configure at least one exact `KnownProxies` address or `KnownNetworks` CIDR. Production rejects an enabled but untrusted proxy configuration.

## Database

- Apply EF Core migrations before running the application.
- Review destructive cleanup migrations before applying them to a database with real data.
- Keep schema changes in migrations, not runtime startup code.
- Run the migration container or `dotnet ef database update` as a one-shot deployment step before starting the new API version.
- Back up the database before applying a migration to an existing environment.

## Runtime Files

- Keep `Uploads/` and `logs/` out of git.
- Make sure the deployed app has write permission to its upload and log directories.
- Back up upload files separately if they matter to business history.
- Mount persistent storage for both `Uploads/` and `logs/` when using containers.
- Keep the upload volume private; expose files only through the authorized download endpoint.
- Persist only relative upload `StorageKey` values. Keep `UploadCleanup` enabled unless another durable storage reconciliation process replaces it.
- Tune `UploadCleanup:MinimumAgeHours` and `UploadCleanup:IntervalHours` conservatively; the built-in scan deletes only aged files absent from persisted upload metadata.
- Add a dedicated antivirus or sandbox scanner before accepting files in a real internet-facing deployment. Built-in format and OOXML checks are defense in depth, not malware detection.

## Observability

- Preserve `X-Correlation-ID` through the reverse proxy and include it in incident reports.
- Collect structured console logs in the deployment platform.
- Configure log levels through `Serilog:MinimumLevel`; keep framework noise at `Warning` or above unless diagnosing an incident.
- Treat `CorrelationId` and authenticated `UserId` as searchable structured properties. Do not add passwords, access tokens, or upload contents to log scopes.
- File logs retain 14 daily files by default; align platform retention with incident and privacy requirements.
- Monitor `/health/live` for process liveness. `/health/ready` verifies both database connectivity and write access to private upload storage.
- Keep liveness independent from database and storage outages so the platform does not restart a healthy process during a dependency incident.
- Treat client-request cancellation separately from server failures when reviewing error rates.

## Containers

- `compose.yml` is for local development/demo and is not a production deployment manifest.
- The `runtime` Docker target runs as the non-root `app` user.
- The runtime image exposes a Docker health check backed by `/health/ready`.
- The `migrations` Docker target contains a framework-dependent EF migration bundle, runs as non-root, performs `database update`, and exits without shipping the SDK or source tree.
- Supply `MSSQL_SA_PASSWORD` and `JWT_KEY` through `.env` only for local Compose; use a deployment secret store in production.
- Terminate public HTTPS at a trusted reverse proxy or platform ingress and forward `X-Forwarded-For` and `X-Forwarded-Proto`; list that proxy explicitly in `ReverseProxy` configuration.

## Continuous Integration

- Require the `Backend CI` workflow to pass before merging.
- Keep full transitive NuGet audit enabled and treat `NU1900`-`NU1904` as release-blocking restore errors.
- Keep `dotnet-ef` aligned with the EF Core package version.
- Do not merge model changes when `has-pending-model-changes` fails.
- Require the SQL Server relational suite, disposable migration, container health check, and JWT authorization smoke test to pass.
- Review the published artifact and container smoke result for the commit being deployed.

## Verification

```powershell
dotnet build .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet test .\WorkManagementSystem.sln --no-build -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
dotnet publish .\WorkManagementSystem.csproj --configuration Release --no-build --output .\artifacts\publish -p:UseAppHost=false
```

Expected result: build succeeds and all automated tests pass.
