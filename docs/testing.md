# Testing Guide

The backend has an xUnit test project:

```text
WorkManagementSystem.Tests/
```

Most service and HTTP integration tests use EF Core InMemory so local test runs do not touch the real SQL Server database. CI also runs a separate relational smoke test against a disposable SQL Server container.

## Run Tests

```powershell
dotnet restore .\WorkManagementSystem.sln
dotnet test .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
```

## CI Release Gate

The repository includes `.github/workflows/backend-ci.yml`. It verifies formatting, builds with warnings as errors, runs the full test suite, checks migration drift, publishes an artifact, and validates the complete Compose stack.

The EF CLI version is locked in `.config/dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

### Relational Container Smoke Test

CI performs the database and runtime checks that EF Core InMemory cannot cover:

1. Start a fresh SQL Server container.
2. Run the non-root EF migration bundle against an empty database.
3. Start the API only after migration completion.
4. Wait for `/health/ready`.
5. Login as Admin, Manager, and User with real JWT authentication.
6. Verify Admin can read KPI periods and Manager can read projects.
7. Verify User project creation returns `403` and anonymous project access returns `401`.
8. Verify SQL migration history reached the expected latest migration and demo seed records exist.
9. Remove the containers and disposable volumes even when a check fails.

This relational gate caught a historical migration that referenced task date columns missing from an empty database, a failure that model-drift checks and InMemory tests could not reproduce.

## Current Test Coverage

Run the full suite to obtain the current test count. The count is intentionally not duplicated in documentation because it changes whenever a regression case is added.

### Auth

- Registration creates a pending account.
- Password is hashed.
- Duplicate username is blocked.
- Pending users cannot login.
- Approved users receive a JWT token.

### Task Service

- Non-manager cannot create task.
- Manager without department cannot create task.
- Manager cannot assign task to staff outside their department.
- Task without direct assignee is assigned to the manager's department.

### Progress And Review

- Completing a review-required task without evidence is blocked.
- Partial progress updates status to `InProgress`.
- Completing a non-review task approves progress and completes task.
- A multi-assignee task completes only after every assigned staff member has approved completion.
- Manager approval completes a submitted task.
- Rejected completion remains a rejected progress report and returns the task to `InProgress`.
- A corrected completion can be resubmitted and approved after rejection.
- A manager from another department cannot review the report.
- Already reviewed progress cannot be reviewed again.

### Upload

- Invalid or dangerous file types are blocked.
- A ZIP file renamed to `.docx` is rejected unless it has the expected OOXML structure.
- OOXML files containing VBA macro payloads are rejected.
- Original file names are sanitized before metadata persistence.
- File metadata is saved only after the physical file is accepted.
- A failed database save cleans up the physical file.
- Persisted paths outside the upload root cannot be downloaded.
- Download uses authorization-aware metadata and does not expose server file paths in public DTOs.

### KPI

- KPI reads do not create a missing period.
- KPI periods are created explicitly by Admin.
- Invalid date ranges are blocked.
- Overlapping KPI periods are blocked.
- Staff unit/role movement is handled through work history during KPI calculation.
- Locked KPI stores employee and department identity snapshots.
- A deleted historical employee remains part of a KPI period that overlaps their employment history.
- Historical manager access follows the selected period's snapshot/history unit rather than the employee's current unit.
- No-task users receive a neutral new/starter score.
- On-time approved work receives bonus points.
- Multiple overdue tasks trigger risk warning behavior.

### Database Model

- Task assignee rows must target exactly one side: user or department.
- KPI periods must have a valid date range.
- KPI result scores and counters must be non-negative.
- KPI result effective date ranges must be valid.
- Critical unique indexes are configured for users, departments, assignments, projects, KPI periods, and KPI results.
- Critical business relationships avoid accidental cascade deletes.

### Demo Seed

- Demo seed is disabled by default.
- When enabled, it creates admin/manager/user demo accounts, a department, project, tasks, progress records, memberships, and work histories.
- Running the seeder multiple times does not duplicate the demo dataset.

### DTO Validation

- Empty GUID values are rejected for required entity references.
- Required text fields are validated before reaching service logic.
- Validation failures use the standard API error contract.

### API Authorization Contract

- Controllers are API controllers with explicit routes.
- Public endpoints are limited to registration, login, and public unit lookup.
- Manager workflow endpoints require the `Manager` role.
- Admin workflow endpoints require the `Admin` role.
- Removed project board endpoints stay removed from the public API surface.

### Pagination

- Invalid page and size values fall back to safe defaults.
- Large page sizes are capped at the shared maximum.
- History endpoints can use a larger default page size without bypassing the maximum cap.

### Operational Middleware And Cancellation

- A safe client correlation ID is reused in the request trace and response header.
- An unsafe correlation ID is rejected in favor of the server trace identifier.
- Client-aborted requests are not converted into false HTTP 500 responses.
- Cancellation reaches Auth database queries.
- Batched task DTO mapping keeps assignees, uploads, and subtasks isolated by task.

### HTTP Integration Workflow

- Test server boots an in-memory ASP.NET Core API over HTTP.
- Login uses real JWT authentication.
- Manager creates a project.
- Manager creates a task linked to that project.
- User uploads evidence.
- User submits 100 percent progress.
- Manager approves the report.
- Task becomes `Approved`.
- Project status counts are updated.
- KPI/performance endpoint can read the completed work context.
- A normal User is forbidden from creating projects or tasks.
- Deleting an employee revokes their existing JWT.
- After deletion and period locking, the authorized historical Manager can still read the immutable KPI snapshot through HTTP.

## Regression Risks Covered

The suite protects the following behavior from regressions:

- Permission boundaries.
- Department isolation.
- Task state transitions.
- Review state transitions.
- Upload safety.
- KPI period integrity.
- KPI explainability when staff data changes over time.
- Database constraints that prevent invalid persisted state.
- Seed-data idempotency so demos can be reset and repeated safely.
- API request contracts that reject bad input early.
- Endpoint authorization contracts that prevent accidental permission regressions.
- Pagination guards that prevent accidental large list queries.
- Request correlation and cancellation behavior that keeps production logs actionable.
- HTTP integration tests that prove the main workflow works through controllers, middleware, authentication, DI, services, and EF Core context together.
