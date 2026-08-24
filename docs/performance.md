# Query performance evidence

Last verified: 2026-08-24 against SQL Server 2022.

The relational integration suite enforces query-count budgets for the main read paths. A reader command includes authorization and DTO enrichment queries. The budgets are deliberately based on SQL command count, not wall-clock latency, because CI and developer machines are not stable benchmark environments.

| Read path | Test dataset | Reader-command budget |
| --- | ---: | ---: |
| `GET /api/tasks` | 30 employees, 300 tasks, assignees, subtasks, files and dependencies | 9 |
| `GET /api/management/workload` | 30 employees with assigned effort | 4 |
| `GET /api/tasks/{id}/timeline` | 40 timeline comments plus a workflow transition | 5 |
| `GET /api/kpi-periods/{id}/dashboard` | 30 employees with tasks and approved reports | 11 |

The Task list and Timeline tests execute both small and large pages from equivalent clean tracking states. Their command count must remain constant as page size grows. Workload and KPI use set-based aggregation and are checked against larger units.

Run the evidence tests with a disposable SQL Server:

```powershell
$env:WMS_TEST_SQLSERVER_CONNECTION = "Server=localhost,14333;Database=master;User Id=sa;Password=<password>;Encrypt=False;TrustServerCertificate=True"
dotnet test WorkManagementSystem.Tests/WorkManagementSystem.Tests.csproj -c Release --filter "Category=SqlServer"
```

## Index review

The measured query patterns use the existing indexes on task scope, project/status, assignee target/task, timeline source/task/time, scheduled notification state, recurring schedule, KPI period/user and child task foreign keys. The DTO builder performs a fixed set of batch queries and does not query inside the per-task mapping loop.

No index was added in this phase. The measured command counts are constant, all target queries translate on SQL Server, and no specific scan or sort bottleneck justified another write-cost-bearing index. Add an index only after a production-like execution plan identifies a real bottleneck.

No cache or latency SLA is introduced. A cache would add invalidation complexity before a measured latency bottleneck exists, while a latency threshold from an unstable CI runner would create misleading failures.
