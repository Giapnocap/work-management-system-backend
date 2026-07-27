using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WorkManagementSystem.Application.Common;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly JwtOptions _jwtOptions;
        private readonly IAuditService _auditService;
        private readonly IEmployeeCodeGenerator _employeeCodeGenerator;
        private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("invalid-password-placeholder");

        public AuthService(
            AppDbContext context,
            IOptions<JwtOptions> jwtOptions,
            IAuditService auditService,
            IEmployeeCodeGenerator employeeCodeGenerator)
        {
            _context = context;
            _jwtOptions = jwtOptions.Value;
            _auditService = auditService;
            _employeeCodeGenerator = employeeCodeGenerator;
        }

        public async Task<string> Register(
            AuthDto dto,
            CancellationToken cancellationToken = default)
        {
            var exists = await _context.Users.IgnoreQueryFilters()
                .AnyAsync(x => x.Username == dto.Username, cancellationToken);
            if (exists)
                throw new BusinessException("Tên đăng nhập đã tồn tại!");

            await EnsureUnitExists(dto.UnitId, cancellationToken);
            var employeeCode = await _employeeCodeGenerator.GenerateAsync(cancellationToken);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                FullName = dto.FullName,
                EmployeeCode = employeeCode,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User",
                UnitId = dto.UnitId,
                PhoneNumber = dto.PhoneNumber,
                IsApproved = false
            };

            _context.Users.Add(user);
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.Registered,
                null,
                new { user.Role, user.UnitId },
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return "Đăng ký thành công! Vui lòng chờ Admin phê duyệt.";
        }

        public async Task<string> Login(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            username = username?.Trim() ?? string.Empty;

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

            var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
            var passwordIsValid = BCrypt.Net.BCrypt.Verify(password, passwordHash);
            if (user == null || !passwordIsValid)
                throw new InvalidCredentialsException();

            if (!user.IsApproved)
                throw new BusinessException("Tài khoản chưa được Admin phê duyệt!");

            return GenerateToken(user);
        }

        public async Task<string> ResetPassword(
            ResetPasswordDto dto,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Username == dto.Username, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản!");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.InvalidateSessions();
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.PasswordReset,
                changedBy,
                cancellationToken: cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return "Đổi mật khẩu thành công!";
        }

        public async Task<string> ApproveUser(
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản!");

            await EnsureUnitExists(user.UnitId, cancellationToken);

            var approvalChanged = !user.IsApproved;
            if (approvalChanged)
            {
                user.IsApproved = true;
                user.InvalidateSessions();
                user.JoinedUnitAt = DateTime.UtcNow;
            }

            if (user.UnitId.HasValue)
            {
                var membership = await _context.Set<UserUnit>()
                    .FirstOrDefaultAsync(uu => uu.UserId == userId, cancellationToken);
                if (membership == null)
                {
                    _context.Set<UserUnit>().Add(new UserUnit
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        UnitId = user.UnitId.Value
                    });
                }
                else
                {
                    membership.UnitId = user.UnitId.Value;
                }
            }

            var hasHistory = await _context.UserWorkHistories
                .AnyAsync(h => h.UserId == userId && h.EffectiveTo == null, cancellationToken);
            if (!hasHistory)
            {
                _context.UserWorkHistories.Add(new UserWorkHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UnitId = user.UnitId,
                    Role = user.Role,
                    EffectiveFrom = user.JoinedUnitAt,
                    ChangeReason = "Approved account"
                });
            }

            if (approvalChanged)
            {
                await _auditService.RecordAsync(
                    AuditEntityTypes.Account,
                    user.Id,
                    AuditActions.Approved,
                    changedBy,
                    new { user.Role, user.UnitId },
                    cancellationToken);
            }
            await _context.SaveChangesAsync(cancellationToken);
            return $"Đã duyệt tài khoản {user.FullName}!";
        }

        public async Task<string> RejectUser(
            Guid userId,
            Guid? changedBy = null,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản!");

            user.IsDeleted = true;
            user.InvalidateSessions();
            await _auditService.RecordAsync(
                AuditEntityTypes.Account,
                user.Id,
                AuditActions.Rejected,
                changedBy,
                cancellationToken: cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return $"Đã từ chối tài khoản {user.FullName}!";
        }

        public async Task<List<UserDto>> GetPendingUsers(CancellationToken cancellationToken = default)
        {
            return await _context.Users.AsNoTracking()
                .Where(x => x.IsApproved == false)
                .Select(x => new UserDto
                {
                    Id = x.Id,
                    Username = x.Username ?? "",
                    FullName = x.FullName ?? "",
                    EmployeeCode = x.EmployeeCode ?? "",
                    Role = x.Role ?? "",
                    UnitId = x.UnitId,
                    IsApproved = x.IsApproved,
                    PhoneNumber = x.PhoneNumber
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<string> RefreshToken(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản!");
            if (!user.IsApproved || user.IsDeleted)
                throw new BusinessException("Tài khoản chưa được Admin phê duyệt!");
            return GenerateToken(user);
        }

        private async Task EnsureUnitExists(
            Guid? unitId,
            CancellationToken cancellationToken = default)
        {
            if (!unitId.HasValue)
                return;

            var exists = await _context.Units.AnyAsync(
                u => u.Id == unitId.Value,
                cancellationToken);
            if (!exists)
                throw new BusinessException("Phòng ban không tồn tại hoặc đã bị lưu trữ.");
        }

        private string GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(AuthenticationClaimTypes.UserId, user.Id.ToString()),
                new Claim(AuthenticationClaimTypes.TokenVersion, user.TokenVersion.ToString()),
                new Claim("employeeCode", user.EmployeeCode ?? ""),
                new Claim("fullName", user.FullName ?? ""),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.Key));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
        }
    }
}
