# Task And Progress Workflow

This document is the current task and progress workflow contract.

## State Ownership

Task and progress report states are separate. A rejected report does not create a rejected task state.

| Owner | States |
| --- | --- |
| Task | `NotStarted`, `InProgress`, `Submitted`, `Approved` |
| Progress | `InProgress`, `Submitted`, `Approved`, `Rejected` |
| Review | Immutable approval/rejection decision linked one-to-one to a submitted progress report |

## Task Transition Matrix

| From | To | Trigger | Effective actor | Required context |
| --- | --- | --- | --- | --- |
| `NotStarted` | `InProgress` | Partial progress | Assigned User | Current task access; dependencies complete |
| `NotStarted` | `Submitted` | 100% report requiring review | Assigned User | Current task access; evidence rule; dependencies complete |
| `NotStarted` | `Approved` | 100% report without review | System rule initiated by assigned User | Every required assignee complete; dependencies complete |
| `InProgress` | `Submitted` | 100% report requiring review | Assigned User | Current task access; evidence rule; dependencies complete |
| `InProgress` | `Approved` | Final required completion | System rule | Every required assignee complete; dependencies complete |
| `Submitted` | `Approved` | Manager approves final required report | Current-unit Manager | Management scope; every required assignee complete |
| `Submitted` | `InProgress` | Manager rejects, or approval is not enough to complete | Current-unit Manager | Management scope; rejection requires reason |

`Submitted -> Submitted` can occur as a no-op when another submitted report remains pending. It is not persisted as a state transition. `Approved` is terminal because no reopen workflow currently exists.

## Progress Transition Matrix

| From | To | Trigger | Actor | Required context |
| --- | --- | --- | --- | --- |
| New | `InProgress` | Partial report | Assigned User | Current task access; dependencies complete |
| New | `Submitted` | 100% report requiring review | Assigned User | Evidence when required; dependencies complete |
| New | `Approved` | 100% report without review | Assigned User + system completion rule | Dependencies and duplicate-completion rules pass |
| `Submitted` | `Approved` | Review approval | Current-unit Manager | Report has no previous review |
| `Submitted` | `Rejected` | Review rejection | Current-unit Manager | Non-empty reason; report has no previous review |

## API Ownership

- `POST /api/progress` is the only progress-report command and derives task state server-side.
- `POST /api/review` is the only approval/rejection command.
- There is no generic task-status mutation endpoint.
- Separate `/start`, `/submit`, `/approve`, or `/reject` task commands are intentionally not added because they would bypass progress evidence and review ownership.

## Implementation Ownership

- `TaskWorkflowPolicy` contains the explicit transition matrix and contextual requirements. It is domain-specific and is not a generic workflow engine.
- `TaskWorkflowService` is the only application service that mutates task completion state, reviewed progress state, approved `ActualHours`, and transition history.
- `ProgressService` owns reporter identity, assignment scope, duplicate completion, evidence, and dependency preconditions before delegating the state change.
- `ReviewService` owns Manager role and current department scope before delegating the review decision.
- A rejection requires a non-empty reason. Invalid or unauthorized requests fail before persisted state changes.
- Every persisted task/progress transition writes `TaskHistory` with the related progress id and a bounded reason.
- `Progress.RowVersion`, `TaskItem.RowVersion`, the unique review index, and the surrounding transaction ensure racing review requests persist one decision and one approved-hours contribution.
