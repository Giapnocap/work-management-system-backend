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
                new() { Name = "Auth", Description = "Đăng ký, đăng nhập, duyệt tài khoản và đặt lại mật khẩu." },
                new() { Name = "Unit", Description = "Tạo phòng ban và quản lý thành viên." },
                new() { Name = "User", Description = "Tìm kiếm nhân sự, đổi vai trò, điều chuyển, xóa và xem hiệu suất." },
                new() { Name = "Project", Description = "Nhóm công việc theo dự án trong phạm vi phòng ban." },
                new() { Name = "Task", Description = "Tạo, cập nhật, nhắc việc, quản lý quan hệ phụ thuộc, lịch sử và dòng hoạt động." },
                new() { Name = "RecurringTask", Description = "Quản lý lịch công việc định kỳ theo phòng ban." },
                new() { Name = "ManagementReminder", Description = "Chính sách nhắc hạn theo hệ thống, phòng ban hoặc dự án." },
                new() { Name = "ManagementWorkload", Description = "Tổng hợp khối lượng công việc và xếp hạng theo sức chứa." },
                new() { Name = "UserCapacity", Description = "Cấu hình sức chứa nhân sự theo thời gian hiệu lực." },
                new() { Name = "Progress", Description = "Báo cáo tiến độ và lịch sử báo cáo của nhân viên." },
                new() { Name = "Review", Description = "Trưởng phòng phê duyệt hoặc từ chối báo cáo đã nộp." },
                new() { Name = "KPI", Description = "Kỳ KPI, bản chốt có phiên bản và dữ liệu quản trị theo phạm vi." },
                new() { Name = "Upload", Description = "Tải tệp minh chứng lên và tải xuống có kiểm soát truy cập." },
                new() { Name = "Dashboard", Description = "Dữ liệu tổng quan dành cho Admin và Trưởng phòng." },
                new() { Name = "Notification", Description = "Thông báo công việc và nhắc hạn." },
                new() { Name = "Comment", Description = "Bình luận, biểu cảm và trạng thái đã xem trong công việc." },
                new() { Name = "SubTask", Description = "Danh sách công việc con." },
                new() { Name = "Export", Description = "Các endpoint xuất dữ liệu Excel." },
                new() { Name = "Profile", Description = "Xem và cập nhật hồ sơ người dùng hiện tại." },
                new() { Name = "ChangePassword", Description = "Đổi mật khẩu cho người dùng đã xác thực." }
            };
        }
    }
}
