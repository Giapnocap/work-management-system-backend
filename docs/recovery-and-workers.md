# Phục hồi và vận hành background worker

Lần diễn tập phục hồi gần nhất: 2026-08-24 trên SQL Server dùng một lần của Compose project `wms-phase8`.

Số bản ghi đã xác minh sau khi restore (`migrations|units|users|tasks|kpi`):

```text
47|1|4|4|0
```

## Diễn tập backup và restore

Diễn tập này dành cho database test và development. Quy trình thực hiện:

1. `BACKUP DATABASE` với `COPY_ONLY` và `CHECKSUM`.
2. `RESTORE VERIFYONLY` kèm xác minh checksum.
3. Restore vào database tạm có tên duy nhất bằng các logical file name đã phát hiện.
4. So sánh số bản ghi migration, phòng ban, user, task và KPI.
5. Chạy `DBCC CHECKDB` trên database đã restore.
6. Xóa database tạm và file backup trong block `finally`.

Khi Compose stack thông thường đang chạy:

```powershell
./scripts/backup-restore-drill.ps1
```

Với một Compose project độc lập:

```powershell
./scripts/backup-restore-drill.ps1 -ComposeProjectName "wms-recovery-test"
```

CI workflow chạy cùng script sau migration và demo seed. Backup được tạo bên trong SQL Server container và không bao giờ được lưu trong repository.

## Phạm vi kiểm chứng phục hồi migration

`SqlServerRelationalTests` bao phủ cả hai đường kiểm chứng được hỗ trợ:

- database rỗng lên migration mới nhất;
- từ `20260821020506_CentralizeTaskWorkflowStateMachine` lên mới nhất, đồng thời giữ nguyên KPI snapshot và kiểm tra backfill dữ liệu giải thích.

## Độ bền và khả năng quan sát của worker

Recurring schedule, generated occurrence key và scheduled deadline notification được lưu bền vững trong SQL Server. Các test restart chứng minh worker khởi động lại không làm mất lịch tiếp theo hoặc tạo trùng occurrence hay inbox notification đã hoàn tất.

`.NET` meter là `WorkManagementSystem.BackgroundJobs` và cung cấp:

- `workmanagement.background_job.executions`;
- `workmanagement.background_job.items`;
- `workmanagement.background_job.duration` theo millisecond.

Metric tag được cố ý giữ low-cardinality: `job.name`, `job.outcome`, `item.kind` và `item.outcome`. Resource ID không được dùng làm metric tag. Recurring template thất bại được log cùng `TemplateId`; reminder thất bại được log cùng `ScheduledNotificationId`, `TaskId`, `NotificationType`, `RetryCount` và `RetryStatePersisted`.

`/health/live` chỉ kiểm tra process liveness. `/health/ready` kiểm tra SQL Server và upload storage. Sự cố database phải khiến readiness thất bại nhưng không làm liveness thất bại.
