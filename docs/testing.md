# Testing Guide

The backend has an xUnit test project:

```text
WorkManagementSystem.Tests/
```

Service tests use EF Core InMemory where relational behavior is not relevant. HTTP integration tests boot the real application entry point through `WebApplicationFactory<Program>` and replace only the database provider with an isolated InMemory database. SQL Server integration tests cover behavior that cannot be proven by InMemory or SQLite.

## Run Tests

```powershell
dotnet restore .\WorkManagementSystem.sln
dotnet test .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
```

Without `WMS_TEST_SQLSERVER_CONNECTION`, the SQL Server category is reported as skipped. To run it against a disposable or dedicated test instance:

```powershell
$env:WMS_TEST_SQLSERVER_CONNECTION = "Server=localhost,14333;Database=master;User Id=sa;Password=<test-password>;Encrypt=True;TrustServerCertificate=True"
dotnet test .\WorkManagementSystem.Tests\WorkManagementSystem.Tests.csproj --filter "Category=SqlServer"
Remove-Item Env:WMS_TEST_SQLSERVER_CONNECTION
```

The fixture creates a uniquely named database, applies every migration, runs the tests, and drops the database. Never point this variable at a production server account.

## CI Release Gate

The repository includes `.github/workflows/backend-ci.yml`. It audits direct and transitive NuGet dependencies, verifies formatting, builds with warnings as errors, runs unit/HTTP tests, runs the SQL Server category against the Compose database, checks migration drift, publishes an artifact, and validates the complete Compose stack. Audit retrieval failures and `NU1901`-`NU1904` vulnerability findings fail the restore gate.

The EF CLI version is locked in `.config/dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

### SQL Server Relational Tests

The `Category=SqlServer` suite runs against a uniquely named database and verifies:

1. All migrations apply from an empty database.
2. Duplicate usernames are rejected by the unique index.
3. Invalid memberships are rejected by foreign keys.
4. Invalid KPI date ranges are rejected by the check constraint.
5. Failed multi-step operations roll back persisted changes.
6. SQL Server `rowversion` rejects stale updates and prevents lost updates.
7. Workload aggregation translates on SQL Server and uses a constant query count for a large employee set.
8. Capacity history constraints reject non-positive values and duplicate open periods.
9. Two recurring-task workers racing for the same due occurrence create exactly one task.
10. Recurring schedules survive a worker/context restart without repeating an occurrence.
11. A failure after SQL commands execute rolls back the generated task, occurrence, and schedule update together.
12. Reminder-policy filtered unique indexes reject duplicate Unit scopes.
13. Two deadline workers racing for the same milestone create one scheduled event and one inbox notification.
14. Deadline events and inbox delivery survive worker/context restart without repeating the milestone.
15. Task timeline keyset pagination translates on SQL Server, does not duplicate equal-timestamp events, and keeps a constant query count across page sizes.
16. Two concurrent review requests persist exactly one decision, one approved-hours contribution, and one transition history set.
17. Progress-status history projection and filters translate on SQL Server.
18. Task list DTO enrichment stays within a nine-command budget for both 10-item and 100-item pages on a 300-task dataset.
19. Upgrading from the pre-KPI-insights migration preserves KPI snapshots and backfills the new explainability fields.

CI always supplies the SQL connection string, so skipped relational tests cannot make the pipeline falsely green.

### Runtime Container Smoke Test

CI performs the database and runtime checks that EF Core InMemory cannot cover:

1. Start a fresh SQL Server container.
2. Run the non-root EF migration bundle against an empty database.
3. Start the API only after migration completion.
4. Wait for `/health/ready` and require the Docker container health status to become `healthy`.
5. Login as Admin, Manager, and User with real JWT authentication.
6. Verify Admin can read KPI periods and Manager can read projects.
7. Verify User project creation returns `403` and anonymous project access returns `401`.
8. Verify SQL migration history reached the expected latest migration and demo seed records exist.
9. Backup with checksum, restore into a temporary database, compare critical records, and run `DBCC CHECKDB`.
10. Stop SQL Server and verify liveness remains `200` while readiness becomes `503`.
11. Remove the containers and disposable volumes even when a check fails.

This relational gate caught a historical migration that referenced task date columns missing from an empty database, a failure that model-drift checks and InMemory tests could not reproduce.

Detailed query budgets are recorded in [query performance](performance.md). Recovery steps and background-worker observability are recorded in [recovery and workers](recovery-and-workers.md).

## Current Test Coverage

Run the full suite to obtain the current test count. The count is intentionally not duplicated in documentation because it changes whenever a regression case is added.

### Auth

- Registration creates a pending account.
- Password policy is shared by registration, reset, and change flows.
- Password is hashed with the configured BCrypt work factor, and older hashes are upgraded after login.
- Duplicate username is blocked.
- Pending users cannot login.
- Approved users receive a JWT token.
- Changing `TokenVersion` invalidates a previously issued JWT.
- SignalR task groups reject authenticated users who cannot access the task.

### Task Service

- Non-manager cannot create task.
- Manager without department cannot create task.
- Manager cannot assign task to staff outside their department.
- Task without direct assignee is assigned to the manager's department.

### Workload And Capacity

- An employee with no active task has zero workload.
- Only active tasks overlapping the selected range contribute remaining effort.
- Approved tasks are excluded and multi-assignee effort is split evenly.
- Busy/Overloaded threshold boundaries are deterministic and configurable.
- Capacity changes inside a date range are prorated from effective-dated history.
- Assignment preview returns projected workload and does not block task creation.
- Manager cross-department workload and capacity access is forbidden.
- SQL Server verifies workload aggregation translation and guards against N+1 queries.

### Recurring Tasks

- Daily and weekly intervals preserve the scheduled UTC time.
- Monthly schedules clamp day 29-31 to the last valid day without losing the preferred day in later months.
- CRUD, department scope, explicit default assignees, pause/resume, and API authorization are covered.
- A rerun and a simulated worker restart do not generate duplicate occurrences.
- Catch-up is bounded per batch and leaves remaining overdue occurrences persisted for the next run.
- A failed generation leaves no task, history, occurrence, or advanced schedule.
- SQL Server verifies query translation, atomic rollback, persisted restart behavior, and two-worker race safety.

### Deadline Reminder And Escalation

- A task due in 24 hours produces one due-soon notification and does not repeat on the next scan.
- Overdue work notifies the employee; crossing the escalation threshold notifies only Managers in the task department.
- A task completed immediately before delivery suppresses the pending event.
- Policy changes made before a future milestone are used by the next scan, including Project-over-Unit-over-Global precedence.
- A transient inbox failure persists bounded retry state and succeeds on a later batch without duplicating the event.
- In-memory restart tests verify persisted state, while SQL Server tests verify migration seed data, filtered uniqueness, query translation, restart behavior, and racing workers.

### Progress And Review

- Completing a review-required task without evidence is blocked.
- Partial progress updates status to `InProgress`.
- Completing a non-review task approves progress and completes task.
- A multi-assignee task completes only after every assigned staff member has approved completion.
- Manager approval completes a submitted task.
- Rejected completion remains a rejected progress report and returns the task to `InProgress`.
- A corrected completion can be resubmitted and approved after rejection.
- Rejection without a reason fails without mutating task, progress, review, or history state.
- A manager from another department cannot review the report.
- Already reviewed progress cannot be reviewed again.
- The explicit policy matrix accepts supported transitions and rejects invalid actor, scope, dependency, and terminal-state combinations.
- Task and progress transitions record related report ids and decision reasons.

### Task Activity Timeline

- Main task lifecycle sources are combined in deterministic descending order.
- Equal-timestamp events paginate without duplicates or missing ids.
- Type, actor, and UTC date filters are enforced.
- Managers from another department cannot read the timeline.
- Soft-deleted actors resolve to a historical snapshot or safe fallback.
- Non-allowlisted task history fields do not leak through metadata.
- Progress status changes expose only bounded reason/status metadata and the related progress id.
- SQL Server verifies cursor translation and constant query count as page size grows.

### Upload

- Invalid or dangerous file types are blocked.
- A ZIP file renamed to `.docx` is rejected unless it has the expected OOXML structure.
- OOXML files containing VBA macro payloads are rejected.
- Original file names are sanitized before metadata persistence.
- File metadata is saved only after the physical file is accepted.
- A failed database save cleans up the physical file.
- Rooted or traversal storage keys cannot be downloaded.
- Aged orphan files are reconciled against persisted storage keys while recent files are preserved.
- Download uses authorization-aware metadata and does not expose server file paths in public DTOs.

### KPI

- KPI reads do not create a missing period.
- KPI periods are created explicitly by Admin.
- Invalid date ranges are blocked.
- Overlapping KPI periods are blocked.
- Staff unit/role movement is handled through work history during KPI calculation.
- Locked KPI stores employee and department identity snapshots.
- A deleted historical employee remains part of a KPI period that overlaps their employment history.
- Locked KPI raw metrics and formula version remain stable after source task or identity changes.
- KPI rates handle zero denominators deterministically.
- Manager dashboard scope is limited to the current department; Admin dashboard scope is organization-wide.
- SQL Server tests verify KPI aggregation translates and keeps a constant query count for a larger department.
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

### HTTP Response Contract

- Validation, authentication, authorization, not-found, conflict, rate-limit, and server failures use the same `application/problem+json` shape.
- Resource creation returns `201 Created`.
- Deletion and commands without response data return `204 No Content`.
- Task and progress pagination use typed `PagedResult<T>` contracts while preserving the JSON fields `total`, `page`, `size`, and `data`.
- Task history endpoints expose DTOs rather than persistence entities.

### Pagination

- Invalid page and size values fall back to safe defaults.
- Large page sizes are capped at the shared maximum.
- History endpoints can use a larger default page size without bypassing the maximum cap.

### Operational Middleware And Cancellation

- A safe client correlation ID is reused in the request trace and response header.
- An unsafe correlation ID is rejected in favor of the server trace identifier.
- Authenticated request logs carry a structured `UserId` property.
- Liveness is isolated from database/upload readiness, while readiness verifies both dependencies.
- Client-aborted requests are not converted into false HTTP 500 responses.
- Cancellation reaches Auth database queries.
- Batched task DTO mapping keeps assignees, uploads, and subtasks isolated by task.

### HTTP Integration Workflow

- `WebApplicationFactory<Program>` boots the same ASP.NET Core middleware, authentication, authorization, routing, and DI pipeline used by the application.
- Only `AppDbContext` is replaced with an isolated InMemory provider for fast HTTP workflow tests.
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
