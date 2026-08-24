# Domain Workflows

These diagrams describe the current implementation. They are not a proposed redesign.

## Task, Progress And Review

Task state is server-derived. A progress report has its own review state; `Rejected` belongs to the report and returns the task to `InProgress`.

```mermaid
stateDiagram-v2
    [*] --> NotStarted: Manager creates and assigns task
    NotStarted --> InProgress: User reports partial progress
    NotStarted --> Submitted: User reports 100%; review required
    NotStarted --> Approved: User reports 100%; no review required
    InProgress --> InProgress: User reports partial progress
    InProgress --> Submitted: User reports 100%; review required
    InProgress --> Approved: User reports 100%; no review required
    Submitted --> Approved: Manager approves report
    Submitted --> InProgress: Manager rejects report with reason
    Approved --> [*]
```

Completion is allowed only when all blocking dependencies are approved and every required assignee has an accepted completion. Evidence is mandatory for a 100 percent report when the task requires review. The transition matrix is implemented by [TaskWorkflowPolicy](../Domain/Workflows/TaskWorkflowPolicy.cs), orchestrated by [TaskWorkflowService](../Application/Services/TaskWorkflowService.cs), and documented in [task workflow](task-workflow.md).

## Task Dependency Graph

An edge points from a prerequisite to the work it blocks. The example is a directed acyclic graph (DAG):

```mermaid
flowchart LR
    A[Design API contract] --> B[Implement endpoint]
    A --> C[Prepare integration fixture]
    B --> D[Run workflow verification]
    C --> D
    D -. Proposed edge rejected: creates cycle .-> A
```

`Implement endpoint` and `Prepare integration fixture` remain blocked until `Design API contract` is approved. `Run workflow verification` remains blocked until both predecessors are approved. Adding `D -> A` is rejected because a path already exists from `A` to `D`.

[TaskDependencyService](../Application/Services/TaskDependencyService.cs) detects direct and indirect cycles before persistence. SQL Server remains the final guard for self-reference and duplicate edges. Dependency completion and removal write task history so an unblock is visible in the activity timeline.

## Resource Authorization

Role authorization is only the outer boundary. Application services must also authorize the concrete resource and current organizational scope.

```mermaid
flowchart LR
    Request[Authenticated request] --> Role{Endpoint role allowed?}
    Role -->|No| Forbidden[403 Forbidden]
    Role -->|Yes| Actor[Load current user and TokenVersion]
    Actor --> Resource[Load task, project, report, period or unit]
    Resource --> Scope{Current unit, assignment, ownership or history scope valid?}
    Scope -->|No| Forbidden
    Scope -->|Yes| Rule{Business invariant valid?}
    Rule -->|No| Problem[409 or 400 ProblemDetails]
    Rule -->|Yes| Execute[Execute use case transaction]
```

The complete role/resource authorization matrix is in [business rules](business-rules.md). In particular, Admin configures the organization but does not create projects or assign daily work; Manager owns work execution only inside the current department; User acts only on assigned task resources.

## Durable Recurring Tasks

```mermaid
sequenceDiagram
    participant Worker as RecurringTaskWorker
    participant Service as RecurringTaskSchedulerService
    participant DB as SQL Server

    Worker->>Service: ProcessDueAsync
    Service->>DB: Read active templates where NextRunAtUtc <= now
    loop Bounded due templates and catch-up occurrences
        Service->>DB: Serializable transaction
        Service->>DB: Check occurrence key
        Service->>DB: Insert normal task, assignees, history and occurrence
        Service->>DB: Advance LastGeneratedAtUtc and NextRunAtUtc
    end
    DB-->>Service: Commit or unique/concurrency conflict
    Service-->>Worker: Processed, generated and failure counts
```

`(TemplateId, ScheduledForUtc)` is unique. The database stores the next schedule and generated occurrences, so process restarts do not lose or repeat work. The worker owns polling and metrics only; scheduling rules remain in the application service.

## Deadline Reminder And Escalation

```mermaid
sequenceDiagram
    participant Worker as DeadlineReminderWorker
    participant Service as DeadlineReminderService
    participant DB as SQL Server
    participant Inbox as Notification inbox

    Worker->>Service: ProcessDueAsync
    Service->>DB: Stage due-soon, overdue and escalation milestones
    Note over Service,DB: Policy precedence: Project, then Unit, then Global
    DB-->>Service: Persist unique EventKey before delivery
    Service->>DB: Revalidate task, policy and recipients
    alt Task completed, deleted or policy disabled
        Service->>DB: Mark milestone Suppressed
    else Recipient available
        Service->>Inbox: Persist inbox notification
        Service->>DB: Mark milestone Sent
    else Transient failure
        Service->>DB: Mark Failed and increment bounded RetryCount
    end
```

`EventKey` and `(TaskId, Type)` prevent duplicate milestones. Delivery state survives restart, and a completed or deleted task suppresses pending delivery. No in-memory queue is the source of truth.

## Workload, Capacity And KPI

Workload planning and KPI reporting use related task data for different purposes and must not be conflated.

```mermaid
flowchart LR
    Capacity[Effective-dated weekly capacity] --> Workload[Date-range workload aggregation]
    Planned[Manager-owned PlannedEffortHours] --> Workload
    Assignment[Proposed assignees] --> Preview[Projected workload preview]
    Workload --> Preview
    Preview --> Warning[Available, Busy or Overloaded warning]
    Warning --> Create[Manager decides whether to create task]

    Approved[Approved task and progress facts] --> Formula[Versioned deterministic KPI formula]
    History[Role and unit work history] --> Formula
    Period[Explicit KPI period] --> Formula
    Formula --> Open[Explainable open-period insight]
    Formula --> Snapshot[Immutable locked KPI snapshot]
```

Projected workload is a non-blocking planning warning. `PlannedEffortHours` does not change KPI score. KPI uses approved workflow outcomes, deadline/rejection facts, period boundaries and historical organizational scope. Locked results store formula version, identity snapshots and raw metrics so later task or staff changes cannot rewrite history.

Implementation and evidence:

- [WorkloadService](../Application/Services/WorkloadService.cs) performs set-based workload calculations.
- [UserPerformanceService](../Application/Services/UserPerformanceService.cs) calculates personal and manager performance inputs.
- [KpiFormula](../Application/Common/KpiFormula.cs) owns formula version and deterministic score constants.
- [query performance evidence](performance.md) records bounded SQL command counts.
