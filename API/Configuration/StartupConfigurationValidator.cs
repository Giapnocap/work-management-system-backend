using Microsoft.Data.SqlClient;

namespace WorkManagementSystem.API.Configuration
{
    public static class StartupConfigurationValidator
    {
        public static string GetConnectionString(
            IConfiguration configuration,
            bool isProduction)
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:Default is required.");

            if (!isProduction)
                return connectionString;

            var sqlConnection = new SqlConnectionStringBuilder(connectionString);
            if (!sqlConnection.Encrypt || sqlConnection.TrustServerCertificate)
            {
                throw new InvalidOperationException(
                    "Production database connections must use certificate-validated encryption.");
            }

            if (configuration.GetValue<bool>("DemoSeed:Enabled"))
                throw new InvalidOperationException("DemoSeed must be disabled in production.");

            var allowedHosts = (configuration["AllowedHosts"] ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowedHosts.Length == 0 || allowedHosts.Contains("*", StringComparer.Ordinal))
                throw new InvalidOperationException("AllowedHosts must be restricted in production.");

            return connectionString;
        }

        public static string[] GetCorsOrigins(
            IConfiguration configuration,
            bool isProduction)
        {
            var origins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            if (origins.Length == 0)
                throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin.");

            if (origins.Any(origin =>
                    !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins contains an invalid HTTP/HTTPS origin.");
            }

            if (isProduction &&
                origins.Any(origin => !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "All production CORS origins must use HTTPS.");
            }

            return origins;
        }
    }
}
