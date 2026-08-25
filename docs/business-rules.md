# Quy tắc nghiệp vụ

Tài liệu này là hợp đồng nghiệp vụ của backend WorkManagementSystem. Hãy dùng nó làm nguồn tham chiếu trước khi thay đổi logic service, database schema hoặc hành vi API.

## Miền nghiệp vụ chính

WorkManagementSystem quản lý công việc theo phạm vi phòng ban:

- Admin cấu hình tài khoản và phòng ban.
- Manager tạo Project và Task cho phòng ban của mình.
- User thực hiện Task được giao và báo cáo Progress.
- Hoàn thành Progress có thể yêu cầu evidence và Manager review.
- KPI được tính theo kỳ và phải tiếp tục giải thích được khi nhân sự chuyển phòng ban hoặc đổi role.

## Các quy tắc bất biến của hệ thống

Các quy tắc sau là hợp đồng bắt buộc đối với mọi lần refactor và thay đổi schema sau này:

- Mỗi User có tối đa một membership phòng ban đang hoạt động tại cùng một thời điểm.
- Phòng ban và role hiện tại của User phải khớp với segment `UserWorkHistories` đang mở.
- Chuyển phòng ban, xóa khỏi phòng ban, thăng chức hoặc hạ chức không được để công việc chưa hoàn thành mất người chịu trách nhiệm.
- Thay đổi tổ chức khi còn công việc chưa hoàn thành phải bị từ chối hoặc đi qua một transaction bàn giao rõ ràng, có audit.
- Không thể thay đổi phòng ban của Project sau khi tạo.
- Mọi Task liên kết với Project phải thuộc cùng phòng ban với Project đó.
- Tổng hợp Project được suy ra từ trạng thái Task; Project không sao chép workflow của Task.
- Chỉ lần hoàn thành đã được duyệt mới là hoàn thành cuối cùng cho báo cáo và KPI.
- Một event Progress, completion, review, bonus hoặc penalty chỉ được đóng góp vào KPI tối đa một lần cho cùng User và kỳ.
- Kết quả KPI đã khóa là snapshot bất biến và không thay đổi theo điều chuyển nhân sự hoặc chỉnh sửa Task sau đó.

Cho đến khi có workflow bàn giao chuyên dụng, hành vi an toàn là từ chối thay đổi tổ chức nếu công việc chưa hoàn thành sẽ trở nên mơ hồ về trách nhiệm.

## Trách nhiệm theo vai trò

| Role | Chịu trách nhiệm | Không được chịu trách nhiệm |
| --- | --- | --- |
| `Admin` | Duyệt tài khoản, quản lý phòng ban, dữ liệu nhân sự, quản trị kỳ KPI | Giao Project/Task hằng ngày |
| `Manager` | Lập kế hoạch Project của phòng ban, tạo Task, giao Task, review Progress, theo dõi KPI phòng ban | Giao việc ngoài phòng ban của mình |
| `User` | Thực hiện Task, báo cáo Progress, tải evidence, xem KPI cá nhân | Tạo Project/Task, review báo cáo |

### Quy tắc Admin

- Admin có thể duyệt hoặc từ chối tài khoản đã đăng ký.
- Admin có thể tạo, cập nhật và xóa phòng ban.
- Admin có thể cập nhật dữ liệu role/unit của nhân sự.
- Admin có thể tạo và khóa kỳ KPI.
- Admin không tạo Project hoặc giao Task trong workflow hằng ngày.

Sự phân tách này giữ mô hình sát thực tế: Admin kiểm soát thiết lập hệ thống, Manager kiểm soát thực thi công việc.

### Quy tắc Manager

- Manager phải thuộc một phòng ban trước khi tạo công việc.
- Manager chỉ có thể tạo Project cho phòng ban của mình.
- Manager chỉ có thể tạo Task cho phòng ban của mình.
- Manager chỉ có thể giao việc cho User đã được duyệt trong phòng ban của mình.
- Manager chỉ có thể review báo cáo Progress cho Task đang hoạt động trong phòng ban hiện tại; là người tạo ban đầu không thể bỏ qua phạm vi phòng ban hiện tại.

### Quy tắc User

- User có thể xem Task được giao trực tiếp cho mình.
- User chỉ có thể báo cáo Progress trên Task được quyền truy cập.
- Danh tính User lấy từ JWT token, không lấy từ request body.
- User không thể tạo Project, tạo Task hoặc review báo cáo.
- User không thể báo cáo thêm Progress sau khi Task được duyệt.

## Ma trận role và quyền

Role attribute cung cấp ranh giới API đầu tiên. Sau đó application service áp dụng kiểm tra phòng ban, assignment, quyền sở hữu và kỳ lịch sử; chỉ sở hữu role không bao giờ được phép bỏ qua các quy tắc phạm vi này.

| Khả năng | Anonymous | `Admin` | `Manager` | `User` |
| --- | --- | --- | --- | --- |
| Đăng ký, đăng nhập, xem phòng ban công khai | Được phép | Được phép | Được phép | Được phép |
| Duyệt/từ chối tài khoản, đặt lại mật khẩu User khác | Không | Được phép | Không | Không |
| Tạo/cập nhật/xóa phòng ban và membership | Không | Được phép | Không | Không |
| Xem User | Không | Mọi tài khoản có thể xem | Phòng ban của mình | Không |
| Đổi role hoặc phòng ban nhân sự | Không | Được phép, tuân theo quy tắc bàn giao | Không | Không |
| Tạo/khóa kỳ KPI | Không | Được phép | Không | Không |
| Đọc kỳ KPI | Không | Được phép | Được phép | Được phép |
| Đọc KPI cá nhân | Không | Mọi lịch sử nhân sự được phép | Bản thân hoặc phạm vi phòng ban hiện tại/lịch sử | Bản thân |
| Đọc KPI phòng ban | Không | Được phép | Phạm vi phòng ban hiện tại/lịch sử | Không |
| Đọc workload/capacity | Không | Mọi phòng ban | Phòng ban hiện tại | Không |
| Cấu hình capacity nhân viên | Không | Mọi nhân viên đang hoạt động | Nhân viên phòng ban hiện tại | Không |
| Tạo/cập nhật/lưu trữ Project | Không | Không | Phòng ban của mình | Không |
| Tạo/cập nhật/xóa/nhắc Task | Không | Không | Phòng ban của mình | Không |
| Quản lý reminder policy | Không | Mọi scope | Scope phòng ban/Project của mình; Global chỉ đọc | Không |
| Xem lịch sử reminder của Task | Không | Phạm vi Task được phép | Phòng ban của mình | Task được giao |
| Xem Task và lịch sử Task | Không | Phạm vi hệ thống được phép | Phòng ban của mình | Task được giao |
| Gửi Progress | Không | Không | Không | Chỉ Task được giao |
| Review Progress đã gửi | Không | Không | Phòng ban/Task mình quản lý | Không |
| Upload/download file Task | Không | Phạm vi Task được phép | Phòng ban của mình | Phạm vi Task được giao |
| Dùng comment/SubTask | Không | Phạm vi Task được phép | Phòng ban của mình | Phạm vi Task được giao; thay đổi chỉ dành cho quản lý vẫn bị chặn |
| Export dữ liệu Task/Progress | Không | Phạm vi quản trị được phép | Phòng ban của mình | Không |
| Đọc administrative audit log | Không | Được phép | Không | Không |

Các controller action công khai duy nhất là đăng ký, đăng nhập và tra cứu phòng ban công khai. Endpoint liveness/readiness cũng là endpoint vận hành anonymous và chỉ công bố health status thay vì dữ liệu nghiệp vụ.

## Quan hệ Project và Task

Project là lớp gom nhóm. Task là công việc thực tế.

| Khái niệm | Mục đích | Tác động nghiệp vụ |
| --- | --- | --- |
| Project | Gom các Task liên quan theo mục tiêu, phạm vi hoặc khoảng thời gian | Giúp Manager theo dõi số lượng theo trạng thái |
| Task | Đại diện công việc có thể thực hiện được giao cho nhân sự | Điều khiển Progress, Review, hoàn thành và KPI |

Quy tắc Project:

- Project có thể tồn tại mà chưa có Task.
- Task có thể tồn tại mà không thuộc Project.
- Mỗi Project thuộc đúng một phòng ban.
- Phòng ban của Project là bất biến sau khi tạo.
- Task và Project liên kết phải thuộc cùng phòng ban.
- Liên kết Task với Project không thay đổi quyền trên Task.
- Trạng thái người tạo Task không thể bỏ qua role, assignment hoặc phạm vi phòng ban hiện tại.
- Project không có workflow riêng ngoài trạng thái Task.
- Tổng hợp trạng thái Project được suy ra từ số Task liên kết.
- Chỉ Manager hiện thuộc phòng ban của Project mới có thể quản lý Project đó; là người tạo không thể bỏ qua phạm vi phòng ban.
- Project chỉ có thể lưu trữ khi mọi Task liên kết chưa bị xóa đều là `Approved`.
- Task đã duyệt trong Project đã lưu trữ không thể mở lại cho đến khi có workflow khôi phục Project rõ ràng.

Tổng hợp trạng thái Project được suy ra:

| Trạng thái Task | Ý nghĩa trong tổng hợp Project |
| --- | --- |
| `NotStarted` | Task liên kết đã được tạo nhưng chưa có báo cáo Progress. |
| `InProgress` | Nhân sự đã báo cáo Progress một phần. |
| `Submitted` | Nhân sự đã gửi báo cáo hoàn thành và đang chờ Manager review. |
| `Approved` | Công việc đã được chấp nhận và được tính là hoàn thành. |

## Vòng đời Task

Vòng đời Task:

```text
NotStarted -> InProgress -> Submitted -> Approved
                      ^          |
                      |          v
                      +------ Báo cáo Rejected đưa Task về InProgress
```

Trạng thái Task và trạng thái báo cáo Progress là hai hợp đồng riêng:

| Thành phần sở hữu trạng thái | Trạng thái hỗ trợ | Được thay đổi bởi |
| --- | --- | --- |
| Task | `NotStarted`, `InProgress`, `Submitted`, `Approved` | Tạo Task, báo cáo Progress và workflow Review |
| Báo cáo Progress | `InProgress`, `Submitted`, `Approved`, `Rejected` | Gửi Progress và Manager review |

`Rejected` là kết quả review báo cáo Progress. Từ chối đưa Task chưa hoàn thành trở lại `InProgress`; nó không được biến chính Task thành trạng thái cuối `Rejected`.

Trạng thái Task do server suy ra:

- Khi tạo, Task là `NotStarted`.
- Progress một phần đặt Task thành `InProgress`.
- Báo cáo hoàn thành cần review đặt Task thành `Submitted`.
- Review chấp thuận chỉ hoàn thành Task khi mọi assignee bắt buộc đều có lần hoàn thành đã duyệt.
- Hoàn thành không cần review có thể chuyển thành `Approved` qua workflow Progress.
- Client và Manager không được gán trạng thái Task tùy ý.
- `Approved` là bất biến cho đến khi có workflow mở lại chuyên dụng, được audit.

API không cung cấp generic endpoint thay đổi trạng thái Task. Database giới hạn giá trị trạng thái Task ở bốn trạng thái hỗ trợ và chuẩn hóa giá trị cũ `Rejected` đã bị loại bỏ thành `InProgress` trong migration.

### Tạo Task

- Chỉ `Manager` có thể tạo Task.
- Manager phải có `UnitId`.
- Task mới bắt đầu ở `NotStarted`.
- `ProjectId` là tùy chọn.
- Nếu có `ProjectId`, Project phải thuộc phòng ban của Manager.
- `UnitId` của Task là phòng ban Manager và được dùng để lọc, kiểm tra quyền, báo cáo và ngữ cảnh KPI.

### Giao Task

Hàng Task assignee phải trỏ đến đúng một phía:

- Một User cụ thể đã được duyệt, hoặc
- Scope phòng ban cũ.

Hành vi tạo hiện tại phải ưu tiên hàng assignee User trực tiếp:

- Nếu có danh sách User được chọn, hệ thống giao trực tiếp cho những User đó.
- User được chọn phải đã được duyệt và thuộc phòng ban Manager.
- Nếu không chọn User, hệ thống snapshot nhân sự `User` đã được duyệt hiện tại trong phòng ban Manager thành các hàng assignee trực tiếp.
- Nhân sự vào phòng ban sau đó không tự động được thêm vào Task hiện có.

Cập nhật Task thông thường không thay đổi assignee. Nếu cần, reassignment nên được triển khai sau như workflow riêng có audit.

### Activity timeline của Task

- `GET /api/tasks/{taskId}/timeline` dùng cùng phạm vi quyền hiện tại với thao tác đọc Task.
- Event đến từ lịch sử Task, assignment snapshot bất biến, Progress, Review, comment, upload, reminder/escalation bền vững và trạng thái hoàn thành Task.
- Kết quả sắp xếp giảm dần theo thời điểm UTC xảy ra, sau đó theo thứ tự nguồn cố định rồi source event id. Opaque cursor giữ thứ tự này khi timestamp bằng nhau.
- Client có thể lọc theo loại event, actor và khoảng ngày UTC inclusive. Page size bị giới hạn `100`.
- Tra cứu actor bỏ qua active-user query filter để actor đã soft delete vẫn giữ display name lịch sử; nếu thiếu hàng thì dùng fallback an toàn.
- Comment đã xóa vẫn giữ activity marker nhưng không bao giờ trả về nội dung đã xóa.
- Chỉ allowlist field lịch sử Task không nhạy cảm được projection. `AuditLog.DetailsJson`, credential, token, storage path và JSON entity thô không bao giờ là metadata timeline.
- Metadata timeline có DTO shape cố định và text value bị giới hạn. Số query là hằng số theo số item trả về.
- Timestamp assignment được suy ra từ thời điểm tạo Task vì assignment là snapshot bất biến lúc tạo trong workflow hiện tại.

### Lập lịch recurring Task

- Chỉ `Manager` hiện tại có thể tạo, cập nhật, tạm dừng, tiếp tục hoặc xóa recurring schedule trong phòng ban của mình.
- Schedule hỗ trợ `Daily`, `Weekly` và `Monthly`; biểu thức cron phức tạp cố ý không được hỗ trợ.
- `NextRunAtUtc` và `LastGeneratedAtUtc` được lưu trong SQL Server. Worker không bao giờ dùng in-memory queue làm nguồn dữ liệu chuẩn của scheduling.
- Mỗi occurrence được sinh tạo một Task `NotStarted` thông thường. Từ đó Task dùng toàn bộ quy tắc assignment, Progress, Review, evidence, completion, workload và KPI hiện có.
- Danh sách assignee mặc định rõ ràng được kiểm tra với phòng ban template. Nếu không chọn User mặc định, nhân sự `User` hiện tại đã duyệt được snapshot khi từng occurrence được sinh.
- Unique key `(TemplateId, ScheduledForUtc)` là lớp chống trùng cuối khi worker cạnh tranh hoặc restart.
- Task, assignee, history, notification metadata, occurrence, audit entry, `LastGeneratedAtUtc` và `NextRunAtUtc` được lưu trong cùng một transaction.
- Catch-up bị giới hạn bởi `RecurringTasks:MaxCatchUpOccurrencesPerTemplate`. Occurrence quá hạn chưa xử lý vẫn ở trạng thái đến hạn cho batch sau và không bị bỏ qua.
- Template tạm dừng hoặc soft delete không bao giờ sinh Task. Resume giữ persisted schedule nên công việc quá hạn tuân theo cùng catch-up policy.
- Không thể lưu trữ Project hoặc xóa phòng ban khi recurring template chưa xóa vẫn tham chiếu đến nó.
- Không thể điều chuyển, thăng chức hoặc xóa assignee mặc định cho đến khi template được cập nhật hoặc xóa.
- Ngày theo tháng như 31 được giới hạn về ngày hợp lệ cuối của tháng ngắn hơn nhưng vẫn giữ ngày 31 cho các tháng sau.

### Deadline reminder và escalation

- Policy có thể áp dụng cho toàn hệ thống, một phòng ban hoặc một Project. Policy hiệu lực được chọn theo thứ tự `Project > Unit > Global`; policy cụ thể không hoạt động cố ý suppress fallback reminder cho scope đó.
- Admin có thể quản lý mọi policy scope. Manager chỉ quản lý policy Unit và Project thuộc phòng ban hiện tại, không thể thay đổi Global policy.
- `BeforeDueHours` kiểm soát milestone sắp đến hạn. Milestone quá hạn của assignee bắt đầu tại deadline, còn `OverdueEscalationHours` kiểm soát escalation sau deadline.
- Persistence dùng UTC. Chuyển đổi date/time theo địa phương thuộc presentation client.
- Mỗi Task có tối đa một event `DueSoon`, `OverdueAssignee` và `ManagerEscalation`. Constraint duy nhất `(TaskId, Type)` và `EventKey` là lớp chống trùng cuối cùng.
- Worker lưu trạng thái milestone trong SQL Server. Bản ghi `Pending` và `Failed` có thể retry tồn tại qua restart; retry count bị giới hạn bởi `DeadlineReminders:MaxRetryCount`.
- Delivery kiểm tra lại trạng thái Task, effective policy và recipient. Task `Approved` hoặc soft delete, deadline bị xóa và policy bị tắt tạo event `Suppressed` thay vì inbox notification.
- Reminder cho assignee chỉ gửi đến recipient `User` hiện tại đã duyệt và vẫn thuộc phòng ban Task. Escalation chỉ gửi đến tài khoản `Manager` hiện tại đã duyệt trong cùng phòng ban.
- Tạo inbox notification và chuyển sang `Sent` dùng chung database transaction. Worker cạnh tranh thua cập nhật rowversion sẽ rollback inbox row trùng.
- `GET /api/tasks/{taskId}/reminders` tuân theo authorization Task bình thường và trả lịch sử scheduling an toàn, không gồm exception hoặc audit payload thô.

### Lập kế hoạch workload và capacity

- `PlannedEffortHours` là tùy chọn và do Manager nhập làm ngân sách lập kế hoạch nguồn lực.
- Đây không phải timesheet của nhân viên, không chứng minh thời gian đã làm và không bao giờ được dùng trong tính KPI.
- Workload còn lại được suy ra bằng `max(PlannedEffortHours - ActualHours, 0)`; không lưu thành cột trùng.
- Chỉ Task chưa xóa, chưa `Approved` và giao với khoảng ngày được chọn mới đóng góp vào workload.
- Task nhiều assignee chia đều workload còn lại cho các assignee trực tiếp.
- Weekly capacity có hiệu lực theo thời gian. Khoảng đi qua một lần thay đổi capacity được tính tỷ lệ theo độ dài từng capacity segment.
- Khoảng trống không có capacity riêng của User dùng `Workload:DefaultWeeklyCapacityHours`.
- `Busy` bắt đầu tại `Workload:BusyThresholdPercent`; `Overloaded` bắt đầu tại `Workload:OverloadedThresholdPercent`.
- Assignment preview và tạo Task có thể trả về cảnh báo workload, nhưng overload không bao giờ chặn tạo Task.
- Manager chỉ được đọc và cấu hình nhân viên hiện tại trong phòng ban mình. Admin có thể truy cập mọi phòng ban để quản trị hệ thống.
- Tổng hợp danh sách workload dùng set-based SQL query và không được chạy một query cho mỗi nhân viên.

### Hoàn thành Task

- Task chỉ hoàn thành khi workflow service đánh dấu `Approved`.
- Với Task nhiều assignee, Task chỉ được duyệt sau khi mọi nhân sự được giao đều có lần hoàn thành đã duyệt.
- `CompletedAt` và `CompletedBy` ghi ngữ cảnh hoàn thành.
- `ActualHours` được cộng từ báo cáo Progress đã duyệt.
- Manager không thể bỏ qua evidence hoặc Review bằng cách đặt Task thành `Approved` trực tiếp.
- Task chỉ có thể soft delete khi đang `NotStarted` và không có Progress, Review, upload hoặc hoạt động thực thi khác.

## Báo cáo Progress

Quy tắc báo cáo Progress:

- Người báo cáo phải là `User` đã được duyệt.
- Người báo cáo phải có quyền truy cập Task.
- Percent Progress bị giới hạn 0-100.
- Hours spent không được âm.
- Progress một phần đưa Task sang `InProgress`.
- Progress 100 phần trăm nghĩa là User khai báo hoàn thành.

Quy tắc chặn:

- User không thể báo cáo Progress trên Task đã duyệt.
- User không thể gửi báo cáo hoàn thành khác khi một báo cáo hoàn thành đã gửi đang chờ review.
- Evidence file không thể tái sử dụng cho báo cáo Progress khác.
- Evidence file phải thuộc cùng Task khi liên kết.

## Luồng Review

Nếu Task yêu cầu review:

1. User tải evidence cho Task lên.
2. User gửi Progress 100 phần trăm kèm evidence file.
3. Trạng thái Progress thành `Submitted`.
4. Trạng thái Task thành `Submitted`.
5. Manager review báo cáo.
6. Chấp thuận đánh dấu Progress là `Approved`.
7. Chấp thuận có thể hoàn thành Task nếu các điều kiện hoàn thành được đáp ứng.
8. Từ chối đánh dấu Progress là `Rejected`.
9. Nếu Task chưa được duyệt, từ chối đưa Task về `InProgress` trừ khi báo cáo khác vẫn đang chờ.

Invariant của Review:

- Một báo cáo Progress chỉ có tối đa một kết quả Review.
- Chỉ `Manager` có thể Review.
- Manager chỉ có thể Review Task mình quản lý.
- Từ chối phải có lý do không rỗng.
- Transition không hợp lệ, dependency đang chặn và actor ngoài scope thất bại trước khi trạng thái workflow được lưu.
- Mỗi transition Task/Progress ghi trạng thái cũ, trạng thái mới, actor, báo cáo liên quan, lý do và timestamp UTC trong lịch sử Task.
- Review đồng thời dùng optimistic concurrency cùng unique constraint của Review; chỉ một quyết định và một lần cộng approved-hours được commit.
- Báo cáo bị từ chối ảnh hưởng penalty KPI.

Ma trận transition đầy đủ và quyền sở hữu implementation được mô tả trong [workflow Task](task-workflow.md).

Nếu Task không yêu cầu review:

- Progress 100 phần trăm có thể được duyệt trực tiếp.
- Workflow service có thể hoàn thành Task mà không cần Manager review.

## Quy tắc upload

Upload là evidence Task/Progress, không phải storage tùy ý.

- Kích thước file giới hạn 10 MB.
- Extension và MIME type được kiểm tra.
- File signature được kiểm tra cho các format phổ biến.
- `.docx`, `.xlsx` và `.pptx` phải có đúng cấu trúc OOXML package, nằm trong giới hạn số entry/kích thước giải nén và không được chứa VBA macro payload.
- Tên file gốc được rút thành base name an toàn, xóa control character và giới hạn độ dài trước khi lưu.
- Metadata file chỉ được lưu sau khi file vật lý được chấp nhận.
- Nếu lưu persistence thất bại, hệ thống cố gắng dọn file vật lý.
- Download yêu cầu quyền truy cập Task/Progress.
- Metadata database lưu `StorageKey` tương đối đã giới hạn độ dài, không bao giờ lưu absolute server path.
- Storage key phải là một file name duy nhất và resolve bên trong root `Uploads` đã cấu hình.
- Quy trình đối soát định kỳ bền vững xóa file vật lý đủ cũ mà không có database row tương ứng.
- Public DTO không được làm lộ server file path.

## Quy tắc KPI

KPI dựa theo kỳ và phải giải thích được, không chỉ là điểm số trực tiếp.

### Kỳ KPI

- Admin tạo kỳ KPI rõ ràng.
- Endpoint chỉ đọc KPI không được tạo kỳ hoặc ghi database.
- Kỳ KPI mới không được giao với kỳ hiện có.
- Ngày bắt đầu phải trước ngày kết thúc.
- Admin có thể khóa kỳ KPI.
- Kỳ đã khóa đọc snapshot `KpiResults` đã lưu khi có.
- Snapshot đã khóa lưu formula version và raw metric dùng để giải thích điểm.
- Thay đổi Task, báo cáo, User hoặc phòng ban sau khi khóa không được thay đổi snapshot.

### KPI cá nhân

KPI cá nhân xét:

- Task được giao trong kỳ hiệu lực.
- Lần hoàn thành đã duyệt.
- Hoàn thành đúng hạn.
- Hoàn thành trễ.
- Công việc quá hạn chưa hoàn thành.
- Báo cáo bị từ chối.
- Điểm bonus và penalty.
- Raw throughput, completion rate, overdue rate, report rejection rate và độ chính xác lập kế hoạch effort.

Điểm không bao giờ được âm.

Hành vi tính điểm hiện tại:

- Điểm bắt đầu là `100`.
- Task có deadline được duyệt đúng hạn cộng bonus point có trọng số.
- Task không có deadline được duyệt cộng bonus nhỏ hơn.
- Chuỗi hoàn thành đúng hạn liên tiếp có thể cộng streak bonus nhỏ.
- Công việc quá hạn chưa hoàn thành trừ penalty point tăng dần có trọng số.
- Báo cáo Progress bị từ chối trừ penalty point.
- User không có Task trong kỳ nhận điểm trung lập dành cho người mới, không bị phạt.

Formula version hiện tại là `1.0`. Formula version được lưu trong mọi kết quả đã khóa. Derived rate dùng raw counter đã đóng băng và trả về zero khi count denominator bằng zero. Độ chính xác ước tính để trống khi không có Task hoàn thành nào có effort plan do Manager sở hữu.

Độ chính xác lập kế hoạch effort chỉ mang tính thông tin:

- Dùng Task đã hoàn thành có `PlannedEffortHours`.
- Planned effort được chia đều cho assignee để tránh nhân đôi tổng phòng ban.
- Actual effort chỉ dùng báo cáo Progress đã duyệt của chính User.
- Không cộng bonus point, trừ penalty point hoặc ảnh hưởng điểm KPI theo bất kỳ cách nào.

### KPI Manager

KPI Manager kết hợp:

- Hiệu suất trung bình phòng ban.
- Hiệu suất công việc cá nhân được giao cho Manager.
- Penalty về độ trễ/chất lượng Review khi áp dụng.

Logic hiện tại đặt trọng số hiệu suất phòng ban lớn hơn hiệu suất Task cá nhân của Manager.

Trọng số hiện tại:

- Điểm trung bình phòng ban: 70 phần trăm.
- Điểm Task cá nhân của Manager: 30 phần trăm.
- Review penalty point được trừ sau khi áp dụng trọng số.

### Insight quản lý

- Admin có thể đọc dashboard tổ chức theo một kỳ KPI được chọn.
- Manager chỉ có thể đọc dashboard phòng ban hiện tại.
- Tổng hợp dashboard được thực hiện theo batch và không được query riêng cho mỗi User.
- Output dashboard nhóm User theo phòng ban và cung cấp cùng formula version cùng raw metric đã đóng băng như output KPI cá nhân.
- KPI là công cụ quản trị có thể giải thích. Hệ thống không được dùng KPI để tự động thăng chức, hạ chức, kỷ luật, sa thải hoặc đưa ra quyết định nhân sự khác.

## Điều chuyển nhân sự và KPI

Đây là vùng nghiệp vụ nhạy cảm nhất.

Hàng User hiện tại lưu trạng thái mới nhất:

- Phòng ban hiện tại.
- Role hiện tại.
- `JoinedUnitAt` hiện tại.

Trạng thái lịch sử được lưu trong `UserWorkHistories`:

- `UserId`
- `UnitId`
- `Role`
- `EffectiveFrom`
- `EffectiveTo`
- Lý do thay đổi và ngữ cảnh người thực hiện

Tính KPI phải dùng work history segment khi User đổi phòng ban hoặc role trong kỳ.

Quy tắc điều chuyển:

- Một lần điều chuyển phải cập nhật trạng thái User hiện tại và work-history segment trong cùng database transaction.
- Work-history segment của cùng User không được overlap.
- Điều chuyển đóng segment cũ ngay trước khi mở segment mới.
- Assignment snapshot Task hiện có không được tự động theo User sang phòng ban khác.
- Direct assignment đang chờ, báo cáo đã gửi và trách nhiệm Manager review phải được giải quyết trước khi chấp nhận điều chuyển.
- Xóa tài khoản bị từ chối khi còn trách nhiệm Task hoặc Review chưa hoàn thành.
- Xóa tài khoản đóng work-history segment đang hoạt động, xóa membership Unit hiện tại, thu hồi session và soft delete tài khoản trong cùng một serializable transaction.
- Assignment Task đã hoàn thành, Progress, Review, upload và lịch sử KPI không bị xóa cùng tài khoản.
- Mọi lần điều chuyển được chấp nhận phải ghi ai thay đổi, thời điểm có hiệu lực và lý do.

Ví dụ:

- Nếu User chuyển từ Phòng ban A sang Phòng ban B giữa tháng 7, KPI tháng 7 phải giải thích được theo hai segment.
- Nếu User trở thành Manager giữa một kỳ, KPI không được áp logic Manager cho toàn bộ kỳ một cách máy móc.
- Nếu kỳ được khóa trước một lần điều chuyển sau đó, `KpiResults` đã khóa phải giữ điểm và ngữ cảnh cũ.
- Manager chỉ có thể đọc KPI User khác khi snapshot đã khóa hoặc work-history segment của kỳ được chọn thuộc phòng ban hiện tại của Manager.
- Membership phòng ban hiện tại không được cấp quyền truy cập một kỳ lịch sử chỉ thuộc phòng ban khác.

## Soft delete và lưu trữ

- User, phòng ban, Task và comment dùng soft delete.
- Project dùng archive.
- Query filter mặc định ẩn record không hoạt động.
- Record lịch sử vẫn được giữ cho audit, KPI và báo cáo.
- Query assignment, Progress, Review, work history và KPI đã khóa trong lịch sử vẫn cố ý khả dụng sau khi User bị soft delete.

## Session xác thực

- Mỗi JWT mang `TokenVersion` hiện tại của User.
- Request đã xác thực chỉ được chấp nhận khi tài khoản vẫn tồn tại, đã duyệt, chưa bị xóa và JWT version khớp version trong database.
- Đổi mật khẩu, Admin đặt lại mật khẩu, đổi role, đổi phòng ban, từ chối tài khoản và xóa tài khoản làm mất hiệu lực toàn bộ JWT đã cấp trước đó.
- Thay đổi chỉ ở hồ sơ như full name, email hoặc phone number không làm mất hiệu lực session.
- JWT cũ được cấp trước khi hỗ trợ `TokenVersion` cố ý bị từ chối và User phải đăng nhập lại.
- Đăng ký, reset mật khẩu và đổi mật khẩu dùng chung một password policy và BCrypt hashing service.
- Đăng nhập thành công nâng cấp hash được tạo bằng BCrypt work factor cũ.
- Development có thể dùng JWT key tạm, còn mọi môi trường không phải Development phải cung cấp key từ bên ngoài.
- SignalR yêu cầu cùng cơ chế xác minh JWT session như HTTP endpoint.
- Tham gia task discussion group yêu cầu quyền hiện tại trên Task thông qua `ITaskAccessService`.

## Audit và lịch sử

Backend giữ hai loại lịch sử và không nhân đôi trách nhiệm của chúng:

- Bản ghi domain history (`TaskHistories`, `UserWorkHistories`, `Progresses`, `Reviews` và `KpiResults` đã khóa) giải thích kết quả workflow và KPI.
- `AuditLogs` ghi action tài khoản/cấu hình quan trọng cùng entity, action, actor, thời gian và JSON detail đã kiểm soát.

Quy tắc audit:

- Audit row là append-only qua public API; không có endpoint update hoặc delete.
- Duyệt/từ chối/xóa tài khoản, thay đổi/reset mật khẩu, thay đổi assignment nhân sự, thay đổi phòng ban, thay đổi Project và tạo/khóa kỳ KPI đều được audit.
- Audit record được thêm trong cùng database transaction với business mutation.
- Thao tác thất bại hoặc rollback không được để lại audit row thành công.
- Password, password hash, JWT, nội dung file và upload server path không bao giờ được ghi vào `DetailsJson`.
- Chỉ `Admin` có thể query `/api/audit-logs`.

## Quy tắc toàn vẹn database

Database constraint bảo vệ các invariant quan trọng:

- Username duy nhất.
- Employee code duy nhất.
- Tên phòng ban duy nhất.
- Tên Project duy nhất trong mỗi phòng ban.
- Hàng Task assignee duy nhất.
- Một Review cho mỗi báo cáo Progress.
- Khoảng percent Progress hợp lệ.
- Hours không âm.
- Khoảng ngày KPI hợp lệ.
- Counter và điểm KPI không âm.
- Khoảng ngày hiệu lực KPI hợp lệ.

Business service vẫn phải validation trước khi save để API response thân thiện với User.

## Quy tắc cho refactor tương lai

Khi refactor service, phải giữ các ranh giới sau:

- Controller tiếp tục mỏng và chỉ chuyển ngữ cảnh HTTP/auth thành lời gọi service.
- Kiểm tra quyền tiếp tục được tập trung thay vì lặp thủ công.
- Resolve assignment Task phải tách khỏi tạo Task.
- Xây dựng DTO phải tách khỏi logic business mutation.
- Resolve kỳ KPI, phân đoạn work history và tính điểm phải tách khỏi logic controller.
- Hợp đồng public API không thay đổi trừ khi frontend và tài liệu được cập nhật cùng lúc.
