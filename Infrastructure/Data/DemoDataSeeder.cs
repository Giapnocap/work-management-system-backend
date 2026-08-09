using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using ProgressStatusEnum = WorkManagementSystem.Domain.Enums.ProgressStatus;
using TaskPriorityEnum = WorkManagementSystem.Domain.Enums.TaskPriority;
using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;

namespace WorkManagementSystem.Infrastructure.Data
{
    public static class DemoDataSeeder
    {
        private const string DemoPassword = "Demo@123456";
        private static readonly DateTime SeedTime = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        public static async Task SeedAsync(
            IServiceProvider services,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
                return;

            var context = services.GetRequiredService<AppDbContext>();
            var passwordHashService = services.GetRequiredService<IPasswordHashService>();

            if (configuration.GetValue<bool>("DemoSeed:ApplyMigrations"))
                await context.Database.MigrateAsync(cancellationToken);

            var unit = await GetOrCreateUnitAsync(context, cancellationToken);
            var admin = await GetOrCreateUserAsync(context, passwordHashService, "demo.admin", "Demo Admin", "DEMO0001", SystemRoles.Admin, null, cancellationToken);
            var manager = await GetOrCreateUserAsync(context, passwordHashService, "demo.manager", "Demo Manager", "DEMO0002", SystemRoles.Manager, unit.Id, cancellationToken);
            var employeeA = await GetOrCreateUserAsync(context, passwordHashService, "demo.employee1", "Demo Employee 1", "DEMO0003", SystemRoles.User, unit.Id, cancellationToken);
            var employeeB = await GetOrCreateUserAsync(context, passwordHashService, "demo.employee2", "Demo Employee 2", "DEMO0004", SystemRoles.User, unit.Id, cancellationToken);

            await EnsureMembershipAsync(context, manager.Id, unit.Id, cancellationToken);
            await EnsureMembershipAsync(context, employeeA.Id, unit.Id, cancellationToken);
            await EnsureMembershipAsync(context, employeeB.Id, unit.Id, cancellationToken);

            await EnsureActiveWorkHistoryAsync(context, admin, null, cancellationToken);
            await EnsureActiveWorkHistoryAsync(context, manager, unit.Id, cancellationToken);
            await EnsureActiveWorkHistoryAsync(context, employeeA, unit.Id, cancellationToken);
            await EnsureActiveWorkHistoryAsync(context, employeeB, unit.Id, cancellationToken);

            var project = await GetOrCreateProjectAsync(context, unit.Id, manager.Id, cancellationToken);
            _ = await GetOrCreateCurrentMonthPeriodAsync(context, cancellationToken);

            await EnsureTaskAsync(
                context,
                project.Id,
                unit.Id,
                manager.Id,
                employeeA.Id,
                "Demo - Prepare dashboard API",
                TaskStatusEnum.Approved,
                TaskPriorityEnum.High,
                SeedTime.AddDays(2),
                SeedTime.AddDays(4),
                ProgressStatusEnum.Approved,
                cancellationToken);

            await EnsureTaskAsync(
                context,
                project.Id,
                unit.Id,
                manager.Id,
                employeeA.Id,
                "Demo - Implement task workflow",
                TaskStatusEnum.InProgress,
                TaskPriorityEnum.Medium,
                SeedTime.AddDays(3),
                SeedTime.AddDays(12),
                ProgressStatusEnum.InProgress,
                cancellationToken);

            await EnsureTaskAsync(
                context,
                project.Id,
                unit.Id,
                manager.Id,
                employeeB.Id,
                "Demo - Submit progress evidence",
                TaskStatusEnum.Submitted,
                TaskPriorityEnum.Medium,
                SeedTime.AddDays(4),
                SeedTime.AddDays(9),
                ProgressStatusEnum.Submitted,
                cancellationToken);

            await EnsureTaskAsync(
                context,
                project.Id,
                unit.Id,
                manager.Id,
                employeeB.Id,
                "Demo - Review KPI rules",
                TaskStatusEnum.NotStarted,
                TaskPriorityEnum.Low,
                SeedTime.AddDays(5),
                SeedTime.AddDays(18),
                null,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("Demo seed data ensured for the configured demo accounts.");
        }

        private static async Task<Unit> GetOrCreateUnitAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var unit = await context.Units.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Name == "Demo Engineering", cancellationToken);

            if (unit != null)
            {
                unit.IsDeleted = false;
                return unit;
            }

            unit = new Unit
            {
                Id = Guid.NewGuid(),
                Name = "Demo Engineering"
            };
            context.Units.Add(unit);
            return unit;
        }

        private static async Task<User> GetOrCreateUserAsync(
            AppDbContext context,
            IPasswordHashService passwordHashService,
            string username,
            string fullName,
            string employeeCode,
            string role,
            Guid? unitId,
            CancellationToken cancellationToken)
        {
            var user = await context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
            var userAlreadyExisted = user != null;

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = passwordHashService.Hash(DemoPassword),
                    JoinedUnitAt = SeedTime
                };
                context.Users.Add(user);
            }

            var shouldInvalidateSessions =
                userAlreadyExisted &&
                (user.Role != role ||
                 user.UnitId != unitId ||
                 !user.IsApproved ||
                 user.IsDeleted);

            user.FullName = fullName;
            user.EmployeeCode = employeeCode;
            user.Role = role;
            user.UnitId = unitId;
            user.IsApproved = true;
            user.IsDeleted = false;
            user.PhoneNumber = "0900000000";
            if (shouldInvalidateSessions)
                user.InvalidateSessions();
            if (user.JoinedUnitAt == default)
                user.JoinedUnitAt = SeedTime;

            return user;
        }

        private static async Task EnsureMembershipAsync(
            AppDbContext context,
            Guid userId,
            Guid unitId,
            CancellationToken cancellationToken)
        {
            var membership = await context.UserUnits.IgnoreQueryFilters()
                .FirstOrDefaultAsync(uu => uu.UserId == userId, cancellationToken);

            if (membership == null)
            {
                context.UserUnits.Add(new UserUnit
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UnitId = unitId
                });
            }
            else
            {
                membership.UnitId = unitId;
            }
        }

        private static async Task EnsureActiveWorkHistoryAsync(
            AppDbContext context,
            User user,
            Guid? unitId,
            CancellationToken cancellationToken)
        {
            var activeHistory = await context.UserWorkHistories.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    h => h.UserId == user.Id && h.EffectiveTo == null,
                    cancellationToken);

            if (activeHistory != null)
            {
                activeHistory.UnitId = unitId;
                activeHistory.Role = user.Role;
                return;
            }

            context.UserWorkHistories.Add(new UserWorkHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UnitId = unitId,
                Role = user.Role,
                EffectiveFrom = user.JoinedUnitAt == default ? SeedTime : user.JoinedUnitAt,
                ChangeReason = "Demo seed"
            });
        }

        private static async Task<Project> GetOrCreateProjectAsync(
            AppDbContext context,
            Guid unitId,
            Guid managerId,
            CancellationToken cancellationToken)
        {
            var project = await context.Projects.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    p => p.UnitId == unitId && p.Name == "Demo Workflow Project",
                    cancellationToken);

            if (project != null)
            {
                project.IsArchived = false;
                return project;
            }

            project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Demo Workflow Project",
                Description = "Seeded project for repeatable workflow testing.",
                UnitId = unitId,
                CreatedBy = managerId,
                CreatedAt = SeedTime
            };
            context.Projects.Add(project);
            return project;
        }

        private static async Task<KpiPeriod> GetOrCreateCurrentMonthPeriodAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);

            var period = await context.KpiPeriods
                .FirstOrDefaultAsync(
                    p => p.StartDate == start && p.EndDate == end,
                    cancellationToken);

            if (period != null)
                return period;

            period = new KpiPeriod
            {
                Id = Guid.NewGuid(),
                Name = $"KPI {start:MM/yyyy}",
                Type = "Monthly",
                StartDate = start,
                EndDate = end,
                Status = "Open",
                CreatedAt = SeedTime
            };
            context.KpiPeriods.Add(period);
            return period;
        }

        private static async Task EnsureTaskAsync(
            AppDbContext context,
            Guid projectId,
            Guid unitId,
            Guid managerId,
            Guid assigneeId,
            string title,
            TaskStatusEnum status,
            TaskPriorityEnum priority,
            DateTime createdAt,
            DateTime dueDate,
            ProgressStatusEnum? progressStatus,
            CancellationToken cancellationToken)
        {
            var task = await context.Tasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    t => t.ProjectId == projectId && t.Title == title,
                    cancellationToken);

            if (task == null)
            {
                task = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Description = "Seeded task for backend demo.",
                    CreatedBy = managerId,
                    CreatedAt = createdAt,
                    StartDate = createdAt,
                    RequiresReview = true,
                    UnitId = unitId,
                    ProjectId = projectId
                };
                context.Tasks.Add(task);
            }

            task.Status = status;
            task.Priority = priority;
            task.DueDate = dueDate;
            task.CompletedAt = status == TaskStatusEnum.Approved ? dueDate.AddDays(-1) : null;
            task.CompletedBy = status == TaskStatusEnum.Approved ? assigneeId : null;
            task.IsDeleted = false;

            var hasAssignee = await context.TaskAssignees.IgnoreQueryFilters()
                .AnyAsync(
                    a => a.TaskId == task.Id && a.UserId == assigneeId,
                    cancellationToken);

            if (!hasAssignee)
            {
                context.TaskAssignees.Add(new TaskAssignee
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = assigneeId
                });
            }

            if (progressStatus.HasValue)
            {
                var progress = await context.Progresses.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        p => p.TaskId == task.Id && p.UserId == assigneeId,
                        cancellationToken);

                if (progress == null)
                {
                    progress = new Progress
                    {
                        Id = Guid.NewGuid(),
                        TaskId = task.Id,
                        UserId = assigneeId
                    };
                    context.Progresses.Add(progress);
                }

                progress.Percent = status == TaskStatusEnum.Approved ? 100 : 70;
                progress.Description = "Seeded progress for backend demo.";
                progress.Status = progressStatus.Value;
                progress.HoursSpent = 2;
                progress.UpdatedAt = task.CompletedAt ?? dueDate.AddDays(-2);
            }
        }
    }
}
