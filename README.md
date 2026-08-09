# WorkManagementSystem Backend

[![Backend CI](https://github.com/Giapnocap/work-management-system-backend/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/Giapnocap/work-management-system-backend/actions/workflows/backend-ci.yml)

ASP.NET Core 8 backend for a department-based work management system.

The backend owns authentication, authorization, department-scoped task workflow, project grouping, progress review, evidence uploads, KPI periods, staff work history, notifications, comments, exports, database integrity, and automated tests.

## Backend Scope

- Layered ASP.NET Core Web API architecture with controllers, application services, domain entities, EF Core infrastructure, and global exception handling.
- JWT authentication and role-based authorization for Admin, Manager, and User workflows.
- Department-scoped permissions so managers can only create projects, assign tasks, and review work inside their own department.
- Task lifecycle covering assignment, progress reports, evidence uploads, manager review, completion, history, and KPI calculation.
- Data integrity through EF Core configuration, unique indexes, check constraints, soft delete, and migrations.
- Upload hardening with MIME/signature checks, OOXML package validation, macro rejection, safe filenames, and download-root confinement.
- Request correlation IDs, structured Serilog request context, health checks, and cancellation propagation for operational troubleshooting.
- Automated tests for permission boundaries, workflow transitions, upload safety, KPI period logic, database constraints, DTO validation, API authorization contracts, pagination guards, and the main HTTP workflow.

## Architecture

This repository is a **layered modular monolith**: one ASP.NET Core runtime project and one xUnit test project. `API`, `Application`, `Domain`, and `Infrastructure` are logical folder and namespace boundaries inside the runtime assembly, not separately deployed services.

```text
Client -> API -> Application -> Domain
                    ^             ^
                    |             |
                Infrastructure ---+

Program.cs is the composition root for every layer.
```

Architecture tests prevent Application from referencing API/Infrastructure and prevent controllers from using data-access types directly. The application layer still uses EF Core query abstractions through `IAppDbContext`, so the project does not claim complete persistence ignorance or full Clean Architecture.

See the [architecture and dependency guide](docs/architecture.md).

## Prerequisites

- .NET 8 SDK (`global.json` pins `8.0.400` and allows a later .NET 8 feature band).
- SQL Server for local execution, or Docker Desktop with Compose v2.
- The repository-local EF Core CLI restored with `dotnet tool restore`.

## Local Configuration

`appsettings.json` contains non-secret defaults and does not contain a JWT signing key. Keep machine-specific, non-secret settings in `appsettings.Local.json`, which is ignored by git.

Create it from the example file:

```powershell
Copy-Item .\appsettings.Local.example.json .\appsettings.Local.json
dotnet user-secrets set "Jwt:Key" "<a-strong-random-secret-of-at-least-32-characters>"
```

Example `appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=WorkManagementDB;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
  },
  "DemoSeed": {
    "Enabled": false,
    "ApplyMigrations": false
  }
}
```

Development secrets are loaded from .NET User Secrets. If `Jwt:Key` is omitted in Development, the API creates an ephemeral key and existing tokens become invalid after restart. Environment variables and command-line arguments have higher precedence and should be used by deployed environments.

## Run Locally

```powershell
dotnet tool restore
dotnet restore .\WorkManagementSystem.sln
dotnet ef database update --project .\WorkManagementSystem.csproj
dotnet run --launch-profile https
```

Swagger:

```text
https://localhost:7231/swagger
```

Swagger includes JWT Bearer support, role notes for protected endpoints, API tags, XML comments when available, and default error responses for validation/auth/server failures.

For a new checkout, configuration precedence, verification commands, and troubleshooting, use the [clean-clone and local setup guide](docs/getting-started.md).

## Run With Docker

The Compose stack is intended for local development and integration testing. It starts SQL Server, applies EF Core migrations once, and then starts the API.

```powershell
Copy-Item .env.example .env
```

Replace `MSSQL_SA_PASSWORD` and `JWT_KEY` in `.env`, then run:

```powershell
docker compose up --build
```

Local endpoints:

```text
API:     http://localhost:8080
Swagger: http://localhost:8080/swagger
SQL:     localhost,14333
```

Stop the stack without deleting persisted data:

```powershell
docker compose down
```

`compose.yml` deliberately uses the `Development` environment because the local SQL Server container uses a self-signed certificate. Production must use the stricter settings in [production checklist](docs/production-checklist.md).

## Optional Demo Seed

Demo seed data is disabled by default. To create a repeatable local test dataset, set this in `appsettings.Local.json`:

```json
{
  "DemoSeed": {
    "Enabled": true,
    "ApplyMigrations": false
  }
}
```

Seeded accounts use password:

```text
Demo@123456
```

Seeded usernames:

- `demo.admin`
- `demo.manager`
- `demo.employee1`
- `demo.employee2`

The seeder is idempotent: running the app multiple times with demo seed enabled does not duplicate the demo users, project, tasks, or memberships.

## Important Business Rules

- Only `Manager` can create projects and tasks.
- A manager can create and assign tasks only inside their own department.
- Admin manages users/departments but does not participate in task assignment.
- A task can be assigned to selected users, or to the current staff snapshot of the manager's department.
- A progress report can complete a task directly only when review is not required.
- If review is required, 100 percent progress must include evidence file metadata.
- A submitted report can be reviewed only once.
- KPI periods can be locked to preserve calculated results.
- User role/unit movement is tracked through work history so old KPI periods remain explainable.
- Account deletion revokes active sessions and current membership while preserving completed task assignments, progress, and work history.
- Locked KPI results store employee and department identity snapshots, so later profile edits or account deletion cannot rewrite historical output.
- Managers can read historical KPI only when the selected period's snapshot or work history belongs to their current department.
- Personal KPI starts from 100, adds weighted bonuses for approved/on-time work, and subtracts penalties for overdue work or rejected reports.
- Manager KPI combines department average performance and the manager's own task performance.

Main workflow:

```text
Admin setup -> Manager creates project -> Manager creates task
-> User uploads evidence -> User submits progress
-> Manager reviews -> Task is approved -> KPI is updated/read
```

See [business rules](docs/business-rules.md).

An executable PowerShell walkthrough is available in [sample API workflow](docs/api-workflow.md).

## Database

The data model is configured in `Infrastructure/Data/AppDbContext.cs`.

Key integrity controls:

- Unique indexes for username, employee code, department name, project per department, task assignment, review per progress report, and KPI result per user/period.
- Check constraints for progress percent and non-negative hours.
- Check constraints for KPI period date ranges, KPI result effective ranges, and non-negative KPI metrics.
- Soft-delete query filters for active operational data, while task assignments, progress, staff history, and locked KPI snapshots remain available for authorized historical reads.
- Migrations are the only supported way to change schema.

See [database overview](docs/database.md).

## Error Handling

The backend has a global exception middleware and explicit application exceptions:

- `BusinessException`
- `NotFoundException`
- `ForbiddenException`
- `InvalidCredentialsException`

Response shape:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Human-readable error message",
  "status": 400,
  "detail": "",
  "instance": "/api/resource",
  "code": "business_error",
  "message": "Human-readable error message",
  "traceId": "0HN...",
  "errors": {}
}
```

See [API error contract](docs/api-errors.md).

## Tests

The test project is located at:

```text
WorkManagementSystem.Tests/
```

Run:

```powershell
dotnet test .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
```

The suite grows with each regression case; use the command above for the current verified count.

Current coverage focuses on core backend rules:

- Auth account lifecycle.
- Task creation permission checks.
- Department assignment boundary checks.
- Progress and review state transitions.
- Upload validation and cleanup behavior.
- KPI period validation, full-day date boundaries, locked snapshots, and date-only deadline handling.
- KPI scoring branches for no-task, on-time, overdue, bonus cap, and staff movement scenarios.
- Staff movement handling for KPI calculation.
- Account-deactivation regression tests for assignment preservation, work-history closure, token revocation, and historical KPI authorization.
- Database model integrity constraints.
- Demo data seeding idempotency.
- DTO and API contract validation.
- Controller route and role authorization contracts.
- Swagger/OpenAPI documentation for JWT, roles, and common error responses.
- Shared pagination normalization with a maximum page size guard.
- HTTP integration tests booting the real `Program.cs` pipeline through `WebApplicationFactory<Program>`.
- HTTP integration flow covering login, project creation, task creation, evidence upload, progress submission, manager review, task approval, and KPI read.
- HTTP integration authorization test proving normal users cannot create projects or tasks.
- HTTP integration flow proving account deletion revokes the old JWT while locked KPI remains visible to the authorized historical manager.
- SQL Server integration tests for migrations from zero, unique/foreign-key/check constraints, transaction rollback, and optimistic concurrency.

See [testing guide](docs/testing.md).

## Known Limitations

- The logical layers compile into one deployable assembly; they are not independently versioned class libraries or microservices.
- Authentication uses short-lived JWT access tokens plus database-backed `TokenVersion` revocation. There is no refresh-token flow.
- SQL Server is the only supported relational provider.
- Uploads use a private local/container volume. There is no object-storage adapter or antivirus engine; built-in validation is defense in depth only.
- SignalR notifications are in-process and best effort. There is no distributed backplane, message broker, or transactional outbox.
- API routes are not versioned yet.
- KPI rules are project-specific policy and require validation against a real organization's HR policy before production use.
- The repository provides CI, container builds, and deployment checks, but no production CD workflow or cloud infrastructure definition.

## Continuous Integration

GitHub Actions runs the following release gate for every push and pull request:

- Restore the repository-local `dotnet-ef` tool and audit all direct/transitive NuGet dependencies, failing when vulnerability data is unavailable or an advisory is found.
- Verify formatting.
- Build Release with warnings treated as errors.
- Run unit and HTTP integration tests and retain the TRX report.
- Start SQL Server and require the relational integration suite to pass.
- Fail when the EF Core model has changes without a migration.
- Publish the backend artifact.
- Validate Compose and build both runtime and migration container targets.
- Start a disposable SQL Server, apply the complete migration bundle from an empty database, and seed the demo dataset.
- Verify API/database/upload readiness, Docker health, JWT login, role authorization, the latest migration, and expected demo records before tearing the stack down.

## Documentation Map

- [Architecture](docs/architecture.md): actual dependency direction, request flow, and runtime topology.
- [Getting started](docs/getting-started.md): clean clone, local SQL Server, Docker Compose, and verification.
- [Business rules](docs/business-rules.md): role permissions and workflow rules.
- [Database overview](docs/database.md): entities, relationships, constraints, and migration notes.
- [Sample API workflow](docs/api-workflow.md): repeatable project-to-task-to-review walkthrough.
- [API error contract](docs/api-errors.md): standard error response shape.
- [Testing guide](docs/testing.md): test categories and commands.
- [Production checklist](docs/production-checklist.md): deployment configuration and runtime checks.

## Useful Commands

Add a migration:

```powershell
dotnet ef migrations add MigrationName
```

Apply migrations:

```powershell
dotnet ef database update
```

Build:

```powershell
dotnet build .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
```

Run only backend workflow integration tests:

```powershell
dotnet test .\WorkManagementSystem.Tests\WorkManagementSystem.Tests.csproj --no-build --filter BackendWorkflowIntegrationTests -p:UseAppHost=false -p:UseSharedCompilation=false
```
