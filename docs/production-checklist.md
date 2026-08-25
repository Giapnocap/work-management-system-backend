# Checklist sẵn sàng cho production

Dùng checklist này trước khi triển khai backend ra ngoài môi trường development cục bộ.

## Cấu hình

- Đặt `ASPNETCORE_ENVIRONMENT=Production`.
- Giữ secret development trong .NET User Secrets.
- Giữ secret production trong environment variable hoặc secret store của nền tảng triển khai.
- Chỉ dùng `appsettings.Local.json` cho override Development riêng của máy và không chứa secret; ứng dụng không load file này trong Production.
- Không commit `appsettings.Local.json`.
- Cung cấp `Jwt:Key` dưới dạng secret mạnh bên ngoài source control; production sẽ không khởi động nếu thiếu.
- Giữ `Jwt:ExpirationMinutes` trong khoảng 5 đến 60 ở production. Backend không triển khai refresh token.
- Mật khẩu phải có ít nhất tám ký tự, một chữ hoa, một chữ thường và một chữ số, đồng thời không vượt quá giới hạn UTF-8 72 byte của BCrypt.
- Đặt `ConnectionStrings:Default` cho database SQL Server đích.
- Đặt `Cors:AllowedOrigins` thành URL frontend thực tế.
- Giới hạn `AllowedHosts`; production từ chối `*`.
- Chỉ dùng HTTPS origin trong cấu hình CORS production.
- Dùng kết nối SQL Server đã mã hóa và xác minh certificate. Production từ chối `Encrypt=False` và `TrustServerCertificate=True`.
- Giữ `DemoSeed:Enabled=false` trong production.
- Chỉ đặt `ReverseProxy:Enabled=true` khi chạy sau reverse proxy, đồng thời cấu hình ít nhất một địa chỉ `KnownProxies` chính xác hoặc CIDR `KnownNetworks`. Production từ chối cấu hình proxy đã bật nhưng không đáng tin cậy.

## Database

- Áp dụng EF Core migration trước khi chạy ứng dụng.
- Rà soát migration dọn dẹp có tính phá hủy trước khi áp dụng vào database chứa dữ liệu thật.
- Lưu thay đổi schema trong migration, không chạy từ code khởi động runtime.
- Chạy migration container hoặc `dotnet ef database update` như bước triển khai one-shot trước khi khởi động phiên bản API mới.
- Backup database trước khi áp dụng migration vào môi trường đang có dữ liệu.

## File runtime

- Không đưa `Uploads/` và `logs/` vào Git.
- Bảo đảm ứng dụng đã triển khai có quyền ghi vào thư mục upload và log.
- Backup file upload riêng nếu chúng quan trọng với lịch sử nghiệp vụ.
- Mount persistent storage cho cả `Uploads/` và `logs/` khi dùng container.
- Giữ upload volume riêng tư; chỉ cung cấp file qua download endpoint có authorization.
- Chỉ lưu `StorageKey` tương đối của upload. Giữ `UploadCleanup` hoạt động trừ khi đã có quy trình đối soát storage bền vững khác thay thế.
- Điều chỉnh `UploadCleanup:MinimumAgeHours` và `UploadCleanup:IntervalHours` thận trọng; cơ chế scan tích hợp chỉ xóa file đủ cũ và không có trong metadata upload đã lưu.
- Bổ sung antivirus hoặc sandbox scanner chuyên dụng trước khi nhận file trong hệ thống production truy cập từ Internet. Kiểm tra format và OOXML tích hợp chỉ là defense in depth, không phải phát hiện malware.

## Khả năng quan sát

- Giữ nguyên `X-Correlation-ID` qua reverse proxy và đưa nó vào báo cáo sự cố.
- Thu thập structured console log trên nền tảng triển khai.
- Cấu hình log level bằng `Serilog:MinimumLevel`; giữ nhiễu framework ở `Warning` trở lên trừ khi đang điều tra sự cố.
- Xem `CorrelationId` và `UserId` đã xác thực là structured property có thể tìm kiếm. Không thêm password, access token hoặc nội dung upload vào log scope.
- File log mặc định giữ 14 file theo ngày; điều chỉnh retention của nền tảng theo yêu cầu sự cố và quyền riêng tư.
- Giám sát `/health/live` cho process liveness. `/health/ready` xác minh cả kết nối database và quyền ghi vào private upload storage.
- Giữ liveness độc lập với sự cố database và storage để nền tảng không restart một process khỏe khi dependency gặp sự cố.
- Xử lý cancellation từ client riêng với lỗi server khi xem tỷ lệ lỗi.

## Container

- `compose.yml` dành cho development/demo cục bộ, không phải manifest triển khai production.
- Docker target `runtime` chạy bằng user `app` không phải root.
- Runtime image cung cấp Docker health check dựa trên `/health/ready`.
- Docker target `migrations` chứa EF migration bundle phụ thuộc framework, chạy không phải root, thực hiện `database update` rồi thoát mà không đóng gói SDK hoặc source tree.
- Chỉ cung cấp `MSSQL_SA_PASSWORD` và `JWT_KEY` qua `.env` cho Compose cục bộ; production phải dùng secret store của nền tảng.
- Kết thúc public HTTPS tại reverse proxy hoặc platform ingress đáng tin cậy rồi forward `X-Forwarded-For` và `X-Forwarded-Proto`; khai báo proxy đó rõ ràng trong cấu hình `ReverseProxy`.

## Continuous Integration

- Bắt buộc workflow `Backend CI` thành công trước khi merge.
- Giữ full transitive NuGet audit và xem lỗi lấy audit `NU1900` đến `NU1904` là lỗi chặn release khi restore.
- Giữ `dotnet-ef` cùng phiên bản với package EF Core.
- Không merge thay đổi model khi `has-pending-model-changes` thất bại.
- Bắt buộc SQL Server relational suite, disposable migration, container health check và JWT authorization smoke test thành công.
- Rà soát artifact được publish và kết quả container smoke của commit cần triển khai.

## Xác minh

```powershell
dotnet build .\WorkManagementSystem.sln --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet test .\WorkManagementSystem.sln --no-build -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
dotnet publish .\WorkManagementSystem.csproj --configuration Release --no-build --output .\artifacts\publish -p:UseAppHost=false
```

Kết quả mong đợi: build thành công và toàn bộ automated test vượt qua.
