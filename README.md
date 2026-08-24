# Work Management System Backend

[![Backend CI](https://github.com/Giapnocap/work-management-system-backend/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/Giapnocap/work-management-system-backend/actions/workflows/backend-ci.yml)

Backend ASP.NET Core 8 cho hệ thống quản lý công việc theo phòng ban. Dự án tập trung vào phân quyền, quy trình giao việc và duyệt báo cáo, lịch sử nhân sự, KPI có thể giải thích, công việc định kỳ, nhắc hạn và tính toàn vẹn dữ liệu.

## Mục tiêu hệ thống

Hệ thống hỗ trợ luồng làm việc chính:

```text
Admin quản lý tài khoản/phòng ban
-> Trưởng phòng tạo dự án và công việc
-> Phân công nhân viên
-> Nhân viên báo cáo tiến độ và tải minh chứng
-> Trưởng phòng duyệt hoặc từ chối báo cáo
-> Cập nhật trạng thái, lịch sử, thông báo và KPI
```

Dự án không triển khai nghiệp vụ thương mại điện tử, microservices, message broker hay hạ tầng cloud. Mục tiêu là hoàn thiện một backend nguyên khối có cấu trúc rõ ràng, kiểm soát nghiệp vụ và có thể chạy lặp lại trong môi trường local/CI.

## Công nghệ sử dụng

- C# và .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8 và SQL Server
- JWT Bearer Authentication
- Role-based Authorization
- SignalR cho cập nhật thảo luận theo thời gian thực
- Serilog và correlation ID
- Swagger/OpenAPI
- xUnit, EF Core InMemory và `WebApplicationFactory`
- Docker, Docker Compose và EF Core migration bundle
- GitHub Actions

## Kiến trúc hiện tại

Repository sử dụng mô hình **Layered Modular Monolith**. `API`, `Application`, `Domain` và `Infrastructure` là các ranh giới logic trong cùng một runtime project, không phải các service triển khai độc lập.

```text
Client -> API -> Application -> Domain
                    ^             ^
                    |             |
                Infrastructure ---+

Program.cs là composition root.
```

- `API`: controller, middleware, authentication, Swagger, health check và SignalR hub.
- `Application`: DTO, interface, validation, service nghiệp vụ và quy tắc truy cập.
- `Domain`: entity, enum và policy chuyển trạng thái.
- `Infrastructure`: EF Core, cấu hình database, bảo mật, lưu tệp, background worker và seed demo.
- `Migrations`: lịch sử thay đổi schema SQL Server.
- `WorkManagementSystem.Tests`: unit test, HTTP integration test và SQL Server integration test.

Architecture test ngăn `Application` tham chiếu ngược tới `API`/`Infrastructure` và ngăn controller truy cập trực tiếp EF Core. Application vẫn sử dụng abstraction `IAppDbContext`, vì vậy dự án không tự nhận là Clean Architecture hoàn chỉnh.

Xem thêm [tài liệu kiến trúc](docs/architecture.md).

## Vai trò và phạm vi quyền

### Admin

- Duyệt hoặc từ chối tài khoản đăng ký đang chờ.
- Quản lý người dùng, phòng ban, điều chuyển nhân sự và trạng thái tài khoản.
- Quản lý kỳ KPI, xem dữ liệu toàn hệ thống và audit log.
- Không tham gia luồng tạo dự án, giao việc hoặc duyệt báo cáo công việc.

### Manager

- Tạo và lưu trữ dự án thuộc phòng ban hiện tại.
- Tạo, cập nhật, phân công và quản lý công việc trong phòng ban.
- Tạo lịch công việc định kỳ và quản lý chính sách nhắc hạn trong phạm vi được phép.
- Duyệt hoặc từ chối báo cáo tiến độ của công việc thuộc phòng ban.
- Xem workload, capacity và KPI đúng phạm vi phòng ban hiện tại/lịch sử được phép.

### User

- Xem công việc được giao trực tiếp hoặc giao chung cho phòng ban.
- Cập nhật tiến độ, tải minh chứng, gửi báo cáo hoàn thành.
- Bình luận, reaction, seen, xem timeline và thông báo trong phạm vi công việc được truy cập.
- Xem KPI cá nhân theo kỳ.

Quyền được kiểm tra tại controller và tại service nghiệp vụ quan trọng. JWT còn được đối chiếu với trạng thái tài khoản, vai trò và `TokenVersion` trong database để thu hồi phiên khi tài khoản thay đổi.

## Chức năng chính

- Đăng ký, đăng nhập, duyệt tài khoản, đặt lại/đổi mật khẩu và thu hồi phiên.
- Quản lý phòng ban, thành viên, điều chuyển và lịch sử làm việc.
- Quản lý dự án theo phòng ban.
- Quản lý công việc, công việc con, người được giao và phụ thuộc giữa các công việc.
- Báo cáo tiến độ, tải minh chứng, gửi duyệt và review một lần.
- Bình luận, reaction, seen, notification và timeline hoạt động.
- Lịch công việc định kỳ theo ngày, tuần hoặc tháng.
- Nhắc trước hạn, cảnh báo quá hạn và chống gửi trùng sau khi worker khởi động lại.
- Dự báo workload và capacity theo khoảng thời gian.
- Kỳ KPI, kết quả cá nhân/phòng ban, snapshot khi khóa kỳ và dữ liệu giải thích điểm.
- Export dữ liệu quản lý.
- Audit log, health check và structured logging.

## Quy trình công việc

Trạng thái công việc và báo cáo được kiểm soát bởi domain workflow policy, không cập nhật tùy ý từ controller.

Các nguyên tắc chính:

- Chỉ Trưởng phòng được tạo dự án và công việc trong phòng ban của mình.
- Công việc mới bắt đầu ở trạng thái `NotStarted`.
- Nhân viên chỉ báo cáo công việc được giao và còn hợp lệ.
- Công việc phụ thuộc không thể hoàn thành trước công việc tiên quyết.
- Báo cáo 100% phải có metadata minh chứng khi công việc yêu cầu review.
- Báo cáo đã gửi chỉ được review một lần.
- Từ chối báo cáo bắt buộc có lý do và cho phép nhân viên sửa, gửi lại.
- Công việc đã `Approved` không được cập nhật tiến độ hoặc chuyển trạng thái ngược trái phép.
- Khi có nhiều người được giao, hệ thống chỉ hoàn thành công việc sau khi đủ điều kiện của toàn bộ tập người thực hiện dự kiến.
- Transaction, rowversion và unique constraint bảo vệ các luồng review đồng thời.

Chi tiết nằm trong [business rules](docs/business-rules.md) và [sơ đồ workflow](docs/domain-workflows.md).

## Database

Mô hình dữ liệu được cấu hình bằng EF Core trong `Infrastructure/Data` và thay đổi schema qua migration.

Các cơ chế bảo vệ dữ liệu đáng chú ý:

- Unique index cho username, mã nhân viên, tên phòng ban, dự án trong phòng ban, assignment, review, kết quả KPI, occurrence định kỳ và event nhắc hạn.
- Composite foreign key giữ công việc và dự án cùng phòng ban, đồng thời giữ file báo cáo đúng công việc.
- Check constraint cho phần trăm tiến độ, ngày kỳ KPI, số liệu KPI, planned effort và cấu trúc reminder policy.
- Rowversion chống lost update ở các aggregate có chỉnh sửa đồng thời.
- Soft delete ẩn dữ liệu vận hành nhưng vẫn giữ assignment, tiến độ, lịch sử nhân sự và snapshot KPI phục vụ truy vết.
- Index phục vụ phân trang timeline, task list, workload và KPI query.

Xem [tài liệu database](docs/database.md).

## Authentication và Authorization

- Mật khẩu được hash bằng BCrypt và áp dụng giới hạn 72 byte của BCrypt.
- JWT chứa định danh, vai trò và phiên bản token.
- Mỗi request có token được kiểm tra lại với tài khoản hiện tại trong database.
- Xóa, khóa, đổi vai trò hoặc thay đổi bảo mật sẽ làm mất hiệu lực token cũ khi phù hợp.
- Endpoint công khai chỉ gồm đăng ký, đăng nhập và danh sách phòng ban phục vụ đăng ký.
- Endpoint quản trị dùng role policy; service tiếp tục kiểm tra phạm vi phòng ban/công việc.
- Lỗi API dùng `ProblemDetails` thống nhất và có `traceId`.

## File upload

Tệp được lưu ngoài thư mục public và chỉ tải xuống qua endpoint có kiểm tra quyền công việc.

Validation hiện có:

- Giới hạn kích thước và loại phần mở rộng.
- Kiểm tra MIME và chữ ký tệp thực tế.
- Kiểm tra cấu trúc gói OOXML, chặn macro và nội dung archive nguy hiểm.
- Chuẩn hóa tên lưu trữ, chống path traversal và giới hạn đường dẫn trong upload root.
- Bắt buộc `taskId`/`progressId` liên kết đúng ngữ cảnh.
- Xóa tệp vật lý nếu lưu database thất bại và có worker dọn tệp mồ côi.

Đây là lớp phòng vệ ứng dụng, không thay thế antivirus chuyên dụng.

## KPI

KPI được dùng làm thông tin hỗ trợ quản lý, không tự động đưa ra quyết định nhân sự.

- Chỉ dữ liệu công việc/báo cáo hợp lệ trong kỳ được tính.
- Điểm cá nhân bắt đầu từ mốc cơ sở, cộng thưởng cho công việc được duyệt/đúng hạn và trừ điểm theo quy tắc quá hạn hoặc báo cáo bị từ chối.
- KPI Trưởng phòng kết hợp kết quả phòng ban và kết quả cá nhân theo công thức hiện tại.
- Khi khóa kỳ, hệ thống lưu kết quả, thông tin nhân sự/phòng ban, raw metrics và phiên bản công thức thành snapshot.
- Thay đổi hồ sơ, phòng ban, chức vụ hoặc dữ liệu vận hành sau khi khóa không viết lại lịch sử KPI.
- Work history được dùng để giải thích kết quả khi nhân viên điều chuyển giữa các kỳ.
- `PlannedEffortHours` phục vụ dự báo workload, không tham gia điểm KPI.

Quy tắc KPI là chính sách riêng của dự án và cần được đối chiếu với quy định thực tế trước khi dùng trong tổ chức thật.

## Recurring task và reminder

- Trưởng phòng có thể tạo lịch ngày, tuần hoặc tháng cho phòng ban của mình.
- Mỗi occurrence tạo một công việc bình thường rồi đi qua workflow hiện có.
- Lịch và trạng thái worker được lưu trong SQL Server.
- Khóa `(TemplateId, ScheduledForUtc)` cùng transaction serializable chống tạo occurrence trùng khi nhiều worker chạy đồng thời.
- Reminder policy có thứ tự ưu tiên `Project > Unit > Global`.
- Event key và unique constraint chống gửi lặp mốc nhắc hạn/cảnh báo quá hạn.
- Công việc đã hoàn thành hoặc bị xóa sẽ hủy/suppress event không còn phù hợp.

## Cấu hình local

Yêu cầu:

- .NET 8 SDK. `global.json` ghim feature band .NET 8 phù hợp.
- SQL Server local hoặc Docker Desktop với Compose v2.
- EF Core CLI được khai báo trong tool manifest của repository.

Tạo cấu hình local:

```powershell
Copy-Item .\appsettings.Local.example.json .\appsettings.Local.json
dotnet user-secrets set "Jwt:Key" "<chuỗi-ngẫu-nhiên-tối-thiểu-32-ký-tự>"
```

`appsettings.Local.json`, `.env`, log, upload, `bin`, `obj` và test artifact đã được loại khỏi Git. Không đặt secret thật trong `appsettings.json`.

## Chạy bằng .NET CLI

```powershell
dotnet tool restore
dotnet restore .\WorkManagementSystem.sln
dotnet ef database update --project .\WorkManagementSystem.csproj
dotnet run --launch-profile https
```

Swagger local:

```text
https://localhost:7231/swagger
```

Xem hướng dẫn chi tiết tại [getting started](docs/getting-started.md).

## Chạy bằng Docker

```powershell
Copy-Item .env.example .env
```

Thay `MSSQL_SA_PASSWORD` và `JWT_KEY` trong `.env`, sau đó chạy:

```powershell
docker compose config
docker compose up --build
```

Endpoint mặc định:

```text
API:     http://localhost:8080
Swagger: http://localhost:8080/swagger
SQL:     localhost,14333
```

Dừng stack nhưng giữ dữ liệu:

```powershell
docker compose down
```

`docker compose down --volumes` sẽ xóa dữ liệu SQL, upload và log của stack; chỉ dùng khi chủ động reset toàn bộ môi trường local.

## Dữ liệu demo tùy chọn

Demo seed mặc định tắt. Có thể bật bằng `DemoSeed:Enabled=true` hoặc `DEMO_SEED_ENABLED=true` trong môi trường Docker.

Tài khoản demo:

- `demo.admin`
- `demo.manager`
- `demo.employee1`
- `demo.employee2`

Mật khẩu demo chung: `Demo@123456`.

Seeder có tính idempotent và nhận diện cả tên dữ liệu demo của phiên bản cũ, vì vậy chạy lại không tạo trùng người dùng, phòng ban, dự án, công việc hoặc membership.

Chạy demo workflow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-workflow.ps1
```

Script đăng nhập Trưởng phòng/Nhân viên, tạo dự án và công việc, tải minh chứng, gửi báo cáo, duyệt và xác minh trạng thái/timeline/KPI.

## Testing

Test không phụ thuộc SQL Server:

```powershell
dotnet test .\WorkManagementSystem.sln `
  --configuration Release `
  --filter "Category!=SqlServer"
```

Test quan hệ trên SQL Server test riêng:

```powershell
$env:WMS_TEST_SQLSERVER_CONNECTION = "Server=localhost,14333;Database=master;User Id=sa;Password=<test-password>;Encrypt=True;TrustServerCertificate=True"
dotnet test .\WorkManagementSystem.Tests\WorkManagementSystem.Tests.csproj `
  --configuration Release `
  --filter "Category=SqlServer"
Remove-Item Env:WMS_TEST_SQLSERVER_CONNECTION
```

SQL test tạo database có tên ngẫu nhiên, áp migration, chạy kiểm tra rồi tự xóa database. Phạm vi kiểm thử gồm:

- Authentication, authorization và thu hồi token.
- Ranh giới phòng ban, task access và upload access.
- Task, dependency, progress, review và KPI workflow.
- Điều chuyển/xóa nhân sự nhưng vẫn giữ lịch sử.
- Upload validation và cleanup.
- Recurring/reminder idempotency khi worker chạy đồng thời hoặc khởi động lại.
- Unique/foreign-key/check constraint, transaction rollback và optimistic concurrency.
- Query translation và query budget cho task list, timeline, workload và KPI.
- HTTP integration test chạy qua pipeline thật của `Program.cs`.

Vòng xác minh gần nhất của repository này đã pass `384` test thường và `24` SQL Server integration test.

Xem [testing guide](docs/testing.md).

## Continuous Integration

Workflow `.github/workflows/backend-ci.yml` chạy khi push và pull request:

- Restore tool/dependency và audit NuGet.
- Kiểm tra format.
- Build Release với warning được xem là lỗi.
- Chạy test thường và test SQL Server.
- Kiểm tra model không thiếu migration.
- Publish artifact.
- Validate/build Docker image API và migration bundle.
- Khởi động stack từ database rỗng, chạy migration và demo seed.
- Kiểm tra health, đăng nhập và role authorization qua HTTP.
- Backup có checksum, restore vào database tạm, đối chiếu dữ liệu và chạy `DBCC CHECKDB`.
- Lưu test report/artifact và luôn dọn stack CI.

## Tài liệu

- [Kiến trúc](docs/architecture.md)
- [Bắt đầu và cấu hình local](docs/getting-started.md)
- [Quy tắc nghiệp vụ](docs/business-rules.md)
- [Sơ đồ workflow](docs/domain-workflows.md)
- [Database](docs/database.md)
- [API workflow mẫu](docs/api-workflow.md)
- [Demo portfolio](docs/demo-guide.md)
- [Bằng chứng portfolio và gợi ý phỏng vấn](docs/portfolio-evidence.md)
- [API error contract](docs/api-errors.md)
- [Testing](docs/testing.md)
- [Query performance](docs/performance.md)
- [Recovery và background worker](docs/recovery-and-workers.md)
- [Checklist production](docs/production-checklist.md)

## Giới hạn hiện tại

- Các layer cùng biên dịch thành một deployable assembly, không phải microservices.
- Không có refresh token; hệ thống dùng access token ngắn hạn và `TokenVersion` để thu hồi phiên.
- Chỉ hỗ trợ SQL Server.
- Upload dùng filesystem/volume riêng, chưa có object storage hoặc antivirus engine.
- SignalR chạy in-process và best effort, không có distributed backplane hay transactional outbox.
- API chưa có versioning.
- Không có production CD hoặc mô tả hạ tầng cloud.
- KPI cần được xác nhận lại với chính sách của tổ chức trước khi áp dụng thực tế.

## Lệnh kiểm tra trước khi commit

```powershell
dotnet tool restore
dotnet restore .\WorkManagementSystem.sln
dotnet format .\WorkManagementSystem.sln --verify-no-changes --no-restore
dotnet build .\WorkManagementSystem.sln --configuration Release --no-restore --warnaserror
dotnet test .\WorkManagementSystem.sln --configuration Release --no-build --filter "Category!=SqlServer"
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
docker compose config --quiet
```
