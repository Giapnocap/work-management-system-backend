# Các workflow trong domain

Những sơ đồ này mô tả implementation hiện tại, không phải đề xuất redesign.

## Task, Progress và Review

Trạng thái Task do server suy ra. Báo cáo Progress có trạng thái review riêng; `Rejected` thuộc về báo cáo và đưa Task trở lại `InProgress`.

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

Chỉ được hoàn thành khi mọi dependency chặn đã được duyệt và mọi assignee bắt buộc đều có lần hoàn thành được chấp thuận. Task yêu cầu review bắt buộc phải có evidence khi báo cáo 100%. Ma trận chuyển trạng thái được triển khai bởi [TaskWorkflowPolicy](../Domain/Workflows/TaskWorkflowPolicy.cs), điều phối bởi [TaskWorkflowService](../Application/Services/TaskWorkflowService.cs) và mô tả trong [workflow Task](task-workflow.md).

## Đồ thị dependency của Task

Một cạnh hướng từ prerequisite đến công việc bị nó chặn. Ví dụ là directed acyclic graph (DAG):

```mermaid
flowchart LR
    A[Design API contract] --> B[Implement endpoint]
    A --> C[Prepare integration fixture]
    B --> D[Run workflow verification]
    C --> D
    D -. Proposed edge rejected: creates cycle .-> A
```

`Implement endpoint` và `Prepare integration fixture` tiếp tục bị chặn cho đến khi `Design API contract` được duyệt. `Run workflow verification` bị chặn đến khi cả hai predecessor được duyệt. Thêm `D -> A` bị từ chối vì đã có một path từ `A` đến `D`.

[TaskDependencyService](../Application/Services/TaskDependencyService.cs) phát hiện cycle trực tiếp và gián tiếp trước khi lưu. SQL Server là lớp bảo vệ cuối cùng cho self-reference và cạnh trùng. Hoàn thành hoặc xóa dependency đều ghi lịch sử Task để việc gỡ chặn hiển thị trên activity timeline.

## Authorization theo tài nguyên

Role authorization chỉ là lớp biên ngoài. Application service còn phải kiểm tra quyền trên tài nguyên cụ thể và phạm vi tổ chức hiện tại.

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

Ma trận authorization đầy đủ theo role/tài nguyên nằm trong [quy tắc nghiệp vụ](business-rules.md). Cụ thể, Admin cấu hình tổ chức nhưng không tạo Project hoặc giao công việc hằng ngày; Manager chỉ sở hữu việc thực thi trong phòng ban hiện tại; User chỉ thao tác trên tài nguyên Task được giao.

## Recurring Task bền vững

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

`(TemplateId, ScheduledForUtc)` là duy nhất. Database lưu lịch chạy tiếp theo và các occurrence đã sinh nên process restart không làm mất hoặc lặp công việc. Worker chỉ chịu trách nhiệm polling và metric; quy tắc scheduling nằm trong application service.

## Deadline reminder và escalation

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

`EventKey` và `(TaskId, Type)` ngăn milestone trùng. Trạng thái delivery tồn tại qua restart, còn Task đã hoàn thành hoặc bị xóa sẽ suppress delivery đang chờ. Không có in-memory queue nào là nguồn dữ liệu chuẩn.

## Workload, capacity và KPI

Workload planning và báo cáo KPI dùng dữ liệu Task liên quan cho hai mục đích khác nhau và không được trộn lẫn.

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

Workload dự kiến là cảnh báo lập kế hoạch không chặn thao tác. `PlannedEffortHours` không thay đổi điểm KPI. KPI dùng kết quả workflow đã duyệt, dữ kiện deadline/từ chối, ranh giới kỳ và phạm vi tổ chức trong lịch sử. Kết quả đã khóa lưu formula version, identity snapshot và raw metric để thay đổi Task hoặc nhân sự sau này không thể viết lại lịch sử.

Implementation và bằng chứng:

- [WorkloadService](../Application/Services/WorkloadService.cs) thực hiện tính workload theo tập hợp.
- [UserPerformanceService](../Application/Services/UserPerformanceService.cs) tính input hiệu suất cá nhân và Manager.
- [KpiFormula](../Application/Common/KpiFormula.cs) sở hữu formula version và hằng số điểm xác định.
- [bằng chứng hiệu năng query](performance.md) ghi lại số SQL command bị giới hạn.
