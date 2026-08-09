# Getting Started

This guide covers a clean clone, local SQL Server setup, and the Docker Compose path.

## Prerequisites

Choose one runtime path:

- Local: Git, .NET 8 SDK, and SQL Server reachable from the host.
- Containers: Git and Docker Desktop with Compose v2.

The repository pins SDK `8.0.400` in `global.json` and permits a later .NET 8 feature band. Verify the selected SDK:

```powershell
dotnet --version
```

## Clean Clone

```powershell
git clone https://github.com/Giapnocap/work-management-system-backend.git
Set-Location .\work-management-system-backend
dotnet tool restore
dotnet restore .\WorkManagementSystem.sln
```

Do not commit `bin/`, `obj/`, `TestResults/`, `Uploads/`, `logs/`, `.env`, or `appsettings.Local.json`. They are local/runtime artifacts covered by `.gitignore`.

## Run With Local SQL Server

Create the ignored local configuration file:

```powershell
Copy-Item .\appsettings.Local.example.json .\appsettings.Local.json
```

Update `ConnectionStrings:Default` in `appsettings.Local.json` if the default SQL Server instance is not available. Then store the JWT key outside source control:

```powershell
dotnet user-secrets set "Jwt:Key" "replace-with-a-random-key-of-at-least-32-characters"
```

Restore the schema and run the API:

```powershell
dotnet ef database update --project .\WorkManagementSystem.csproj
dotnet run --launch-profile https
```

Development endpoints:

```text
Swagger:   https://localhost:7231/swagger
Liveness:  https://localhost:7231/health/live
Readiness: https://localhost:7231/health/ready
```

Swagger is intentionally enabled only in Development. If the development JWT key is omitted, startup uses an ephemeral key and tokens stop working after the process restarts.

## Optional Demo Dataset

Set the following in `appsettings.Local.json` before starting the API:

```json
{
  "DemoSeed": {
    "Enabled": true,
    "ApplyMigrations": false
  }
}
```

The demo seed is idempotent and creates approved Admin, Manager, and User accounts. It is disabled by default and must remain disabled in production.

## Run With Docker Compose

Create the ignored environment file:

```powershell
Copy-Item .\.env.example .\.env
```

Replace both placeholder secrets in `.env`. `MSSQL_SA_PASSWORD` must satisfy SQL Server password complexity and `JWT_KEY` must contain at least 32 characters. Optionally set `DEMO_SEED_ENABLED=true` for the documented sample workflow.

Validate and start the stack:

```powershell
docker compose config
docker compose up --build
```

Compose performs these steps in order:

1. Start SQL Server and wait for its health check.
2. Run the EF Core migration bundle once.
3. Start the non-root API container.
4. Persist SQL data, uploads, and logs in named volumes.

Container endpoints:

```text
API:       http://localhost:8080
Swagger:   http://localhost:8080/swagger
Liveness:  http://localhost:8080/health/live
Readiness: http://localhost:8080/health/ready
SQL:       localhost,14333
```

Useful checks:

```powershell
docker compose ps
docker compose logs migrate
docker compose logs api
Invoke-RestMethod http://localhost:8080/health/ready
```

Stop containers while preserving data:

```powershell
docker compose down
```

`docker compose down -v` also deletes the SQL, upload, and log volumes. Use it only when a complete local reset is intended.

## Verify A Clean Checkout

```powershell
dotnet format .\WorkManagementSystem.sln --verify-no-changes --no-restore
dotnet build .\WorkManagementSystem.sln --configuration Release --no-restore -warnaserror `
  -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet test .\WorkManagementSystem.sln --configuration Release --no-build `
  -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

SQL Server integration tests require `WMS_TEST_SQLSERVER_CONNECTION`; without it, only that category is skipped locally. CI supplies the variable and requires the relational tests to pass.

## Common Startup Problems

- SQL connection failure: verify the server name, authentication mode, certificate settings, and that the database service is running.
- `dotnet ef` not found: run `dotnet tool restore` from the repository root.
- HTTPS certificate warning: run `dotnet dev-certs https --trust` for local development.
- `401` after API restart: sign in again if Development used the ephemeral JWT key.
- Readiness returns `503`: inspect both SQL connectivity and write access to `Uploads/`; liveness can remain healthy during a dependency outage.
