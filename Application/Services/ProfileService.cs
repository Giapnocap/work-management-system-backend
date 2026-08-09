using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace WorkManagementSystem.Application.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IAppDbContext _context;

        public ProfileService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ProfileDto> GetProfile(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                throw new NotFoundException("Khong tim thay ho so.");

            return new ProfileDto
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            };
        }

        public async Task<ProfileDto> UpdateProfile(
            Guid userId,
            ProfileDto dto,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                throw new NotFoundException("Khong tim thay nguoi dung.");

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new BusinessException("Ho ten khong duoc de trong.");

            user.FullName = dto.FullName.Trim();
            user.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            return new ProfileDto
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            };
        }
    }
}
