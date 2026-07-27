using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class ChangePasswordService : IChangePasswordService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public ChangePasswordService(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<string> ChangePassword(
            Guid userId,
            ChangePasswordDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.NewPassword != dto.ConfirmPassword)
                return "Mật khẩu mới không khớp!";

            if (dto.NewPassword.Length < 6)
                return "Mật khẩu mới phải có ít nhất 6 ký tự!";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return "Không tìm thấy người dùng!";

            if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                return "Mật khẩu cũ không đúng!";

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.InvalidateSessions();
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.PasswordChanged,
                user.Id,
                cancellationToken: cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return "Đổi mật khẩu thành công!";
        }
    }
}
