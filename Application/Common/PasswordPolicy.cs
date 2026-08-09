using System.ComponentModel.DataAnnotations;
using System.Text;

namespace WorkManagementSystem.Application.Common;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumUtf8Bytes = 72;

    public static string? GetValidationError(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            return $"Mat khau phai co it nhat {MinimumLength} ky tu.";

        if (Encoding.UTF8.GetByteCount(password) > MaximumUtf8Bytes)
            return $"Mat khau toi da {MaximumUtf8Bytes} byte UTF-8.";

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            return "Mat khau phai co chu hoa, chu thuong va chu so.";
        }

        return null;
    }

    public static void EnsureValid(string? password)
    {
        var error = GetValidationError(password);
        if (error != null)
            throw new BusinessException(error);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password || string.IsNullOrEmpty(password))
            return ValidationResult.Success;

        var error = PasswordPolicy.GetValidationError(password);
        if (error == null)
            return ValidationResult.Success;

        var memberNames = validationContext.MemberName == null
            ? Array.Empty<string>()
            : new[] { validationContext.MemberName };
        return new ValidationResult(error, memberNames);
    }
}
