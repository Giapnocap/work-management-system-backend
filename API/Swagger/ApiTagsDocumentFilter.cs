using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WorkManagementSystem.API.Swagger
{
    public sealed class ApiTagsDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Tags = new List<OpenApiTag>
            {
                new() { Name = "Auth", Description = "Register, login, approve accounts, and reset passwords." },
                new() { Name = "Unit", Description = "Department creation and staff membership management." },
                new() { Name = "User", Description = "Staff search, role changes, transfers, deletion, and performance lookup." },
                new() { Name = "Project", Description = "Department-scoped project grouping for tasks." },
                new() { Name = "Task", Description = "Task creation, update, reminders, dependencies, history, and activity timeline." },
                new() { Name = "RecurringTask", Description = "Durable recurring task schedules managed by department managers." },
                new() { Name = "ManagementReminder", Description = "Deadline reminder policies scoped to the system, departments, or projects." },
                new() { Name = "ManagementWorkload", Description = "Department workload summaries and capacity-based rankings." },
                new() { Name = "UserCapacity", Description = "Effective-dated employee capacity configuration." },
                new() { Name = "Progress", Description = "Employee progress reports and progress history." },
                new() { Name = "Review", Description = "Manager approval and rejection of submitted progress." },
                new() { Name = "KPI", Description = "KPI periods, versioned locked snapshots, and scoped management insights." },
                new() { Name = "Upload", Description = "Evidence file upload and protected download." },
                new() { Name = "Dashboard", Description = "Dashboard summaries for administrators and managers." },
                new() { Name = "Notification", Description = "Task and reminder notifications." },
                new() { Name = "Comment", Description = "Task comments, reactions, and seen state." },
                new() { Name = "SubTask", Description = "Task checklist items." },
                new() { Name = "Export", Description = "Excel export endpoints." },
                new() { Name = "Profile", Description = "Current user profile lookup and update." },
                new() { Name = "ChangePassword", Description = "Authenticated password changes." }
            };
        }
    }
}
