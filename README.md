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

```text
API
+-- Controllers        # HTTP endpoints
+-- Middlewares        # Error handling, security headers, request correlation
+-- Hubs               # SignalR discussion hub

Application
+-- DTOs               # Request/response contracts
+-- Interfaces         # Service contracts
+-- Services           # Business logic
+-- Exceptions         # Application-level exceptions
+-- Mappings           # AutoMapper profile

Domain
+-- Entities           # EF Core entities
+-- Enums              # Workflow status and priority enums

Infrastructure
+-- Data               # AppDbContext and model configuration
+-- Repositories       # Generic repository abstraction
```

## Local Configuration

`appsettings.json` contains safe default placeholders. Keep machine-specific, non-secret settings in `appsettings.Local.json`, which is ignored by git.

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

Development secrets are loaded from .NET User Secrets. Environment variables and command-line arguments have higher precedence and should be used by deployed environments.

## Run Locally

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

Swagger:

```text
https://localhost:7231/swagger
```

Swagger includes JWT Bearer support, role notes for protected endpoints, API tags, XML comments when available, and default error responses for validation/auth/server failures.

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
  "message": "Human-readable error message",
  "code": "business_error",
  "traceId": "0HN...",
  "details": "",
  "errors": null
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
- HTTP integration flow covering login, project creation, task creation, evidence upload, progress submission, manager review, task approval, and KPI read.
- HTTP integration authorization test proving normal users cannot create projects or tasks.
- HTTP integration flow proving account deletion revokes the old JWT while locked KPI remains visible to the authorized historical manager.

See [testing guide](docs/testing.md).

## Continuous Integration

GitHub Actions runs the following release gate for every push and pull request:

- Restore the repository-local `dotnet-ef` tool and NuGet dependencies.
- Verify formatting.
- Build Release with warnings treated as errors.
- Run all backend tests and retain the TRX report.
- Fail when the EF Core model has changes without a migration.
- Publish the backend artifact.
- Validate Compose and build both runtime and migration container targets.
- Start a disposable SQL Server, apply the complete migration bundle from an empty database, and seed the demo dataset.
- Verify API readiness, JWT login, role authorization, the latest migration, and expected demo records before tearing the stack down.

## Documentation Map

- [Business rules](docs/business-rules.md): role permissions and workflow rules.
- [Database overview](docs/database.md): entities, relationships, constraints, and migration notes.
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
