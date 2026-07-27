# Production Readiness Checklist

Use this checklist before deploying the backend outside a local development environment.

## Configuration

- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Keep development secrets in .NET User Secrets.
- Keep production secrets in environment variables or a deployment secret store.
- Use `appsettings.Local.json` only for non-secret machine-specific overrides.
- Do not commit `appsettings.Local.json`.
- Replace `Jwt:Key` with a strong secret value outside source control.
- Set `ConnectionStrings:Default` for the target SQL Server database.
- Set `Cors:AllowedOrigins` to the real frontend URL(s).
- Restrict `AllowedHosts`; production startup rejects `*`.
- Use only HTTPS origins in production CORS configuration.
- Use a certificate-validated encrypted SQL Server connection. Production startup rejects `Encrypt=False` and `TrustServerCertificate=True`.
- Keep `DemoSeed:Enabled=false` in production.

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
- Add a dedicated antivirus or sandbox scanner before accepting files in a real internet-facing deployment. Built-in format and OOXML checks are defense in depth, not malware detection.

## Observability

- Preserve `X-Correlation-ID` through the reverse proxy and include it in incident reports.
- Collect structured console logs in the deployment platform.
- Retain rolling file logs only when the mounted storage and cleanup policy are intentional.
- Monitor `/health/live` for process liveness and `/health/ready` for database readiness.
- Treat client-request cancellation separately from server failures when reviewing error rates.

## Containers

- `compose.yml` is for local development/demo and is not a production deployment manifest.
- The `runtime` Docker target runs as the non-root `app` user.
- The `migrations` Docker target contains a framework-dependent EF migration bundle, runs as non-root, performs `database update`, and exits without shipping the SDK or source tree.
- Supply `MSSQL_SA_PASSWORD` and `JWT_KEY` through `.env` only for local Compose; use a deployment secret store in production.
- Terminate public HTTPS at a trusted reverse proxy or platform ingress and forward `X-Forwarded-For` and `X-Forwarded-Proto`.

## Continuous Integration

- Require the `Backend CI` workflow to pass before merging.
- Keep `dotnet-ef` aligned with the EF Core package version.
- Do not merge model changes when `has-pending-model-changes` fails.
- Require the disposable SQL Server migration and JWT authorization smoke test to pass.
- Review the published artifact and container smoke result for the commit being deployed.

## Verification

```powershell
dotnet build .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet test .\WorkManagementSystem.sln --no-build -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
dotnet publish .\WorkManagementSystem.csproj --configuration Release --no-build --output .\artifacts\publish -p:UseAppHost=false
```

Expected result: build succeeds and all automated tests pass.
