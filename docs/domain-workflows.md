# Các workflow trong domain

Những sơ đồ này mô tả implementation hiện tại, không phải đề xuất redesign.

## Task, Progress và Review

Trạng thái Task do server suy ra. Báo cáo Progress có trạng thái review riêng; `Rejected` thuộc về báo cáo và đưa Task trở lại `InProgress`.

```mermaid
stateDiagram-v2
    [*] --> NotStarted: Trưởng phòng tạo và giao công việc
    NotStarted --> InProgress: Nhân viên báo cáo một phần tiến độ
    NotStarted --> Submitted: Nhân viên báo cáo 100%; cần duyệt
    NotStarted --> Approved: Nhân viên báo cáo 100%; không cần duyệt
    InProgress --> InProgress: Nhân viên báo cáo một phần tiến độ
    InProgress --> Submitted: Nhân viên báo cáo 100%; cần duyệt
    InProgress --> Approved: Nhân viên báo cáo 100%; không cần duyệt
    Submitted --> Approved: Trưởng phòng duyệt báo cáo
    Submitted --> InProgress: Trưởng phòng từ chối và nêu lý do
    Approved --> [*]
```

Chỉ được hoàn thành khi mọi dependency chặn đã được duyệt và mọi assignee bắt buộc đều có lần hoàn thành được chấp thuận. Task yêu cầu review bắt buộc phải có evidence khi báo cáo 100%. Ma trận chuyển trạng thái được triển khai bởi [TaskWorkflowPolicy](../Domain/Workflows/TaskWorkflowPolicy.cs), điều phối bởi [TaskWorkflowService](../Application/Services/TaskWorkflowService.cs) và mô tả trong [workflow Task](task-workflow.md).

## Đồ thị dependency của Task

Một cạnh hướng từ prerequisite đến công việc bị nó chặn. Ví dụ là directed acyclic graph (DAG):

```mermaid
flowchart LR
    A[Thiết kế hợp đồng API] --> B[Triển khai endpoint]
    A --> C[Chuẩn bị fixture tích hợp]
    B --> D[Chạy xác minh workflow]
    C --> D
    D -. Cạnh đề xuất bị từ chối: tạo chu trình .-> A
```

`Triển khai endpoint` và `Chuẩn bị fixture tích hợp` tiếp tục bị chặn cho đến khi `Thiết kế hợp đồng API` được duyệt. `Chạy xác minh workflow` bị chặn đến khi cả hai công việc tiên quyết được duyệt. Thêm `D -> A` bị từ chối vì đã có một đường đi từ `A` đến `D`.

[TaskDependencyService](../Application/Services/TaskDependencyService.cs) phát hiện cycle trực tiếp và gián tiếp trước khi lưu. SQL Server là lớp bảo vệ cuối cùng cho self-reference và cạnh trùng. Hoàn thành hoặc xóa dependency đều ghi lịch sử Task để việc gỡ chặn hiển thị trên activity timeline.

## Authorization theo tài nguyên

Role authorization chỉ là lớp biên ngoài. Application service còn phải kiểm tra quyền trên tài nguyên cụ thể và phạm vi tổ chức hiện tại.

```mermaid
flowchart LR
    Request[Yêu cầu đã xác thực] --> Role{Endpoint cho phép role?}
    Role -->|Không| Forbidden[403 Forbidden]
    Role -->|Có| Actor[Tải User hiện tại và TokenVersion]
    Actor --> Resource[Tải Task, Project, báo cáo, kỳ hoặc phòng ban]
    Resource --> Scope{Phạm vi phòng ban, assignment, quyền sở hữu hoặc lịch sử hợp lệ?}
    Scope -->|Không| Forbidden
    Scope -->|Có| Rule{Invariant nghiệp vụ hợp lệ?}
    Rule -->|Không| Problem[409 hoặc 400 ProblemDetails]
    Rule -->|Có| Execute[Thực thi transaction của use case]
```

Ma trận authorization đầy đủ theo role/tài nguyên nằm trong [quy tắc nghiệp vụ](business-rules.md). Cụ thể, Admin cấu hình tổ chức nhưng không tạo Project hoặc giao công việc hằng ngày; Manager chỉ sở hữu việc thực thi trong phòng ban hiện tại; User chỉ thao tác trên tài nguyên Task được giao.

## Recurring Task bền vững

```mermaid
sequenceDiagram
    participant Worker as RecurringTaskWorker
    participant Service as RecurringTaskSchedulerService
    participant DB as SQL Server

    Worker->>Service: ProcessDueAsync
    Service->>DB: Đọc template hoạt động có NextRunAtUtc <= hiện tại
    loop Giới hạn template đến hạn và occurrence catch-up
        Service->>DB: Transaction serializable
        Service->>DB: Kiểm tra khóa occurrence
        Service->>DB: Thêm Task, assignee, history và occurrence
        Service->>DB: Cập nhật LastGeneratedAtUtc và NextRunAtUtc
    end
    DB-->>Service: Commit hoặc xung đột unique/concurrency
    Service-->>Worker: Số lượng đã xử lý, đã sinh và thất bại
```

`(TemplateId, ScheduledForUtc)` là duy nhất. Database lưu lịch chạy tiếp theo và các occurrence đã sinh nên process restart không làm mất hoặc lặp công việc. Worker chỉ chịu trách nhiệm polling và metric; quy tắc scheduling nằm trong application service.

## Deadline reminder và escalation

```mermaid
sequenceDiagram
    participant Worker as DeadlineReminderWorker
    participant Service as DeadlineReminderService
    participant DB as SQL Server
    participant Inbox as Hộp thư Notification

    Worker->>Service: ProcessDueAsync
    Service->>DB: Chuẩn bị milestone sắp đến hạn, quá hạn và escalation
    Note over Service,DB: Thứ tự policy: Project, sau đó Unit, rồi Global
    DB-->>Service: Lưu EventKey duy nhất trước khi gửi
    Service->>DB: Kiểm tra lại Task, policy và recipient
    alt Task đã hoàn thành, bị xóa hoặc policy bị tắt
        Service->>DB: Đánh dấu milestone là Suppressed
    else Có recipient
        Service->>Inbox: Lưu notification vào hộp thư
        Service->>DB: Đánh dấu milestone là Sent
    else Lỗi tạm thời
        Service->>DB: Đánh dấu Failed và tăng RetryCount có giới hạn
    end
```

`EventKey` và `(TaskId, Type)` ngăn milestone trùng. Trạng thái delivery tồn tại qua restart, còn Task đã hoàn thành hoặc bị xóa sẽ suppress delivery đang chờ. Không có in-memory queue nào là nguồn dữ liệu chuẩn.

## Workload, capacity và KPI

Workload planning và báo cáo KPI dùng dữ liệu Task liên quan cho hai mục đích khác nhau và không được trộn lẫn.

```mermaid
flowchart LR
    Capacity[Capacity tuần có hiệu lực theo thời gian] --> Workload[Tổng hợp workload theo khoảng ngày]
    Planned[PlannedEffortHours do Manager quản lý] --> Workload
    Assignment[Assignee được đề xuất] --> Preview[Xem trước workload dự kiến]
    Workload --> Preview
    Preview --> Warning[Cảnh báo Available, Busy hoặc Overloaded]
    Warning --> Create[Manager quyết định có tạo Task hay không]

    Approved[Dữ kiện Task và Progress đã Approved] --> Formula[Công thức KPI xác định có version]
    History[Lịch sử role và phòng ban] --> Formula
    Period[Kỳ KPI được tạo rõ ràng] --> Formula
    Formula --> Open[Insight có thể giải thích của kỳ đang mở]
    Formula --> Snapshot[Snapshot KPI đã khóa và bất biến]
```

Workload dự kiến là cảnh báo lập kế hoạch không chặn thao tác. `PlannedEffortHours` không thay đổi điểm KPI. KPI dùng kết quả workflow đã duyệt, dữ kiện deadline/từ chối, ranh giới kỳ và phạm vi tổ chức trong lịch sử. Kết quả đã khóa lưu formula version, identity snapshot và raw metric để thay đổi Task hoặc nhân sự sau này không thể viết lại lịch sử.

Implementation và bằng chứng:

- [WorkloadService](../Application/Services/WorkloadService.cs) thực hiện tính workload theo tập hợp.
- [UserPerformanceService](../Application/Services/UserPerformanceService.cs) tính input hiệu suất cá nhân và Manager.
- [KpiFormula](../Application/Common/KpiFormula.cs) sở hữu formula version và hằng số điểm xác định.
- [bằng chứng hiệu năng query](performance.md) ghi lại số SQL command bị giới hạn.
