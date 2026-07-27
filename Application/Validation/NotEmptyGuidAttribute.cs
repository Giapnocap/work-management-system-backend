using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class NotEmptyGuidAttribute : ValidationAttribute
    {
        public NotEmptyGuidAttribute()
        {
            ErrorMessage = "Id khong duoc rong.";
        }

        public override bool IsValid(object? value)
        {
            return value switch
            {
                null => true,
                Guid guid => guid != Guid.Empty,
                _ => false
            };
        }
    }
}
