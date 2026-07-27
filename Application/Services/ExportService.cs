using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class ExportService : IExportService
    {
        private readonly AppDbContext _context;

        public ExportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> ExportTasksToExcel(
            Guid requestedBy,
            CancellationToken cancellationToken = default)
        {
            var requester = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == requestedBy, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var taskQuery = _context.Tasks.AsNoTracking().Where(t => !t.IsDeleted);
            if (requester.Role == "Manager")
            {
                if (!requester.UnitId.HasValue)
                    taskQuery = taskQuery.Where(t => false);
                else
                    taskQuery = taskQuery.Where(t => t.UnitId == requester.UnitId.Value);
            }
            else if (requester.Role != "Admin")
            {
                throw new ForbiddenException("Ban khong co quyen export danh sach cong viec.");
            }

            var tasks = await taskQuery.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
            var unitIds = tasks.Where(t => t.UnitId.HasValue).Select(t => t.UnitId!.Value).Distinct().ToList();
            var units = await _context.Units
                .AsNoTracking()
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Tasks");

            sheet.Cell(1, 1).Value = "STT";
            sheet.Cell(1, 2).Value = "Ten cong viec";
            sheet.Cell(1, 3).Value = "Mo ta";
            sheet.Cell(1, 4).Value = "Trang thai";
            sheet.Cell(1, 5).Value = "Uu tien";
            sheet.Cell(1, 6).Value = "Bat dau";
            sheet.Cell(1, 7).Value = "Deadline";
            sheet.Cell(1, 8).Value = "Phong ban";
            sheet.Cell(1, 9).Value = "Gio thuc te";

            var header = sheet.Range("A1:I1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 2;
            foreach (var task in tasks)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = task.Title;
                sheet.Cell(row, 3).Value = task.Description ?? "";
                sheet.Cell(row, 4).Value = task.Status.ToString();
                sheet.Cell(row, 5).Value = task.Priority.ToString();
                sheet.Cell(row, 6).Value = task.StartDate?.ToString("dd/MM/yyyy") ?? "";
                sheet.Cell(row, 7).Value = task.DueDate?.ToString("dd/MM/yyyy") ?? "";
                sheet.Cell(row, 8).Value = task.UnitId.HasValue && units.TryGetValue(task.UnitId.Value, out var unitName) ? unitName : "";
                sheet.Cell(row, 9).Value = task.ActualHours;

                if (row % 2 == 0)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

                row++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> ExportProgressToExcel(
            Guid requestedBy,
            CancellationToken cancellationToken = default)
        {
            var requester = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == requestedBy, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            var taskQuery = _context.Tasks.AsNoTracking().Where(t => !t.IsDeleted);
            if (requester.Role == "Manager")
            {
                if (!requester.UnitId.HasValue)
                    taskQuery = taskQuery.Where(t => false);
                else
                    taskQuery = taskQuery.Where(t => t.UnitId == requester.UnitId.Value);
            }
            else if (requester.Role != "Admin")
            {
                throw new ForbiddenException("Ban khong co quyen export tien do.");
            }

            var tasks = await taskQuery.ToDictionaryAsync(t => t.Id, t => t, cancellationToken);
            var taskIds = tasks.Keys.ToList();
            var progresses = await _context.Progresses
                .Where(p => taskIds.Contains(p.TaskId))
                .OrderByDescending(p => p.UpdatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userIds = progresses.Select(p => p.UserId).Distinct().ToList();
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Progress");

            sheet.Cell(1, 1).Value = "STT";
            sheet.Cell(1, 2).Value = "Cong viec";
            sheet.Cell(1, 3).Value = "Nhan vien";
            sheet.Cell(1, 4).Value = "Mo ta";
            sheet.Cell(1, 5).Value = "Phan tram";
            sheet.Cell(1, 6).Value = "Trang thai";
            sheet.Cell(1, 7).Value = "Gio bao cao";
            sheet.Cell(1, 8).Value = "Ngay cap nhat";

            var header = sheet.Range("A1:H1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 2;
            foreach (var progress in progresses)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = tasks.TryGetValue(progress.TaskId, out var task) ? task.Title : "";
                sheet.Cell(row, 3).Value = users.TryGetValue(progress.UserId, out var userName) ? userName : "";
                sheet.Cell(row, 4).Value = progress.Description ?? "";
                sheet.Cell(row, 5).Value = progress.Percent + "%";
                sheet.Cell(row, 6).Value = progress.Status.ToString();
                sheet.Cell(row, 7).Value = progress.HoursSpent;
                sheet.Cell(row, 8).Value = progress.UpdatedAt.ToString("dd/MM/yyyy HH:mm");

                if (row % 2 == 0)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

                row++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
