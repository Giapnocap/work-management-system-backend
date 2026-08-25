# Bắt đầu sử dụng

Hướng dẫn này bao gồm quy trình clone sạch, thiết lập SQL Server cục bộ và cách chạy bằng Docker Compose.

## Điều kiện cần

Chọn một cách chạy:

- Cục bộ: Git, .NET 8 SDK và SQL Server mà máy host có thể kết nối.
- Container: Git và Docker Desktop có Compose v2.

Repository cố định SDK `8.0.400` trong `global.json` và cho phép feature band .NET 8 mới hơn. Kiểm tra SDK được chọn:

```powershell
dotnet --version
```

## Clone sạch

```powershell
git clone https://github.com/Giapnocap/work-management-system-backend.git
Set-Location .\work-management-system-backend
dotnet tool restore
dotnet restore .\WorkManagementSystem.sln
```

Không commit `bin/`, `obj/`, `TestResults/`, `Uploads/`, `logs/`, `.env` hoặc `appsettings.Local.json`. Đây là artifact cục bộ/runtime đã được `.gitignore` loại trừ.

## Chạy với SQL Server cục bộ

Tạo file cấu hình cục bộ đã bị Git bỏ qua:

```powershell
Copy-Item .\appsettings.Local.example.json .\appsettings.Local.json
```

Cập nhật `ConnectionStrings:Default` trong `appsettings.Local.json` nếu SQL Server instance mặc định không khả dụng. Sau đó lưu JWT key bên ngoài source control:

```powershell
dotnet user-secrets set "Jwt:Key" "replace-with-a-random-key-of-at-least-32-characters"
```

Khôi phục schema và chạy API:

```powershell
dotnet ef database update --project .\WorkManagementSystem.csproj
dotnet run --launch-profile https
```

Endpoint trong Development:

```text
Swagger:   https://localhost:7231/swagger
Liveness:  https://localhost:7231/health/live
Readiness: https://localhost:7231/health/ready
```

Swagger cố ý chỉ bật trong Development. Nếu không cấu hình development JWT key, ứng dụng dùng key tạm khi khởi động và token sẽ ngừng hoạt động sau khi process restart.

## Dữ liệu demo tùy chọn

Thiết lập nội dung sau trong `appsettings.Local.json` trước khi khởi động API:

```json
{
  "DemoSeed": {
    "Enabled": true,
    "ApplyMigrations": false
  }
}
```

Demo seed có tính idempotent và tạo các tài khoản Admin, Manager, User đã được duyệt. Tính năng này mặc định tắt và phải tiếp tục tắt trong production.

## Chạy với Docker Compose

Tạo file environment đã bị Git bỏ qua:

```powershell
Copy-Item .\.env.example .\.env
```

Thay cả hai secret placeholder trong `.env`. `MSSQL_SA_PASSWORD` phải đáp ứng độ phức tạp mật khẩu của SQL Server và `JWT_KEY` phải có ít nhất 32 ký tự. Có thể đặt `DEMO_SEED_ENABLED=true` để chạy workflow mẫu đã được tài liệu hóa.

Kiểm tra cấu hình và khởi động stack:

```powershell
docker compose config
docker compose up --build
```

Compose thực hiện lần lượt:

1. Khởi động SQL Server và chờ health check thành công.
2. Chạy EF Core migration bundle một lần.
3. Khởi động API container bằng user không phải root.
4. Lưu bền vững dữ liệu SQL, upload và log trong named volume.

Endpoint của container:

```text
API:       http://localhost:8080
Swagger:   http://localhost:8080/swagger
Liveness:  http://localhost:8080/health/live
Readiness: http://localhost:8080/health/ready
SQL:       localhost,14333
```

Các lệnh kiểm tra hữu ích:

```powershell
docker compose ps
docker compose logs migrate
docker compose logs api
Invoke-RestMethod http://localhost:8080/health/ready
```

Dừng container nhưng giữ dữ liệu:

```powershell
docker compose down
```

`docker compose down -v` cũng xóa volume SQL, upload và log. Chỉ dùng khi chủ động reset hoàn toàn môi trường cục bộ.

## Xác minh checkout sạch

```powershell
dotnet format .\WorkManagementSystem.sln --verify-no-changes --no-restore
dotnet build .\WorkManagementSystem.sln --configuration Release --no-restore -warnaserror `
  -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet test .\WorkManagementSystem.sln --configuration Release --no-build `
  -p:UseAppHost=false -p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --configuration Release --no-build
```

SQL Server integration test yêu cầu `WMS_TEST_SQLSERVER_CONNECTION`; nếu không có, chỉ category này bị skip ở máy cục bộ. CI cung cấp biến và bắt buộc relational test phải vượt qua.

## Lỗi khởi động thường gặp

- Không kết nối được SQL: kiểm tra server name, authentication mode, certificate setting và database service đang chạy.
- Không tìm thấy `dotnet ef`: chạy `dotnet tool restore` từ thư mục gốc repository.
- Cảnh báo HTTPS certificate: chạy `dotnet dev-certs https --trust` cho development cục bộ.
- Nhận `401` sau khi API restart: đăng nhập lại nếu Development đã dùng JWT key tạm.
- Readiness trả về `503`: kiểm tra cả kết nối SQL và quyền ghi vào `Uploads/`; liveness vẫn có thể khỏe khi dependency gặp sự cố.
