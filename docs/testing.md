# Hướng dẫn kiểm thử

Backend có một test project xUnit:

```text
WorkManagementSystem.Tests/
```

Service test dùng EF Core InMemory khi hành vi quan hệ không liên quan. HTTP integration test khởi động entry point thật của ứng dụng qua `WebApplicationFactory<Program>` và chỉ thay database provider bằng database InMemory độc lập. SQL Server integration test bao phủ hành vi mà InMemory hoặc SQLite không thể chứng minh.

## Chạy kiểm thử

```powershell
dotnet restore .\WorkManagementSystem.sln
dotnet test .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
```

Khi thiếu `WMS_TEST_SQLSERVER_CONNECTION`, category SQL Server được báo là skip. Để chạy với test instance dùng một lần hoặc chuyên dụng:

```powershell
$env:WMS_TEST_SQLSERVER_CONNECTION = "Server=localhost,14333;Database=master;User Id=sa;Password=<test-password>;Encrypt=True;TrustServerCertificate=True"
dotnet test .\WorkManagementSystem.Tests\WorkManagementSystem.Tests.csproj --filter "Category=SqlServer"
Remove-Item Env:WMS_TEST_SQLSERVER_CONNECTION
```

Fixture tạo database có tên duy nhất, áp dụng mọi migration, chạy test rồi xóa database. Không bao giờ trỏ biến này vào tài khoản SQL Server production.

## Điều kiện kiểm tra trên CI

Repository có `.github/workflows/backend-ci.yml`. Workflow audit dependency NuGet trực tiếp và bắc cầu, xác minh format, build với warning là error, chạy unit/HTTP test, chạy category SQL Server trên database Compose, kiểm tra migration drift, publish artifact và xác minh toàn bộ Compose stack. Lỗi lấy dữ liệu audit và phát hiện lỗ hổng `NU1901` đến `NU1904` làm restore gate thất bại.

Phiên bản EF CLI được cố định trong `.config/dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

### Kiểm thử quan hệ với SQL Server

Suite `Category=SqlServer` chạy trên database có tên duy nhất và xác minh:

1. Mọi migration áp dụng được từ database rỗng.
2. Unique index từ chối username trùng.
3. Foreign key từ chối membership không hợp lệ.
4. Check constraint từ chối khoảng ngày KPI không hợp lệ.
5. Thao tác nhiều bước thất bại rollback thay đổi đã lưu.
6. SQL Server `rowversion` từ chối update cũ và ngăn lost update.
7. Tổng hợp workload translate trên SQL Server và giữ số query cố định với tập nhân viên lớn.
8. Constraint lịch sử capacity từ chối giá trị không dương và kỳ đang mở trùng.
9. Hai recurring-task worker cạnh tranh cùng một occurrence đến hạn chỉ tạo đúng một Task.
10. Recurring schedule tồn tại qua restart worker/context mà không lặp occurrence.
11. Lỗi sau khi SQL command đã chạy rollback đồng thời Task sinh ra, occurrence và cập nhật schedule.
12. Filtered unique index của reminder policy từ chối scope Unit trùng.
13. Hai deadline worker cạnh tranh cùng milestone tạo một scheduled event và một inbox notification.
14. Deadline event và inbox delivery tồn tại qua restart worker/context mà không lặp milestone.
15. Keyset pagination timeline Task translate trên SQL Server, không trùng event có cùng timestamp và giữ số query cố định theo page size.
16. Hai request Review đồng thời chỉ lưu đúng một quyết định, một lần cộng approved-hours và một tập transition history.
17. Projection và filter lịch sử trạng thái Progress translate trên SQL Server.
18. Làm giàu DTO danh sách Task nằm trong ngân sách chín command cho cả page 10 và 100 item trên bộ 300 Task.
19. Nâng cấp từ migration trước KPI insight giữ nguyên KPI snapshot và backfill field giải thích mới.

CI luôn cung cấp SQL connection string nên relational test bị skip không thể khiến pipeline xanh sai.

### Kiểm thử nhanh container khi chạy

CI thực hiện các kiểm tra database và runtime mà EF Core InMemory không thể bao phủ:

1. Khởi động SQL Server container sạch.
2. Chạy EF migration bundle không phải root trên database rỗng.
3. Chỉ khởi động API sau khi migration hoàn tất.
4. Chờ `/health/ready` và bắt buộc Docker container health status thành `healthy`.
5. Đăng nhập bằng Admin, Manager và User với JWT authentication thật.
6. Xác minh Admin đọc được kỳ KPI và Manager đọc được Project.
7. Xác minh User tạo Project nhận `403`, truy cập Project anonymous nhận `401`.
8. Xác minh lịch sử SQL migration đạt migration mới nhất và demo seed record tồn tại.
9. Backup kèm checksum, restore vào database tạm, so sánh record quan trọng và chạy `DBCC CHECKDB`.
10. Dừng SQL Server và xác minh liveness vẫn trả `200` trong khi readiness thành `503`.
11. Xóa container và disposable volume ngay cả khi một bước kiểm tra thất bại.

Relational gate này từng phát hiện migration cũ tham chiếu cột ngày Task chưa tồn tại trong database rỗng, một lỗi mà model-drift check và InMemory test không thể tái hiện.

Ngân sách query chi tiết được ghi trong [hiệu năng query](performance.md). Các bước phục hồi và khả năng quan sát background worker được ghi trong [phục hồi và worker](recovery-and-workers.md).

## Phạm vi test hiện tại

Chạy full suite để lấy số test hiện tại. Tài liệu cố ý không ghi cố định số lượng vì nó thay đổi mỗi khi thêm regression case.

### Auth

- Đăng ký tạo tài khoản pending.
- Password policy dùng chung cho đăng ký, reset và đổi mật khẩu.
- Mật khẩu được hash với BCrypt work factor đã cấu hình, hash cũ được nâng cấp sau đăng nhập.
- Username trùng bị chặn.
- User pending không thể đăng nhập.
- User đã duyệt nhận JWT token.
- Đổi `TokenVersion` làm mất hiệu lực JWT đã cấp trước đó.
- SignalR task group từ chối User đã xác thực nhưng không có quyền truy cập Task.

### Task Service

- Không phải Manager không thể tạo Task.
- Manager không có phòng ban không thể tạo Task.
- Manager không thể giao Task cho nhân sự ngoài phòng ban.
- Task không có assignee trực tiếp được giao cho phòng ban Manager.

### Workload và capacity

- Nhân viên không có Task đang hoạt động có workload bằng zero.
- Chỉ Task đang hoạt động giao với khoảng được chọn đóng góp remaining effort.
- Task đã duyệt bị loại; effort của Task nhiều assignee được chia đều.
- Biên threshold Busy/Overloaded xác định và có thể cấu hình.
- Thay đổi capacity bên trong khoảng ngày được tính tỷ lệ từ lịch sử có hiệu lực theo thời gian.
- Assignment preview trả workload dự kiến và không chặn tạo Task.
- Cấm Manager truy cập workload/capacity khác phòng ban.
- SQL Server xác minh translation tổng hợp workload và bảo vệ khỏi N+1 query.

### Recurring Task

- Interval hằng ngày và hằng tuần giữ nguyên thời gian UTC đã lên lịch.
- Schedule hằng tháng giới hạn ngày 29-31 về ngày hợp lệ cuối mà không mất ngày ưu tiên ở tháng sau.
- CRUD, phạm vi phòng ban, assignee mặc định rõ ràng, pause/resume và API authorization đều được bao phủ.
- Chạy lại và mô phỏng worker restart không sinh occurrence trùng.
- Catch-up bị giới hạn theo batch và giữ occurrence quá hạn còn lại cho lần chạy sau.
- Lần sinh thất bại không để lại Task, history, occurrence hoặc schedule đã tăng.
- SQL Server xác minh query translation, atomic rollback, hành vi restart đã lưu và an toàn khi hai worker cạnh tranh.

### Deadline reminder và escalation

- Task đến hạn sau 24 giờ tạo một due-soon notification và không lặp ở lần scan sau.
- Công việc quá hạn thông báo nhân viên; vượt ngưỡng escalation chỉ thông báo Manager trong phòng ban Task.
- Task hoàn thành ngay trước delivery suppress event đang chờ.
- Thay đổi policy trước milestone tương lai được lần scan sau sử dụng, gồm thứ tự Project trên Unit trên Global.
- Lỗi inbox tạm thời lưu retry state có giới hạn và thành công ở batch sau mà không nhân đôi event.
- Test restart InMemory xác minh persisted state; test SQL Server xác minh migration seed, filtered uniqueness, query translation, restart và worker cạnh tranh.

### Progress và Review

- Không thể hoàn thành Task yêu cầu review khi thiếu evidence.
- Progress một phần cập nhật trạng thái thành `InProgress`.
- Hoàn thành Task không cần review sẽ duyệt Progress và hoàn thành Task.
- Task nhiều assignee chỉ hoàn thành sau khi mọi nhân sự được giao có lần hoàn thành đã duyệt.
- Manager chấp thuận hoàn thành Task đã gửi.
- Từ chối completion giữ báo cáo Progress là `Rejected` và đưa Task về `InProgress`.
- Có thể gửi lại và duyệt completion đã sửa sau khi bị từ chối.
- Từ chối không có lý do thất bại mà không thay đổi trạng thái Task, Progress, Review hoặc history.
- Manager khác phòng ban không thể review báo cáo.
- Progress đã review không thể review lại.
- Ma trận policy rõ ràng chấp nhận transition được hỗ trợ và từ chối actor, scope, dependency hoặc terminal state không hợp lệ.
- Transition Task và Progress ghi report id liên quan cùng lý do quyết định.

### Activity timeline của Task

- Nguồn vòng đời Task chính được kết hợp theo thứ tự giảm dần xác định.
- Event cùng timestamp được phân trang không trùng hoặc mất id.
- Filter loại, actor và ngày UTC được cưỡng chế.
- Manager khác phòng ban không thể đọc timeline.
- Actor soft delete resolve thành snapshot lịch sử hoặc fallback an toàn.
- Field lịch sử Task ngoài allowlist không bị lộ qua metadata.
- Thay đổi trạng thái Progress chỉ hiển thị reason/status đã giới hạn và progress id liên quan.
- SQL Server xác minh cursor translation và số query cố định khi page size tăng.

### Upload

- File type không hợp lệ hoặc nguy hiểm bị chặn.
- File ZIP đổi tên thành `.docx` bị từ chối trừ khi có cấu trúc OOXML mong đợi.
- File OOXML chứa VBA macro payload bị từ chối.
- Tên file gốc được làm an toàn trước khi lưu metadata.
- Metadata file chỉ lưu sau khi file vật lý được chấp nhận.
- Database save thất bại sẽ dọn file vật lý.
- Storage key rooted hoặc traversal không thể download.
- File mồ côi đủ cũ được đối soát với storage key đã lưu, còn file mới được giữ.
- Download dùng metadata có authorization và không lộ server file path trong public DTO.

### KPI

- Luồng đọc KPI không tạo kỳ còn thiếu.
- Kỳ KPI được Admin tạo rõ ràng.
- Khoảng ngày không hợp lệ bị chặn.
- Kỳ KPI overlap bị chặn.
- Thay đổi phòng ban/role nhân sự được xử lý bằng work history khi tính KPI.
- KPI đã khóa lưu identity snapshot của nhân viên và phòng ban.
- Nhân viên lịch sử đã xóa vẫn thuộc kỳ KPI giao với lịch sử làm việc của họ.
- Raw metric và formula version KPI đã khóa giữ nguyên sau thay đổi dữ liệu nguồn Task hoặc danh tính.
- KPI rate xử lý denominator bằng zero theo cách xác định.
- Phạm vi dashboard Manager giới hạn trong phòng ban hiện tại; dashboard Admin là toàn tổ chức.
- SQL Server test xác minh aggregate KPI translate và giữ số query cố định với phòng ban lớn.
- Quyền Manager với lịch sử đi theo snapshot/history unit của kỳ được chọn thay vì Unit hiện tại của nhân viên.
- User không có Task nhận điểm trung lập dành cho người mới.
- Công việc được duyệt đúng hạn nhận bonus point.
- Nhiều Task quá hạn kích hoạt hành vi cảnh báo rủi ro.

### Database model

- Hàng Task assignee phải trỏ đúng một phía: User hoặc phòng ban.
- Kỳ KPI phải có khoảng ngày hợp lệ.
- Điểm và counter của KPI result không được âm.
- Khoảng ngày hiệu lực KPI result phải hợp lệ.
- Unique index quan trọng được cấu hình cho User, phòng ban, assignment, Project, kỳ KPI và KPI result.
- Quan hệ nghiệp vụ quan trọng tránh cascade delete ngoài ý muốn.

### Demo seed

- Demo seed mặc định tắt.
- Khi bật, nó tạo tài khoản demo Admin/Manager/User, phòng ban, Project, Task, Progress, membership và work history.
- Chạy seeder nhiều lần không nhân đôi demo dataset.

### DTO validation

- Empty GUID bị từ chối cho entity reference bắt buộc.
- Text field bắt buộc được validation trước khi đến logic service.
- Lỗi validation dùng hợp đồng lỗi API chuẩn.

### Hợp đồng API authorization

- Controller là API controller có route rõ ràng.
- Public endpoint giới hạn ở đăng ký, đăng nhập và tra cứu Unit công khai.
- Endpoint workflow Manager yêu cầu role `Manager`.
- Endpoint workflow Admin yêu cầu role `Admin`.
- Project board endpoint đã xóa tiếp tục không xuất hiện trên public API surface.

### Hợp đồng HTTP response

- Lỗi validation, authentication, authorization, not-found, conflict, rate-limit và server dùng cùng cấu trúc `application/problem+json`.
- Tạo tài nguyên trả về `201 Created`.
- Xóa và command không có response data trả về `204 No Content`.
- Pagination Task và Progress dùng hợp đồng typed `PagedResult<T>` nhưng giữ các JSON field `total`, `page`, `size` và `data`.
- Endpoint lịch sử Task trả DTO thay vì persistence entity.

### Pagination

- Page và size không hợp lệ quay về giá trị mặc định an toàn.
- Page size lớn bị giới hạn ở maximum dùng chung.
- Endpoint history có thể dùng default page size lớn hơn mà không bỏ qua maximum cap.

### Middleware vận hành và cancellation

- Correlation ID an toàn từ client được dùng lại trong request trace và response header.
- Correlation ID không an toàn bị từ chối và thay bằng server trace identifier.
- Request log đã xác thực chứa structured property `UserId`.
- Liveness tách khỏi readiness của database/upload, còn readiness xác minh cả hai dependency.
- Request bị client hủy không bị chuyển thành HTTP 500 sai.
- Cancellation đến được database query trong Auth.
- Ánh xạ DTO Task theo batch giữ assignee, upload và SubTask tách đúng theo Task.

### HTTP integration workflow

- `WebApplicationFactory<Program>` khởi động cùng ASP.NET Core middleware, authentication, authorization, routing và DI pipeline mà ứng dụng dùng.
- Chỉ `AppDbContext` được thay bằng provider InMemory độc lập để HTTP workflow test chạy nhanh.
- Đăng nhập dùng JWT authentication thật.
- Manager tạo Project.
- Manager tạo Task liên kết Project đó.
- User tải evidence lên.
- User gửi Progress 100 phần trăm.
- Manager duyệt báo cáo.
- Task thành `Approved`.
- Số lượng trạng thái Project được cập nhật.
- Endpoint KPI/performance đọc được ngữ cảnh công việc đã hoàn thành.
- User thông thường bị cấm tạo Project hoặc Task.
- Xóa nhân viên làm mất hiệu lực JWT hiện có của họ.
- Sau khi xóa và khóa kỳ, Manager lịch sử có quyền vẫn đọc được immutable KPI snapshot qua HTTP.

## Rủi ro regression được bao phủ

Suite bảo vệ các hành vi sau khỏi regression:

- Ranh giới quyền.
- Cách ly phòng ban.
- Transition trạng thái Task.
- Transition trạng thái Review.
- An toàn upload.
- Toàn vẹn kỳ KPI.
- Khả năng giải thích KPI khi dữ liệu nhân sự thay đổi theo thời gian.
- Database constraint ngăn trạng thái lưu không hợp lệ.
- Tính idempotent của seed data để demo có thể reset và lặp an toàn.
- Hợp đồng API request từ chối input xấu sớm.
- Hợp đồng authorization endpoint ngăn regression quyền ngoài ý muốn.
- Pagination guard ngăn query danh sách quá lớn ngoài ý muốn.
- Correlation và cancellation request giúp production log có thể hành động.
- HTTP integration test chứng minh workflow chính hoạt động xuyên controller, middleware, authentication, DI, service và EF Core context.
