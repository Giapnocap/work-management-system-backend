# Portfolio Demo Guide

This guide presents the existing backend in 10 to 15 minutes. It does not require direct database edits, prepared identifiers, or manual JWT copying.

## Prerequisites

- Start from a clean checkout and follow [getting started](getting-started.md).
- Run the Docker stack with `DEMO_SEED_ENABLED=true`.
- Keep the seeded password at its local default, `Demo@123456`, or pass a different value to the script.
- Use Windows PowerShell 5.1 or PowerShell 7 with `curl.exe` available.

Example local setup:

```powershell
Copy-Item .env.example .env
$env:DEMO_SEED_ENABLED = "true"
docker compose up --detach --build --wait
```

Run the complete workflow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-workflow.ps1
```

`-ExecutionPolicy Bypass` applies only to this child process and does not change the machine-wide policy. With PowerShell 7, use `pwsh -File .\scripts\demo-workflow.ps1` instead.

For another API address or seed password:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-workflow.ps1 `
    -BaseUrl "https://localhost:7231" `
    -Password "Demo@123456"
```

The script creates uniquely named project and task records, uploads a temporary evidence file, submits progress, approves it, and verifies the final task, timeline, and KPI read model. It deletes the temporary local file in a `finally` block. Re-running it does not require cleanup.

## 10 To 15 Minute Agenda

| Time | Demonstration | Engineering point |
| --- | --- | --- |
| 0:00-1:30 | Show the repository structure and `Program.cs` | Layered modular monolith with a clear composition root, not claimed as microservices or full Clean Architecture. |
| 1:30-3:00 | Open Swagger and sign in as Manager and User | JWT role checks are an outer boundary; services also enforce resource and department scope. |
| 3:00-5:00 | Create a project and assign a task | Manager-only creation, server-derived department, DTO validation, and `201 Created` semantics. |
| 5:00-7:00 | Upload evidence as the assigned User | Task-scoped authorization, MIME/signature validation, private storage, and orphan prevention. |
| 7:00-9:30 | Submit 100 percent progress and approve it | Centralized workflow policy, evidence requirement, transaction boundary, concurrency token, and one-review constraint. |
| 9:30-11:00 | Read the task timeline | Permission-scoped, stable cursor pagination assembled from existing facts rather than duplicated event data. |
| 11:00-12:30 | Read KPI and workload documentation | Effective-dated staff history, explainable formula, immutable locked snapshots, and workload kept separate from scoring. |
| 12:30-14:00 | Show recurring/reminder workers and health endpoints | SQL-backed durable scheduling, idempotency keys, bounded retries, and distinct liveness/readiness semantics. |
| 14:00-15:00 | Show CI/tests and known limitations | Reproducible evidence and explicit non-goals instead of unsupported scale claims. |

## Expected Result

The final output includes:

```text
Demo workflow passed.
TaskStatus    : Approved
ProgressStatus: Approved
```

## Talking Points

- Project groups related tasks but does not duplicate task workflow state.
- Admin configures users and departments; only Manager assigns operational work.
- A normal User cannot create projects/tasks or review reports.
- Review-required completion is not accepted without task-scoped evidence.
- Database uniqueness and rowversion protect concurrent review and scheduler races.
- KPI is an explainable management insight, not an automated HR decision.

## Troubleshooting

- A readiness failure means SQL Server or upload storage is unavailable; check `docker compose ps` and container logs.
- A login failure usually means demo seed is disabled or the configured seed password differs.
- A port conflict on `8080` or `14333` means another local stack is already running.
- A script execution-policy error is avoided by the process-scoped command shown above.
- The script intentionally stops on the first failed assertion so a partial workflow is never presented as a passing demo.

Stop the local stack without deleting its data:

```powershell
docker compose down
```
