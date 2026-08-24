# Recovery and background worker operations

Last recovery drill: 2026-08-24 against the disposable `wms-phase8` SQL Server Compose project.

Verified record counts after restore (`migrations|units|users|tasks|kpi`):

```text
47|1|4|4|0
```

## Backup and restore drill

The drill is for test and development databases. It performs:

1. `BACKUP DATABASE` with `COPY_ONLY` and `CHECKSUM`.
2. `RESTORE VERIFYONLY` with checksum validation.
3. Restore into a uniquely named temporary database with discovered logical file names.
4. Compare migration, unit, user, task and KPI record counts.
5. Run `DBCC CHECKDB` on the restored database.
6. Remove the temporary database and backup file in a `finally` block.

With the normal Compose stack running:

```powershell
./scripts/backup-restore-drill.ps1
```

For an isolated Compose project:

```powershell
./scripts/backup-restore-drill.ps1 -ComposeProjectName "wms-recovery-test"
```

The CI workflow runs the same script after migrations and demo seeding. Backups are created inside the SQL Server container and are never stored in the repository.

## Migration recovery coverage

`SqlServerRelationalTests` covers both supported validation paths:

- empty database to the latest migration;
- `20260821020506_CentralizeTaskWorkflowStateMachine` to latest while preserving a KPI snapshot and validating the explainability backfill.

## Worker durability and observability

Recurring schedules, generated occurrence keys and scheduled deadline notifications are persisted in SQL Server. Restart tests prove that a worker restart neither loses the next schedule nor duplicates an already finalized occurrence or inbox notification.

The `.NET` meter is `WorkManagementSystem.BackgroundJobs` and exposes:

- `workmanagement.background_job.executions`;
- `workmanagement.background_job.items`;
- `workmanagement.background_job.duration` in milliseconds.

Metric tags are intentionally low-cardinality: `job.name`, `job.outcome`, `item.kind` and `item.outcome`. Resource IDs are not metric tags. Failed recurring templates are logged with `TemplateId`; failed reminders are logged with `ScheduledNotificationId`, `TaskId`, `NotificationType`, `RetryCount` and `RetryStatePersisted`.

`/health/live` checks only process liveness. `/health/ready` checks SQL Server and upload storage. A database outage must make readiness fail without making liveness fail.
