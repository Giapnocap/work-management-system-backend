# Hướng dẫn demo dự án

Hướng dẫn này trình bày backend hiện có trong 10 đến 15 phút. Quy trình không yêu cầu sửa trực tiếp database, chuẩn bị trước identifier hoặc sao chép JWT thủ công.

## Điều kiện cần

- Bắt đầu từ checkout sạch và làm theo [hướng dẫn khởi động](getting-started.md).
- Chạy Docker stack với `DEMO_SEED_ENABLED=true`.
- Giữ mật khẩu seed mặc định cục bộ là `Demo@123456`, hoặc truyền giá trị khác cho script.
- Dùng Windows PowerShell 5.1 hoặc PowerShell 7 có `curl.exe`.

Ví dụ thiết lập cục bộ:

```powershell
Copy-Item .env.example .env
$env:DEMO_SEED_ENABLED = "true"
docker compose up --detach --build --wait
```

Chạy toàn bộ workflow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-workflow.ps1
```

`-ExecutionPolicy Bypass` chỉ áp dụng cho process con này và không thay đổi policy toàn máy. Với PowerShell 7, dùng `pwsh -File .\scripts\demo-workflow.ps1`.

Với địa chỉ API hoặc seed password khác:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo-workflow.ps1 `
    -BaseUrl "https://localhost:7231" `
    -Password "Demo@123456"
```

Script tạo Project và Task có tên duy nhất, tải lên file evidence tạm, gửi Progress, duyệt báo cáo, rồi xác minh Task cuối cùng, timeline và KPI read model. File cục bộ tạm được xóa trong block `finally`. Chạy lại script không cần dọn dữ liệu trước.

## Lịch trình 10 đến 15 phút

| Thời gian | Nội dung demo | Điểm kỹ thuật |
| --- | --- | --- |
| 0:00-1:30 | Trình bày cấu trúc repository và `Program.cs` | Layered modular monolith với composition root rõ ràng, không tuyên bố là microservices hoặc full Clean Architecture. |
| 1:30-3:00 | Mở Swagger và đăng nhập bằng Manager, User | Kiểm tra role trong JWT là lớp biên ngoài; service tiếp tục kiểm tra tài nguyên và phạm vi phòng ban. |
| 3:00-5:00 | Tạo Project và giao Task | Chỉ Manager được tạo, phòng ban do server suy ra, DTO validation và semantics `201 Created`. |
| 5:00-7:00 | Tải evidence lên bằng User được giao | Authorization theo Task, kiểm tra MIME/signature, storage riêng tư và chống file mồ côi. |
| 7:00-9:30 | Gửi Progress 100% và duyệt | Workflow policy tập trung, yêu cầu evidence, transaction boundary, concurrency token và ràng buộc một review. |
| 9:30-11:00 | Đọc timeline của Task | Phân quyền theo phạm vi, cursor pagination ổn định, ghép từ dữ kiện sẵn có thay vì nhân đôi event data. |
| 11:00-12:30 | Trình bày tài liệu KPI và workload | Lịch sử nhân sự có hiệu lực theo thời gian, công thức giải thích được, snapshot đã khóa bất biến và workload tách khỏi điểm số. |
| 12:30-14:00 | Trình bày recurring/reminder worker và health endpoint | Lịch bền vững trong SQL, idempotency key, retry có giới hạn và semantics liveness/readiness riêng biệt. |
| 14:00-15:00 | Trình bày CI/test và giới hạn đã biết | Bằng chứng tái tạo được và non-goal minh bạch thay vì tuyên bố quy mô không có căn cứ. |

## Kết quả mong đợi

Output cuối gồm:

```text
Demo workflow passed.
TaskStatus    : Approved
ProgressStatus: Approved
```

## Điểm cần trình bày

- Project gom nhóm các Task liên quan nhưng không sao chép trạng thái workflow của Task.
- Admin cấu hình user và phòng ban; chỉ Manager giao công việc vận hành.
- User thông thường không thể tạo Project/Task hoặc review báo cáo.
- Task cần review không thể hoàn thành nếu thiếu evidence thuộc đúng Task.
- Database uniqueness và rowversion bảo vệ race condition khi review và scheduling.
- KPI là thông tin quản trị có thể giải thích, không phải quyết định nhân sự tự động.

## Xử lý sự cố

- Readiness thất bại nghĩa là SQL Server hoặc upload storage không sẵn sàng; kiểm tra `docker compose ps` và container log.
- Đăng nhập thất bại thường do demo seed bị tắt hoặc mật khẩu seed cấu hình khác.
- Xung đột port `8080` hoặc `14333` nghĩa là local stack khác đang chạy.
- Tránh lỗi script execution policy bằng command theo phạm vi process ở trên.
- Script cố ý dừng tại assertion thất bại đầu tiên để không trình bày một workflow chưa hoàn tất như đã thành công.

Dừng local stack nhưng giữ dữ liệu:

```powershell
docker compose down
```
