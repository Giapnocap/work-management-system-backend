# Portfolio Evidence And Interview Guide

This document turns repository claims into code and test evidence. It is a preparation aid, not a substitute for understanding the implementation.

## Project Identity

WorkManagementSystem is a layered modular monolith for department-scoped workflow, collaboration, and workforce visibility. Its engineering focus is resource authorization, task dependencies, progress/review transitions, workload capacity, durable scheduling, reminders, activity history, and explainable KPI snapshots.

An e-commerce backend normally emphasizes catalog, inventory, cart, order, payment, and fulfillment consistency. Those are not this repository's domain. WorkManagementSystem also does not claim broker-backed messaging, distributed transactions, microservices, or independent layer deployment.

## Suggested CV Entry

**Work Management System Backend | ASP.NET Core 8, EF Core, SQL Server, xUnit, Docker**

- Built a layered modular-monolith Web API for department-scoped project/task assignment, evidence-based progress reporting, Manager review, activity timelines, and KPI insights.
- Enforced role and resource authorization across current department membership, task assignment, and historical KPI scope, with revocable JWT sessions.
- Implemented explicit task workflow and DAG dependencies with cycle detection, transition history, optimistic concurrency, and relational uniqueness constraints.
- Built SQL-backed recurring-task and deadline-reminder workers with idempotency keys, restart-safe state, bounded retry, health checks, and operational metrics.
- Added set-based workload/KPI queries, reproducible SQL command budgets, unit/API/SQL Server integration tests, Docker migration verification, and a backup/restore drill in CI.

Keep only bullets you can explain from request to database. Do not add throughput, latency, availability, or percentage-improvement claims without a reproducible benchmark and retained result.

## Claim Evidence

| CV claim | Implementation evidence | Test evidence |
| --- | --- | --- |
| Layered modular monolith with a clear HTTP/application/data boundary | [Program composition root](../Program.cs), [application registrations](../Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs), [infrastructure registrations](../Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs), [architecture guide](architecture.md) | [architecture dependency tests](../WorkManagementSystem.Tests/ArchitectureDependencyTests.cs), [API contract tests](../WorkManagementSystem.Tests/ApiContractIntegrationTests.cs) |
| Role plus resource-scoped authorization | [task access service](../Application/Services/TaskAccessService.cs), [current user service](../API/Authentication/CurrentUserService.cs), [business-rule matrix](business-rules.md) | [task access security tests](../WorkManagementSystem.Tests/TaskAccessSecurityTests.cs), [authorization contract tests](../WorkManagementSystem.Tests/ApiAuthorizationContractTests.cs), [workflow HTTP tests](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs) |
| Explicit task/progress/review workflow with concurrency protection | [workflow policy](../Domain/Workflows/TaskWorkflowPolicy.cs), [workflow service](../Application/Services/TaskWorkflowService.cs), [review service](../Application/Services/ReviewService.cs), [transaction manager](../Infrastructure/Data/EfTransactionManager.cs) | [workflow policy tests](../WorkManagementSystem.Tests/TaskWorkflowPolicyTests.cs), [progress/review tests](../WorkManagementSystem.Tests/ProgressReviewServiceTests.cs), [SQL Server relational tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs) |
| DAG task dependencies with cycle rejection | [dependency service](../Application/Services/TaskDependencyService.cs), [dependency entity](../Domain/Entities/TaskDependency.cs), [dependency configuration](../Infrastructure/Data/Configurations/TaskDependencyConfiguration.cs) | [dependency service tests](../WorkManagementSystem.Tests/TaskDependencyServiceTests.cs), [HTTP workflow tests](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs), [database model tests](../WorkManagementSystem.Tests/DatabaseModelTests.cs) |
| Durable recurring tasks and deadline reminders | [recurring scheduler](../Application/Services/RecurringTaskSchedulerService.cs), [recurring worker](../Infrastructure/Scheduling/RecurringTaskWorker.cs), [deadline service](../Application/Services/DeadlineReminderService.cs), [deadline worker](../Infrastructure/Scheduling/DeadlineReminderWorker.cs) | [recurring scheduler tests](../WorkManagementSystem.Tests/RecurringTaskSchedulerTests.cs), [deadline tests](../WorkManagementSystem.Tests/DeadlineReminderServiceTests.cs), [worker metric tests](../WorkManagementSystem.Tests/BackgroundJobMetricsTests.cs), [SQL Server relational tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs) |
| Workload planning and explainable KPI snapshots | [workload service](../Application/Services/WorkloadService.cs), [performance service](../Application/Services/UserPerformanceService.cs), [KPI formula](../Application/Common/KpiFormula.cs), [KPI service](../Application/Services/KpiService.cs) | [workload tests](../WorkManagementSystem.Tests/WorkloadServiceTests.cs), [KPI tests](../WorkManagementSystem.Tests/KpiServiceTests.cs), [staff-history KPI tests](../WorkManagementSystem.Tests/UserKpiWorkHistoryTests.cs), [query budgets](performance.md) |
| Hardened task-scoped evidence uploads | [upload service](../Application/Services/UploadService.cs), [upload controller](../API/Controllers/UploadController.cs), [upload configuration](../Infrastructure/Data/Configurations/UploadFileConfiguration.cs) | [upload tests](../WorkManagementSystem.Tests/UploadServiceTests.cs), [orphan cleanup tests](../WorkManagementSystem.Tests/UploadOrphanCleanupTests.cs), [workflow HTTP tests](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs) |
| Repeatable migrations, recovery verification, and operational health | [Compose stack](../compose.yml), [CI workflow](../.github/workflows/backend-ci.yml), [backup/restore drill](../scripts/backup-restore-drill.ps1), [health checks](../Infrastructure/Health/DatabaseHealthCheck.cs) | [operational tests](../WorkManagementSystem.Tests/OperationalObservabilityTests.cs), [migration and constraint tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs), [recovery guide](recovery-and-workers.md) |

## Interview Questions And Answers

### 1. Is this Clean Architecture?

No. It is a layered modular monolith with logical API, Application, Domain, and Infrastructure namespaces in one runtime assembly. Architecture tests protect useful dependency boundaries, but Application still exposes EF-oriented query abstractions, so claiming full persistence ignorance would be inaccurate. Evidence: [architecture guide](architecture.md) and [architecture tests](../WorkManagementSystem.Tests/ArchitectureDependencyTests.cs).

### 2. Why keep controllers thin?

Controllers translate HTTP input, resolve the authenticated user, call one application use case, and choose the status code. Authorization of the concrete task/project/report and transactional business changes remain in services. Evidence: [task controller](../API/Controllers/TaskController.cs) and [task service](../Application/Services/TaskService.cs).

### 3. Why is role authorization insufficient?

Two Managers must not manage each other's departments. Endpoint roles reject the wrong actor category; `TaskAccessService` then checks current department, creator/assignment, management scope, and permitted history. Evidence: [task access service](../Application/Services/TaskAccessService.cs) and [security tests](../WorkManagementSystem.Tests/TaskAccessSecurityTests.cs).

### 4. How are revoked JWTs rejected before expiry?

The token contains a version tied to the user record. Password reset, role/unit-sensitive account changes, or deletion invalidates sessions by changing `TokenVersion`; current-user resolution rejects an old version. Evidence: [current user service](../API/Authentication/CurrentUserService.cs) and [security operation tests](../WorkManagementSystem.Tests/SecurityOperationsTests.cs).

### 5. Why centralize task transitions?

Progress creation, review, and other use cases must not each invent their own state changes. `TaskWorkflowPolicy` defines legal transitions while `TaskWorkflowService` applies state and history changes consistently. Evidence: [workflow diagram](task-workflow.md) and [policy tests](../WorkManagementSystem.Tests/TaskWorkflowPolicyTests.cs).

### 6. How is double review prevented?

The service checks current report state and existing review, then updates inside a transaction. Rowversion detects stale writes and a unique database constraint on the progress review is the final race guard. Evidence: [review service](../Application/Services/ReviewService.cs), [progress configuration](../Infrastructure/Data/Configurations/ProgressConfiguration.cs), and [SQL tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 7. Why does dependency validation need more than a foreign key?

A foreign key proves both tasks exist but cannot prove the graph is acyclic. The service searches for an existing path before adding an edge; SQL constraints independently reject self-reference and duplicate edges. Evidence: [dependency service](../Application/Services/TaskDependencyService.cs) and [dependency tests](../WorkManagementSystem.Tests/TaskDependencyServiceTests.cs).

### 8. What happens when a blocking task completes?

The dependent task is not auto-completed; it merely becomes eligible for its normal progress workflow. Dependency and unblock facts are recorded in task history and become visible in the timeline. Evidence: [task workflow service](../Application/Services/TaskWorkflowService.cs) and [workflow integration tests](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs).

### 9. How does recurring scheduling survive restart?

Templates store `NextRunAtUtc`; generated occurrences store a unique `(TemplateId, ScheduledForUtc)` key. Each occurrence is created transactionally and the persisted schedule advances, so the database, not worker memory, is the source of truth. Evidence: [scheduler](../Application/Services/RecurringTaskSchedulerService.cs), [occurrence configuration](../Infrastructure/Data/Configurations/GeneratedTaskOccurrenceConfiguration.cs), and [scheduler tests](../WorkManagementSystem.Tests/RecurringTaskSchedulerTests.cs).

### 10. What if two scheduler instances race?

Both may discover the same due template, but the serializable transaction, optimistic concurrency, and unique occurrence key allow only one committed occurrence. The losing attempt is handled as a conflict rather than producing duplicate work. Evidence: [scheduler](../Application/Services/RecurringTaskSchedulerService.cs) and [SQL Server tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 11. How are reminders made idempotent?

The service stages a persisted milestone with a unique event key before delivery, revalidates the task/policy/recipient, and records sent, failed, or suppressed state. Retries are bounded and completed/deleted tasks suppress stale delivery. Evidence: [deadline service](../Application/Services/DeadlineReminderService.cs) and [deadline tests](../WorkManagementSystem.Tests/DeadlineReminderServiceTests.cs).

### 12. Why is workload not part of KPI score?

Planned effort is a Manager estimate used for capacity warnings. KPI measures approved outcomes and deadline/rejection facts. Mixing estimated workload into employee score would reward or punish a Manager's estimate rather than verified work. Evidence: [domain workflows](domain-workflows.md), [workload service](../Application/Services/WorkloadService.cs), and [KPI formula](../Application/Common/KpiFormula.cs).

### 13. How are staff transfers handled in KPI?

Effective-dated work history defines the user's role/unit during a period. Locked results preserve identity, unit, formula version, and raw metric snapshots, so later transfers or deletion cannot rewrite historical output. Evidence: [performance service](../Application/Services/UserPerformanceService.cs) and [work-history KPI tests](../WorkManagementSystem.Tests/UserKpiWorkHistoryTests.cs).

### 14. Do KPI GET endpoints write data?

No. KPI periods are explicitly created through commands and reads resolve an existing period. This avoids hidden writes, unique-key races, and surprising side effects in GET requests. Evidence: [KPI service](../Application/Services/KpiService.cs) and [KPI tests](../WorkManagementSystem.Tests/KpiServiceTests.cs).

### 15. How were EF Core query costs checked?

Read paths use projections, `AsNoTracking` where appropriate, and set-based aggregates. Integration tests seed a medium dataset and enforce SQL command-count budgets for task lists, workload, timeline, and KPI dashboard reads. Evidence: [performance notes](performance.md) and [SQL Server tests](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 16. Why are liveness and readiness separate?

Liveness answers whether the process is running. Readiness also checks database connectivity and upload-storage writability, so a dependency outage removes the instance from traffic without claiming the process itself is dead. Evidence: [Program endpoints](../Program.cs), [health checks](../Infrastructure/Health/UploadStorageHealthCheck.cs), and [operational tests](../WorkManagementSystem.Tests/OperationalObservabilityTests.cs).

### 17. What upload threats are addressed?

The service requires a valid task/progress context, checks resource access, bounds size/extensions, compares MIME/signature, validates OOXML contents, rejects macros, sanitizes names, keeps physical paths private, and cleans orphaned files. It does not claim antivirus scanning. Evidence: [upload service](../Application/Services/UploadService.cs) and [upload tests](../WorkManagementSystem.Tests/UploadServiceTests.cs).

### 18. How is database recovery verified?

The CI drill takes a checksum backup, restores into a temporary database, compares critical row counts, and runs `DBCC CHECKDB`. It verifies the procedure for this local SQL Server topology; it is not a claim of production RPO/RTO. Evidence: [restore script](../scripts/backup-restore-drill.ps1), [CI workflow](../.github/workflows/backend-ci.yml), and [recovery guide](recovery-and-workers.md).

### 19. Why does the timeline not use a separate event store?

The system already persists authoritative task history, progress, reviews, comments, files, and scheduled notifications. The timeline composes a permission-scoped read model from those facts, avoiding another synchronization source while using stable time/id cursor ordering. Evidence: [timeline service](../Application/Services/TaskTimelineService.cs) and [timeline tests](../WorkManagementSystem.Tests/TaskTimelineServiceTests.cs).

### 20. What would you improve next?

Validate KPI policy with a real organization, add refresh-token rotation if the client requires long sessions, move uploads to object storage with malware scanning for production, version public APIs, and define production infrastructure/CD. A broker or service split would be justified only by measured operational needs, not by portfolio appearance. Evidence: [README](../README.md) and [production checklist](production-checklist.md).

## Known Limitations And Non-Goals

- One deployable ASP.NET Core assembly; no independently deployed modules or microservices.
- No refresh-token flow, broker, transactional outbox, distributed SignalR backplane, Redis cache, or distributed lock.
- SQL Server is the supported database provider.
- Local/container file storage is private and validated but has no external malware scanner or object-storage adapter.
- KPI policy is demonstrative domain policy and must be validated before HR use.
- CI validates build, tests, migrations, containers, and recovery, but the repository does not define cloud infrastructure or production CD.
- Query budgets detect regressions in database round trips; they are not latency, throughput, or scale benchmarks.

The canonical and current limitation list remains in the [README](../README.md).
