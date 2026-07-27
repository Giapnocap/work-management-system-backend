namespace WorkManagementSystem.Application.Common
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 180;

        public void Validate(bool isProduction)
        {
            if (string.IsNullOrWhiteSpace(Key) || Key.Length < 32)
                throw new InvalidOperationException("Jwt:Key must contain at least 32 characters.");

            if (isProduction && Key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Jwt:Key must be replaced in production.");

            if (string.IsNullOrWhiteSpace(Issuer))
                throw new InvalidOperationException("Jwt:Issuer is required.");

            if (string.IsNullOrWhiteSpace(Audience))
                throw new InvalidOperationException("Jwt:Audience is required.");

            if (ExpirationMinutes is < 5 or > 1440)
                throw new InvalidOperationException("Jwt:ExpirationMinutes must be between 5 and 1440.");
        }
    }
}
