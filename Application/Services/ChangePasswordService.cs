using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.Application.Services
{
    public class ChangePasswordService : IChangePasswordService
    {
        private readonly IAppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IPasswordHashService _passwordHashService;

        public ChangePasswordService(
            IAppDbContext context,
            IAuditService auditService,
            IPasswordHashService passwordHashService)
        {
            _context = context;
            _auditService = auditService;
            _passwordHashService = passwordHashService;
        }

        public async Task ChangePassword(
            Guid userId,
            ChangePasswordDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.NewPassword != dto.ConfirmPassword)
                throw new BusinessException("Mật khẩu mới không khớp.");

            PasswordPolicy.EnsureValid(dto.NewPassword);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                throw new NotFoundException("Không tìm thấy người dùng.");

            if (!_passwordHashService.Verify(dto.OldPassword, user.PasswordHash))
                throw new BusinessException("Mật khẩu cũ không đúng.");

            if (_passwordHashService.Verify(dto.NewPassword, user.PasswordHash))
                throw new BusinessException("Mật khẩu mới phải khác mật khẩu hiện tại.");

            user.PasswordHash = _passwordHashService.Hash(dto.NewPassword);
            user.InvalidateSessions();
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.PasswordChanged,
                user.Id,
                cancellationToken: cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
