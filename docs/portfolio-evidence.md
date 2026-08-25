# Bằng chứng portfolio và hướng dẫn phỏng vấn

Tài liệu này liên kết các tuyên bố về repository với bằng chứng code và test. Đây là tài liệu hỗ trợ chuẩn bị, không thay thế việc hiểu implementation.

## Định vị dự án

WorkManagementSystem là layered modular monolith cho workflow theo phạm vi phòng ban, cộng tác và khả năng quan sát nguồn lực. Trọng tâm kỹ thuật gồm authorization theo tài nguyên, dependency Task, transition Progress/Review, workload capacity, scheduling bền vững, reminder, activity history và KPI snapshot có thể giải thích.

Backend thương mại điện tử thường tập trung vào catalog, inventory, cart, order, payment và tính nhất quán fulfillment. Đây không phải domain của repository này. WorkManagementSystem cũng không tuyên bố có broker-backed messaging, distributed transaction, microservices hoặc layer triển khai độc lập.

## Nội dung gợi ý cho CV

**Work Management System Backend | ASP.NET Core 8, EF Core, SQL Server, xUnit, Docker**

- Xây dựng Web API theo kiến trúc layered modular monolith cho việc giao Project/Task theo phòng ban, báo cáo Progress có evidence, Manager review, activity timeline và KPI insight.
- Cưỡng chế authorization theo role và tài nguyên dựa trên membership phòng ban hiện tại, assignment Task và phạm vi KPI lịch sử, với JWT session có thể thu hồi.
- Triển khai workflow Task rõ ràng và dependency DAG có phát hiện cycle, transition history, optimistic concurrency cùng relational uniqueness constraint.
- Xây dựng recurring-task và deadline-reminder worker lưu trạng thái trong SQL với idempotency key, trạng thái an toàn qua restart, retry có giới hạn, health check và operational metric.
- Bổ sung query workload/KPI theo tập hợp, ngân sách SQL command tái tạo được, unit/API/SQL Server integration test, xác minh Docker migration và diễn tập backup/restore trong CI.

Chỉ giữ những bullet bạn có thể giải thích từ request đến database. Không thêm tuyên bố throughput, latency, availability hoặc phần trăm cải thiện nếu không có benchmark tái tạo được và kết quả được lưu lại.

## Bằng chứng cho từng tuyên bố

| Tuyên bố trong CV | Bằng chứng implementation | Bằng chứng test |
| --- | --- | --- |
| Layered modular monolith với ranh giới HTTP/application/data rõ ràng | [Program composition root](../Program.cs), [đăng ký Application](../Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs), [đăng ký Infrastructure](../Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs), [hướng dẫn kiến trúc](architecture.md) | [architecture dependency test](../WorkManagementSystem.Tests/ArchitectureDependencyTests.cs), [API contract test](../WorkManagementSystem.Tests/ApiContractIntegrationTests.cs) |
| Authorization theo role và phạm vi tài nguyên | [task access service](../Application/Services/TaskAccessService.cs), [current user service](../API/Authentication/CurrentUserService.cs), [ma trận quy tắc nghiệp vụ](business-rules.md) | [task access security test](../WorkManagementSystem.Tests/TaskAccessSecurityTests.cs), [authorization contract test](../WorkManagementSystem.Tests/ApiAuthorizationContractTests.cs), [workflow HTTP test](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs) |
| Workflow Task/Progress/Review rõ ràng có bảo vệ concurrency | [workflow policy](../Domain/Workflows/TaskWorkflowPolicy.cs), [workflow service](../Application/Services/TaskWorkflowService.cs), [review service](../Application/Services/ReviewService.cs), [transaction manager](../Infrastructure/Data/EfTransactionManager.cs) | [workflow policy test](../WorkManagementSystem.Tests/TaskWorkflowPolicyTests.cs), [progress/review test](../WorkManagementSystem.Tests/ProgressReviewServiceTests.cs), [SQL Server relational test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs) |
| Dependency Task dạng DAG có từ chối cycle | [dependency service](../Application/Services/TaskDependencyService.cs), [dependency entity](../Domain/Entities/TaskDependency.cs), [dependency configuration](../Infrastructure/Data/Configurations/TaskDependencyConfiguration.cs) | [dependency service test](../WorkManagementSystem.Tests/TaskDependencyServiceTests.cs), [HTTP workflow test](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs), [database model test](../WorkManagementSystem.Tests/DatabaseModelTests.cs) |
| Recurring Task và deadline reminder bền vững | [recurring scheduler](../Application/Services/RecurringTaskSchedulerService.cs), [recurring worker](../Infrastructure/Scheduling/RecurringTaskWorker.cs), [deadline service](../Application/Services/DeadlineReminderService.cs), [deadline worker](../Infrastructure/Scheduling/DeadlineReminderWorker.cs) | [recurring scheduler test](../WorkManagementSystem.Tests/RecurringTaskSchedulerTests.cs), [deadline test](../WorkManagementSystem.Tests/DeadlineReminderServiceTests.cs), [worker metric test](../WorkManagementSystem.Tests/BackgroundJobMetricsTests.cs), [SQL Server relational test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs) |
| Workload planning và KPI snapshot có thể giải thích | [workload service](../Application/Services/WorkloadService.cs), [performance service](../Application/Services/UserPerformanceService.cs), [KPI formula](../Application/Common/KpiFormula.cs), [KPI service](../Application/Services/KpiService.cs) | [workload test](../WorkManagementSystem.Tests/WorkloadServiceTests.cs), [KPI test](../WorkManagementSystem.Tests/KpiServiceTests.cs), [staff-history KPI test](../WorkManagementSystem.Tests/UserKpiWorkHistoryTests.cs), [query budget](performance.md) |
| Upload evidence theo Task đã được gia cố | [upload service](../Application/Services/UploadService.cs), [upload controller](../API/Controllers/UploadController.cs), [upload configuration](../Infrastructure/Data/Configurations/UploadFileConfiguration.cs) | [upload test](../WorkManagementSystem.Tests/UploadServiceTests.cs), [orphan cleanup test](../WorkManagementSystem.Tests/UploadOrphanCleanupTests.cs), [workflow HTTP test](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs) |
| Migration có thể lặp lại, xác minh phục hồi và operational health | [Compose stack](../compose.yml), [CI workflow](../.github/workflows/backend-ci.yml), [diễn tập backup/restore](../scripts/backup-restore-drill.ps1), [health check](../Infrastructure/Health/DatabaseHealthCheck.cs) | [operational test](../WorkManagementSystem.Tests/OperationalObservabilityTests.cs), [migration và constraint test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs), [hướng dẫn phục hồi](recovery-and-workers.md) |

## Câu hỏi và trả lời phỏng vấn

### 1. Đây có phải Clean Architecture không?

Không. Đây là layered modular monolith với namespace logic API, Application, Domain và Infrastructure trong một runtime assembly. Architecture test bảo vệ các dependency boundary hữu ích, nhưng Application vẫn cung cấp abstraction query hướng EF nên tuyên bố persistence ignorance hoàn toàn sẽ không chính xác. Bằng chứng: [hướng dẫn kiến trúc](architecture.md) và [architecture test](../WorkManagementSystem.Tests/ArchitectureDependencyTests.cs).

### 2. Tại sao giữ controller mỏng?

Controller chuyển HTTP input, resolve User đã xác thực, gọi một application use case và chọn status code. Authorization cho Task/Project/report cụ thể cùng thay đổi nghiệp vụ trong transaction vẫn nằm ở service. Bằng chứng: [task controller](../API/Controllers/TaskController.cs) và [task service](../Application/Services/TaskService.cs).

### 3. Tại sao role authorization chưa đủ?

Hai Manager không được quản lý phòng ban của nhau. Role tại endpoint từ chối sai loại actor; sau đó `TaskAccessService` kiểm tra phòng ban hiện tại, creator/assignment, management scope và lịch sử được phép. Bằng chứng: [task access service](../Application/Services/TaskAccessService.cs) và [security test](../WorkManagementSystem.Tests/TaskAccessSecurityTests.cs).

### 4. JWT bị thu hồi được từ chối trước khi hết hạn như thế nào?

Token chứa version gắn với User record. Reset mật khẩu, thay đổi tài khoản nhạy cảm với role/unit hoặc xóa làm đổi `TokenVersion`; current-user resolution từ chối version cũ. Bằng chứng: [current user service](../API/Authentication/CurrentUserService.cs) và [security operation test](../WorkManagementSystem.Tests/SecurityOperationsTests.cs).

### 5. Tại sao tập trung transition Task?

Tạo Progress, Review và các use case khác không được tự tạo cách đổi trạng thái riêng. `TaskWorkflowPolicy` định nghĩa transition hợp lệ, còn `TaskWorkflowService` áp dụng thay đổi trạng thái và history nhất quán. Bằng chứng: [sơ đồ workflow](task-workflow.md) và [policy test](../WorkManagementSystem.Tests/TaskWorkflowPolicyTests.cs).

### 6. Review trùng được ngăn như thế nào?

Service kiểm tra trạng thái report và Review hiện tại rồi update trong transaction. Rowversion phát hiện write cũ và unique database constraint trên Progress Review là lớp bảo vệ cuối cho race condition. Bằng chứng: [review service](../Application/Services/ReviewService.cs), [progress configuration](../Infrastructure/Data/Configurations/ProgressConfiguration.cs) và [SQL test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 7. Tại sao validation dependency cần nhiều hơn foreign key?

Foreign key chứng minh cả hai Task tồn tại nhưng không chứng minh graph không có cycle. Service tìm path hiện có trước khi thêm cạnh; SQL constraint độc lập từ chối self-reference và cạnh trùng. Bằng chứng: [dependency service](../Application/Services/TaskDependencyService.cs) và [dependency test](../WorkManagementSystem.Tests/TaskDependencyServiceTests.cs).

### 8. Điều gì xảy ra khi Task đang chặn hoàn thành?

Task phụ thuộc không tự động hoàn thành; nó chỉ đủ điều kiện đi tiếp trong workflow Progress thông thường. Dữ kiện dependency và unblock được ghi trong Task history và hiển thị trên timeline. Bằng chứng: [task workflow service](../Application/Services/TaskWorkflowService.cs) và [workflow integration test](../WorkManagementSystem.Tests/BackendWorkflowIntegrationTests.cs).

### 9. Recurring scheduling tồn tại qua restart như thế nào?

Template lưu `NextRunAtUtc`; generated occurrence lưu key duy nhất `(TemplateId, ScheduledForUtc)`. Mỗi occurrence được tạo trong transaction và persisted schedule được tăng, nên database chứ không phải worker memory là nguồn chuẩn. Bằng chứng: [scheduler](../Application/Services/RecurringTaskSchedulerService.cs), [occurrence configuration](../Infrastructure/Data/Configurations/GeneratedTaskOccurrenceConfiguration.cs) và [scheduler test](../WorkManagementSystem.Tests/RecurringTaskSchedulerTests.cs).

### 10. Nếu hai scheduler instance cạnh tranh thì sao?

Cả hai có thể phát hiện cùng template đến hạn, nhưng serializable transaction, optimistic concurrency và unique occurrence key chỉ cho phép một occurrence được commit. Lần thua được xử lý như conflict thay vì tạo công việc trùng. Bằng chứng: [scheduler](../Application/Services/RecurringTaskSchedulerService.cs) và [SQL Server test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 11. Reminder có tính idempotent như thế nào?

Service stage một milestone bền vững có event key duy nhất trước delivery, kiểm tra lại Task/policy/recipient rồi ghi trạng thái sent, failed hoặc suppressed. Retry có giới hạn và Task hoàn thành/bị xóa suppress delivery cũ. Bằng chứng: [deadline service](../Application/Services/DeadlineReminderService.cs) và [deadline test](../WorkManagementSystem.Tests/DeadlineReminderServiceTests.cs).

### 12. Tại sao workload không thuộc điểm KPI?

Planned effort là ước tính của Manager dùng cho cảnh báo capacity. KPI đo kết quả đã duyệt cùng dữ kiện deadline/từ chối. Trộn workload ước tính vào điểm nhân viên sẽ thưởng hoặc phạt theo ước tính của Manager thay vì công việc đã xác minh. Bằng chứng: [domain workflow](domain-workflows.md), [workload service](../Application/Services/WorkloadService.cs) và [KPI formula](../Application/Common/KpiFormula.cs).

### 13. Điều chuyển nhân sự được xử lý trong KPI như thế nào?

Work history có hiệu lực theo thời gian xác định role/unit của User trong một kỳ. Kết quả đã khóa giữ identity, Unit, formula version và raw metric snapshot nên điều chuyển hoặc xóa sau đó không thể viết lại output lịch sử. Bằng chứng: [performance service](../Application/Services/UserPerformanceService.cs) và [work-history KPI test](../WorkManagementSystem.Tests/UserKpiWorkHistoryTests.cs).

### 14. KPI GET endpoint có ghi dữ liệu không?

Không. Kỳ KPI được tạo rõ ràng qua command và luồng đọc resolve kỳ đã tồn tại. Điều này tránh hidden write, race unique key và side effect bất ngờ trong GET request. Bằng chứng: [KPI service](../Application/Services/KpiService.cs) và [KPI test](../WorkManagementSystem.Tests/KpiServiceTests.cs).

### 15. Chi phí query EF Core được kiểm tra như thế nào?

Luồng đọc dùng projection, `AsNoTracking` khi phù hợp và set-based aggregate. Integration test seed bộ dữ liệu trung bình và áp ngân sách số SQL command cho danh sách Task, workload, timeline và KPI dashboard. Bằng chứng: [ghi chú hiệu năng](performance.md) và [SQL Server test](../WorkManagementSystem.Tests/SqlServerRelationalTests.cs).

### 16. Tại sao tách liveness và readiness?

Liveness trả lời process có đang chạy không. Readiness còn kiểm tra kết nối database và khả năng ghi upload storage, để sự cố dependency loại instance khỏi traffic mà không tuyên bố chính process đã chết. Bằng chứng: [Program endpoint](../Program.cs), [health check](../Infrastructure/Health/UploadStorageHealthCheck.cs) và [operational test](../WorkManagementSystem.Tests/OperationalObservabilityTests.cs).

### 17. Những mối đe dọa upload nào được xử lý?

Service yêu cầu ngữ cảnh Task/Progress hợp lệ, kiểm tra quyền tài nguyên, giới hạn size/extension, so sánh MIME/signature, xác minh nội dung OOXML, từ chối macro, làm an toàn tên, giữ physical path riêng tư và dọn file mồ côi. Hệ thống không tuyên bố có antivirus scanning. Bằng chứng: [upload service](../Application/Services/UploadService.cs) và [upload test](../WorkManagementSystem.Tests/UploadServiceTests.cs).

### 18. Phục hồi database được xác minh thế nào?

Diễn tập CI tạo backup có checksum, restore vào database tạm, so sánh số record quan trọng và chạy `DBCC CHECKDB`. Nó xác minh quy trình cho topology SQL Server cục bộ này, không phải tuyên bố RPO/RTO production. Bằng chứng: [restore script](../scripts/backup-restore-drill.ps1), [CI workflow](../.github/workflows/backend-ci.yml) và [hướng dẫn phục hồi](recovery-and-workers.md).

### 19. Tại sao timeline không dùng event store riêng?

Hệ thống đã lưu Task history, Progress, Review, comment, file và scheduled notification làm nguồn chuẩn. Timeline ghép read model có phân quyền từ những dữ kiện đó, tránh thêm nguồn đồng bộ khác và dùng thứ tự cursor time/id ổn định. Bằng chứng: [timeline service](../Application/Services/TaskTimelineService.cs) và [timeline test](../WorkManagementSystem.Tests/TaskTimelineServiceTests.cs).

### 20. Sẽ cải thiện gì tiếp theo?

Xác minh policy KPI với tổ chức thật, thêm rotation refresh token nếu client cần session dài, chuyển upload sang object storage có malware scanning cho production, version public API và định nghĩa infrastructure/CD production. Broker hoặc tách service chỉ hợp lý khi có nhu cầu vận hành đã đo, không phải để portfolio trông phức tạp. Bằng chứng: [README](../README.md) và [production checklist](production-checklist.md).

## Giới hạn và non-goal đã biết

- Một ASP.NET Core assembly có thể triển khai; không có module hoặc microservice triển khai độc lập.
- Không có refresh-token flow, broker, transactional outbox, distributed SignalR backplane, Redis cache hoặc distributed lock.
- SQL Server là database provider được hỗ trợ.
- File storage cục bộ/container là riêng tư và đã validation nhưng không có external malware scanner hoặc object-storage adapter.
- KPI policy là domain policy minh họa và phải được xác minh trước khi dùng cho HR.
- CI xác minh build, test, migration, container và recovery, nhưng repository không định nghĩa cloud infrastructure hoặc production CD.
- Query budget phát hiện regression về lượt đi database; đây không phải benchmark latency, throughput hoặc scale.

Danh sách giới hạn chuẩn và hiện hành vẫn nằm trong [README](../README.md).
