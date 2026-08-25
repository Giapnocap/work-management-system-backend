# Bằng chứng hiệu năng truy vấn

Lần xác minh gần nhất: 2026-08-24 trên SQL Server 2022.

Bộ integration test quan hệ áp đặt ngân sách số query cho các luồng đọc chính. Một reader command bao gồm cả query authorization và làm giàu DTO. Ngân sách cố ý dựa trên số SQL command thay vì độ trễ thực tế, vì CI và máy của developer không phải môi trường benchmark ổn định.

| Luồng đọc | Dữ liệu test | Ngân sách reader command |
| --- | ---: | ---: |
| `GET /api/tasks` | 30 nhân viên, 300 Task, assignee, SubTask, file và dependency | 9 |
| `GET /api/management/workload` | 30 nhân viên có effort được giao | 4 |
| `GET /api/tasks/{id}/timeline` | 40 comment trên timeline và một lần chuyển workflow | 5 |
| `GET /api/kpi-periods/{id}/dashboard` | 30 nhân viên có Task và báo cáo đã duyệt | 11 |

Các test danh sách Task và Timeline chạy cả page nhỏ lẫn lớn từ trạng thái tracking sạch tương đương. Số command phải giữ nguyên khi page size tăng. Workload và KPI dùng aggregate theo tập hợp và được kiểm tra trên phòng ban lớn hơn.

Chạy các test bằng chứng với SQL Server dùng một lần:

```powershell
$env:WMS_TEST_SQLSERVER_CONNECTION = "Server=localhost,14333;Database=master;User Id=sa;Password=<password>;Encrypt=False;TrustServerCertificate=True"
dotnet test WorkManagementSystem.Tests/WorkManagementSystem.Tests.csproj -c Release --filter "Category=SqlServer"
```

## Rà soát index

Các pattern query đã đo sử dụng index hiện có trên phạm vi Task, project/status, assignee target/task, nguồn timeline/task/thời gian, trạng thái scheduled notification, recurring schedule, KPI period/user và foreign key của Task con. DTO builder thực hiện một tập query batch cố định và không query bên trong vòng lặp ánh xạ từng Task.

Không có index nào được thêm trong giai đoạn này. Số command đo được là hằng số, mọi query mục tiêu đều translate trên SQL Server, và không có scan hoặc sort cụ thể nào chứng minh cần thêm index có chi phí ghi. Chỉ thêm index sau khi execution plan gần với production chỉ ra bottleneck thật.

Không bổ sung cache hoặc latency SLA. Cache sẽ tạo thêm độ phức tạp invalidation trước khi có bottleneck độ trễ được đo; còn latency threshold trên CI runner không ổn định sẽ tạo ra lỗi sai lệch.
