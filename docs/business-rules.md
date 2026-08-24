# Business Rules

This document is the business contract for the WorkManagementSystem backend. Use it as the reference before changing service logic, database schema, or API behavior.

## Core Domain

WorkManagementSystem manages department-scoped work:

- Admin configures accounts and departments.
- Manager creates projects and tasks for their own department.
- User executes assigned tasks and reports progress.
- Progress completion can require evidence and manager review.
- KPI is calculated by period and must remain explainable when staff move between departments or roles.

## System Invariants

The following rules are the non-negotiable contract for all later refactoring and schema changes:

- A user has at most one active department membership at any point in time.
- Current user department and role must agree with the open `UserWorkHistories` segment.
- Department transfer, department removal, promotion, or demotion must not leave unfinished work without an owner.
- An organizational movement with unfinished work must either be rejected or run through an explicit, audited handover transaction.
- A project's department cannot be changed after the project is created.
- Every task linked to a project must belong to the same department as that project.
- Project summaries are derived from task statuses; projects do not duplicate the task workflow.
- Only an approved completion is final completion for reporting and KPI.
- A progress, completion, review, bonus, or penalty event can contribute to KPI at most once for the same user and period.
- Locked KPI results are immutable snapshots and must not change after later staff movements or task edits.

Until the dedicated handover workflow exists, the safe behavior is to reject an organizational movement when unfinished work would become ambiguous.

## Role Responsibilities

| Role | Owns | Must Not Own |
| --- | --- | --- |
| `Admin` | Account approval, department management, staff data, KPI period administration | Daily project/task assignment |
| `Manager` | Department project planning, task creation, task assignment, progress review, department KPI monitoring | Assigning work outside their department |
| `User` | Task execution, progress reporting, evidence upload, personal KPI view | Creating projects/tasks, reviewing reports |

### Admin Rules

- Admin can approve or reject registered accounts.
- Admin can create, update, and delete departments.
- Admin can update staff role/unit data.
- Admin can create and lock KPI periods.
- Admin does not create projects or assign tasks in the daily workflow.

This separation keeps the model realistic: Admin controls system setup, Manager controls work execution.

### Manager Rules

- Manager must belong to a department before creating work.
- Manager can create projects only for their department.
- Manager can create tasks only for their department.
- Manager can assign work only to approved users in their department.
- Manager can review only progress reports for active tasks in their current department; being the original creator does not bypass current department scope.

### User Rules

- User can see tasks assigned directly to them.
- User can report progress only on accessible tasks.
- User identity comes from the JWT token, not from request body data.
- User cannot create projects, create tasks, or review reports.
- User cannot report additional progress after the task is approved.

## Role And Permission Matrix

Role attributes provide the first API boundary. Application services then apply department, assignment, ownership, and historical-period checks; possessing a role never bypasses those scope rules.

| Capability | Anonymous | `Admin` | `Manager` | `User` |
| --- | --- | --- | --- | --- |
| Register, login, list public departments | Allowed | Allowed | Allowed | Allowed |
| Approve/reject accounts, reset another user's password | No | Allowed | No | No |
| Create/update/delete departments and memberships | No | Allowed | No | No |
| View users | No | All visible accounts | Own department | No |
| Change staff role or department | No | Allowed, subject to handover rules | No | No |
| Create/lock KPI periods | No | Allowed | No | No |
| Read KPI periods | No | Allowed | Allowed | Allowed |
| Read personal KPI | No | Any authorized staff history | Self or current/historical department scope | Self |
| Read department KPI | No | Allowed | Current/historical department scope | No |
| Read workload/capacity | No | All departments | Current department | No |
| Configure employee capacity | No | Any active employee | Current department employee | No |
| Create/update/archive projects | No | No | Own department | No |
| Create/update/delete/remind tasks | No | No | Own department | No |
| Manage reminder policies | No | All scopes | Own department/project scopes; Global is read-only | No |
| View task reminder history | No | Authorized task scope | Own department | Assigned tasks |
| View tasks and task history | No | Authorized system scope | Own department | Assigned tasks |
| Submit progress | No | No | No | Assigned tasks only |
| Review submitted progress | No | No | Own department/managed tasks | No |
| Upload/download task files | No | Authorized task scope | Own department | Assigned task scope |
| Use comments/subtasks | No | Authorized task scope | Own department | Assigned task scope; management-only mutations remain blocked |
| Export task/progress data | No | Authorized administrative scope | Own department | No |
| Read administrative audit logs | No | Allowed | No | No |

The only public controller actions are registration, login, and public department lookup. Liveness/readiness endpoints are also anonymous operational endpoints and expose health status rather than business data.

## Project And Task Relationship

Project is a grouping layer. Task is the actual work item.

| Concept | Purpose | Business Effect |
| --- | --- | --- |
| Project | Groups related tasks by goal, scope, or time window | Helps managers track status counts |
| Task | Represents executable work assigned to staff | Drives progress, review, completion, KPI |

Project rules:

- A project can exist without tasks.
- A task may exist without a project.
- Every project belongs to exactly one department.
- Project department is immutable after creation.
- A task and its linked project must belong to the same department.
- Linking a task to a project does not change task permissions.
- Task creator status does not bypass current role, assignment, or department scope.
- A project does not have a separate workflow from task statuses.
- Project status summary is derived from linked task counts.
- Only a Manager currently belonging to the project's department can manage it; creator status does not bypass department scope.
- A project can be archived only when all non-deleted linked tasks are `Approved`.
- An approved task in an archived project cannot be reopened until an explicit project restore workflow exists.

Derived project status summary:

| Task Status | Project Summary Meaning |
| --- | --- |
| `NotStarted` | Linked tasks have been created but no progress was reported. |
| `InProgress` | Staff reported partial progress. |
| `Submitted` | Staff submitted completion and is waiting for manager review. |
| `Approved` | Work was accepted and counts as completed. |

## Task Lifecycle

The task lifecycle is:

```text
NotStarted -> InProgress -> Submitted -> Approved
                      ^          |
                      |          v
                      +------ Rejected report returns task to InProgress
```

Task state and progress-report state are separate contracts:

| State Owner | Supported States | Changed By |
| --- | --- | --- |
| Task | `NotStarted`, `InProgress`, `Submitted`, `Approved` | Task creation, progress reporting, and review workflow |
| Progress report | `InProgress`, `Submitted`, `Approved`, `Rejected` | Progress submission and manager review |

`Rejected` is a progress-report review result. Rejection sends an unfinished task back to `InProgress`; it must not turn the task itself into a `Rejected` terminal state.

Task status is server-derived:

- Creation sets `NotStarted`.
- Partial progress sets `InProgress`.
- A completion report that needs review sets `Submitted`.
- Approval completes the task only when every required assignee has approved completion.
- Completion that does not need review can become `Approved` through the progress workflow.
- Clients and managers must not assign an arbitrary task status.
- `Approved` is immutable until a dedicated, audited reopen workflow exists.

The API does not expose a generic task-status mutation endpoint. The database limits task status values to the four supported states and normalizes the removed legacy `Rejected` value to `InProgress` during migration.

### Task Creation

- Only `Manager` can create tasks.
- Manager must have `UnitId`.
- New task starts as `NotStarted`.
- `ProjectId` is optional.
- If `ProjectId` is provided, the project must belong to the manager's department.
- `UnitId` on the task is the manager's department and is used for filtering, permission checks, reports, and KPI context.

### Task Assignment

Task assignee rows must target exactly one side:

- A specific approved user, or
- A department legacy scope.

Current creation behavior should prefer direct user assignee rows:

- If selected users are provided, the system assigns directly to those users.
- Selected users must be approved and inside the manager's department.
- If no selected users are provided, the system snapshots the current approved staff in the manager's department into direct assignee rows.
- Staff who join the department later are not automatically added to existing tasks.

Normal task updates do not change assignees. Reassignment should be implemented later as a separate audited workflow if needed.

### Task Activity Timeline

- `GET /api/tasks/{taskId}/timeline` uses the same current permission scope as reading the task.
- Events come from task history, immutable assignment snapshots, progress, review, comments, uploads, durable reminders/escalations, and task completion state.
- Results are ordered by UTC occurrence time descending, then a fixed source order, then the source event id. The opaque cursor preserves that ordering when timestamps are equal.
- Clients can filter by event type, actor, and an inclusive UTC date range. Page size is limited to `100`.
- Actor lookup ignores the active-user query filter so a soft-deleted actor retains their historical display name; a missing row uses a safe fallback.
- Deleted comments retain an activity marker but never return the deleted content.
- Only an allowlist of non-security task-history fields is projected. `AuditLog.DetailsJson`, credentials, tokens, storage paths, and raw entity JSON are never timeline metadata.
- Timeline metadata has a fixed DTO shape and text values are bounded. Query count is constant relative to the number of returned items.
- Assignment timestamps are derived from task creation because assignments are immutable creation-time snapshots in the current workflow.

### Recurring Task Scheduling

- Only a current `Manager` can create, update, pause, resume, or delete recurring schedules in their own department.
- Supported schedules are `Daily`, `Weekly`, and `Monthly`; complex cron expressions are intentionally unsupported.
- `NextRunAtUtc` and `LastGeneratedAtUtc` are persisted in SQL Server. The worker never uses an in-memory queue as the scheduling source of truth.
- A generated occurrence creates a normal `NotStarted` task. From that point onward it uses the existing assignment, progress, review, evidence, completion, workload, and KPI rules.
- An explicit default assignee list is validated against the template department. If no default users are selected, current approved `User` staff are snapshotted when each occurrence is generated.
- The unique key `(TemplateId, ScheduledForUtc)` is the final duplicate defense when workers race or restart.
- Task, assignees, history, notification metadata, occurrence, audit entry, `LastGeneratedAtUtc`, and `NextRunAtUtc` are saved in one transaction.
- Catch-up is bounded by `RecurringTasks:MaxCatchUpOccurrencesPerTemplate`. Unprocessed overdue occurrences remain due for the next batch and are not skipped.
- Paused or soft-deleted templates never generate tasks. Resuming preserves the persisted schedule, so overdue work follows the same catch-up policy.
- A project cannot be archived and a department cannot be deleted while a non-deleted recurring template still references it.
- A default assignee cannot be transferred, promoted, or deleted until the template is updated or removed.
- A monthly day such as 31 is clamped to the last valid day of shorter months while retaining day 31 for later months.

### Deadline Reminder And Escalation

- Policies can be scoped to the whole system, one department, or one project. The effective policy is selected in the order `Project > Unit > Global`; an inactive specific policy intentionally suppresses fallback reminders for that scope.
- Admin can manage every policy scope. A Manager can manage only Unit and Project policies belonging to their current department and cannot change the Global policy.
- `BeforeDueHours` controls the due-soon milestone. The overdue assignee milestone starts at the deadline, and `OverdueEscalationHours` controls escalation after the deadline.
- Persistence uses UTC. Date/time localization belongs to presentation clients.
- Each task has at most one `DueSoon`, `OverdueAssignee`, and `ManagerEscalation` event. Unique `(TaskId, Type)` and `EventKey` constraints are the final duplicate defense.
- The worker persists milestone state in SQL Server. `Pending` and retryable `Failed` records survive restarts; retry count is bounded by `DeadlineReminders:MaxRetryCount`.
- Delivery rechecks task state, effective policy, and recipients. `Approved` or soft-deleted tasks, removed deadlines, and disabled policies produce `Suppressed` events instead of inbox notifications.
- Assignee reminders go only to current approved `User` recipients that remain in the task department. Escalation goes only to current approved `Manager` accounts in that same department.
- Inbox notification creation and the transition to `Sent` share one database transaction. A competing worker that loses the rowversion update rolls back its duplicate inbox rows.
- `GET /api/tasks/{taskId}/reminders` follows normal task authorization and exposes safe scheduling history without raw exception or audit payloads.

### Workload And Capacity Planning

- `PlannedEffortHours` is optional and is entered by the Manager as a resource-planning budget.
- It is not an employee timesheet, does not prove time worked, and is never used by KPI calculations.
- Remaining workload is derived as `max(PlannedEffortHours - ActualHours, 0)`; it is not stored as a duplicate column.
- Only non-deleted tasks that are not `Approved` and overlap the selected date range contribute to workload.
- A multi-assignee task divides its remaining workload equally across its direct assignees.
- Weekly capacity is effective-dated. A range crossing a capacity change is prorated by the duration of each capacity segment.
- Gaps without a user-specific capacity use `Workload:DefaultWeeklyCapacityHours`.
- `Busy` begins at `Workload:BusyThresholdPercent`; `Overloaded` begins at `Workload:OverloadedThresholdPercent`.
- Assignment preview and task creation may return a workload warning, but overload never blocks task creation.
- Managers can read and configure only current employees in their own department. Admin can access all departments for system administration.
- Workload list aggregation uses set-based SQL queries and must not execute one query per employee.

### Task Completion

- A task is completed only when the workflow service marks it `Approved`.
- For multi-assignee tasks, the task should become approved only after all assigned staff have approved completion.
- `CompletedAt` and `CompletedBy` record completion context.
- `ActualHours` is accumulated from approved progress reports.
- A manager cannot bypass progress evidence or review by directly marking a task `Approved`.
- A task can be soft-deleted only while it is `NotStarted` and has no progress, review, upload, or other execution activity.

## Progress Reporting

Progress reporting rules:

- Reporter must be an approved `User`.
- Reporter must have access to the task.
- Progress percent is constrained to 0-100.
- Hours spent cannot be negative.
- Partial progress moves the task to `InProgress`.
- 100 percent progress means the user claims completion.

Blocking rules:

- User cannot report progress on an approved task.
- User cannot submit another completion report while a submitted completion report is pending review.
- Evidence file cannot be reused by another progress report.
- Evidence file must belong to the same task when linked.

## Review Flow

If task review is required:

1. User uploads evidence for the task.
2. User submits 100 percent progress with the evidence file.
3. Progress status becomes `Submitted`.
4. Task status becomes `Submitted`.
5. Manager reviews the report.
6. Approval marks progress as `Approved`.
7. Approval may complete the task if completion conditions are satisfied.
8. Rejection marks progress as `Rejected`.
9. If the task is not approved yet, rejection moves the task back to `InProgress` unless another report is still pending.

Review invariants:

- One progress report can have only one review result.
- Only `Manager` can review.
- Manager can review only tasks they manage.
- Rejection requires a non-empty reason.
- Invalid transitions, blocked dependencies, and out-of-scope actors fail before workflow state is persisted.
- Every task/progress transition records old state, new state, actor, related report, reason, and UTC timestamp in task history.
- Concurrent reviews use optimistic concurrency plus the unique review constraint; only one decision and one approved-hours contribution can commit.
- Rejected reports affect KPI penalties.

The complete transition matrix and implementation ownership are documented in [task workflow](task-workflow.md).

If task review is not required:

- 100 percent progress can be approved directly.
- The workflow service can complete the task without a manager review.

## Upload Rules

Uploads are task/progress evidence, not arbitrary storage.

- File size is limited to 10 MB.
- File extension and MIME type are validated.
- File signatures are checked for common formats.
- `.docx`, `.xlsx`, and `.pptx` must contain the expected OOXML package structure, stay within archive entry/uncompressed-size limits, and must not contain a VBA macro payload.
- Original file names are reduced to a safe base name, stripped of control characters, and capped in length before persistence.
- File metadata is persisted only after the physical file is accepted.
- If persistence fails, physical file cleanup is attempted.
- Download requires task/progress access.
- Database metadata stores a bounded relative `StorageKey`, never an absolute server path.
- A storage key must be a single file name and must resolve inside the configured `Uploads` root.
- A durable periodic reconciliation deletes aged physical files that have no corresponding database row.
- Public DTOs must not expose server file paths.

## KPI Rules

KPI is period-based and should be explainable, not just a live score.

### KPI Periods

- Admin creates KPI periods explicitly.
- Read-only KPI endpoints must not create periods or write to the database.
- New KPI periods cannot overlap existing periods.
- Period start date must be before end date.
- Admin can lock KPI periods.
- Locked periods read stored `KpiResults` snapshots when available.
- A locked snapshot stores the formula version and raw metrics used to explain its score.
- Source task, report, user, or department changes after locking must not change the snapshot.

### Personal KPI

Personal KPI considers:

- Assigned tasks in the effective period.
- Approved completions.
- On-time completion.
- Late completion.
- Overdue unfinished work.
- Rejected reports.
- Bonus and penalty points.
- Raw throughput, completion rate, overdue rate, report rejection rate, and effort-planning accuracy.

The score should never become negative.

Current scoring behavior:

- Score starts at `100`.
- Approved on-time task with a deadline adds weighted bonus points.
- Approved task without a deadline adds a smaller weighted bonus.
- Consecutive on-time completions can add a small streak bonus.
- Overdue unfinished work subtracts escalating weighted penalty points.
- Rejected progress reports subtract penalty points.
- Users with no tasks in the period receive a neutral new/starter score, not a punishment.

Current formula version is `1.0`. Formula version is persisted on every locked result. Derived rates use frozen raw counters and return zero when their count denominator is zero. Estimation accuracy is left unavailable when no completed task has a Manager-owned effort plan.

Effort-planning accuracy is informational only:

- It uses completed tasks with `PlannedEffortHours`.
- Planned effort is shared equally across task assignees to avoid duplicate department totals.
- Actual effort uses only the user's approved progress reports.
- It does not add bonus points, subtract penalty points, or otherwise affect the KPI score.

### Manager KPI

Manager KPI combines:

- Department average performance.
- Manager's own assigned work performance.
- Review delay/quality penalties when applicable.

Current logic gives more weight to department performance than personal task performance for managers.

Current weighting:

- Department average score: 70 percent.
- Manager personal task score: 30 percent.
- Review penalty points are subtracted after weighting.

### Management Insights

- Admin can read the organization dashboard for a selected KPI period.
- Manager can read only the dashboard for their current department.
- Dashboard aggregation is performed in batches and must not issue a query per user.
- Dashboard output groups users by department and exposes the same formula version and frozen raw metrics as personal KPI output.
- KPI is an explainable management aid. The system must not use it to automatically promote, demote, discipline, dismiss, or otherwise make HR decisions.

## Staff Movement And KPI

This is the most sensitive business area.

Current user row stores the latest state:

- Current department.
- Current role.
- Current `JoinedUnitAt`.

Historical state is stored in `UserWorkHistories`:

- `UserId`
- `UnitId`
- `Role`
- `EffectiveFrom`
- `EffectiveTo`
- Change reason and changer context

KPI calculation must use work history segments when a user changes department or role inside a period.

Movement rules:

- A movement must update the current user state and work-history segments in one database transaction.
- Work-history segments for the same user must not overlap.
- A transfer closes the old segment immediately before opening the new segment.
- Existing task assignment snapshots must not silently follow a user into another department.
- Pending direct assignments, submitted reports, and manager review responsibilities must be resolved before the movement is accepted.
- Account deletion is rejected while unfinished task or review responsibility remains.
- Account deletion closes the active work-history segment, removes current unit membership, revokes sessions, and soft-deletes the account in one serializable transaction.
- Completed task assignments, progress reports, reviews, uploads, and KPI history are not deleted with the account.
- Every accepted movement must record who made the change, when it took effect, and why.

Examples:

- If a User moves from Department A to Department B in the middle of July, July KPI should be explainable as two segments.
- If a User becomes Manager in the middle of a period, KPI should not blindly apply Manager logic to the entire period.
- If a period is locked before a later staff movement, locked `KpiResults` should preserve the old score and context.
- A Manager can read another user's KPI only when the selected period's locked snapshot or work-history segment belongs to the Manager's current department.
- Current department membership must not grant access to a historical period that belongs exclusively to another department.

## Soft Delete And Archive

- Users, departments, tasks, and comments use soft delete.
- Projects use archive.
- Query filters hide inactive records by default.
- Historical records remain for audit, KPI, and reporting.
- Historical assignment, progress, review, work-history, and locked KPI queries intentionally remain available after a user is soft-deleted.

## Authentication Sessions

- Every JWT carries the user's current `TokenVersion`.
- An authenticated request is accepted only when the account still exists, is approved, is not deleted, and the JWT version matches the database version.
- Password changes, administrator password resets, role changes, department changes, account rejection, and account deletion invalidate all previously issued JWTs.
- Profile-only changes such as full name, email, or phone number do not invalidate sessions.
- Existing JWTs issued before `TokenVersion` support are intentionally rejected and users must sign in again.
- Registration, password reset, and password change share one password policy and one BCrypt hashing service.
- Successful login upgrades hashes created with an older BCrypt work factor.
- Development can use an ephemeral JWT key, while every non-Development environment must supply a key externally.
- SignalR requires the same JWT session validation as HTTP endpoints.
- Joining a task discussion group requires current access to that task through `ITaskAccessService`.

## Audit And History

The backend keeps two kinds of history and does not duplicate their responsibilities:

- Domain history records (`TaskHistories`, `UserWorkHistories`, `Progresses`, `Reviews`, and locked `KpiResults`) explain workflow and KPI outcomes.
- `AuditLogs` records important account and configuration actions with entity, action, actor, time, and controlled JSON details.

Audit rules:

- Audit rows are append-only through the public API; there is no update or delete endpoint.
- Account approval/rejection/deletion, password changes/resets, staff assignment changes, department changes, project changes, and KPI period creation/locking are audited.
- Audit records are added to the same database transaction as the business mutation.
- Failed or rolled-back operations must not leave successful audit rows.
- Passwords, password hashes, JWTs, file contents, and upload server paths must never be written to `DetailsJson`.
- Only `Admin` can query `/api/audit-logs`.

## Database Integrity Rules

Database constraints protect critical invariants:

- Unique usernames.
- Unique employee codes.
- Unique department names.
- Unique project name per department.
- Unique task assignee rows.
- One review per progress report.
- Progress percent range.
- Non-negative hours.
- Valid KPI date ranges.
- Non-negative KPI counters and scores.
- Valid KPI effective date ranges.

Business services must still validate before save so API responses stay user-friendly.

## Rules For Future Refactoring

When refactoring services, preserve these boundaries:

- Controllers should stay thin and only translate HTTP/auth context to service calls.
- Permission checks should remain centralized instead of repeated by hand.
- Task assignment resolution should be isolated from task creation.
- DTO building should be isolated from business mutation logic.
- KPI period resolution, work-history segmentation, and scoring should be separate from controller logic.
- Public API contracts should not change unless frontend and docs are updated together.
