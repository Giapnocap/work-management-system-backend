using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.API.Configuration
{
    public static class StartupConfigurationValidator
    {
        public static JwtOptions GetJwtOptions(
            IConfiguration configuration,
            bool isDevelopment,
            bool isProduction)
        {
            var options = configuration
                .GetSection(JwtOptions.SectionName)
                .Get<JwtOptions>() ?? new JwtOptions();

            if (isDevelopment && string.IsNullOrWhiteSpace(options.Key))
                options.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

            options.Validate(isProduction);
            return options;
        }

        public static ReverseProxySettings GetReverseProxySettings(
            IConfiguration configuration,
            bool isProduction)
        {
            var settings = configuration
                .GetSection(ReverseProxySettings.SectionName)
                .Get<ReverseProxySettings>() ?? new ReverseProxySettings();
            settings.KnownProxies ??= Array.Empty<string>();
            settings.KnownNetworks ??= Array.Empty<string>();
            settings.Validate(isProduction);
            return settings;
        }

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

            var parsedOrigins = origins.Select(origin =>
            {
                var value = origin.Trim();
                return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    ? uri
                    : null;
            }).ToArray();

            if (parsedOrigins.Any(uri =>
                    uri == null ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    !string.IsNullOrEmpty(uri.UserInfo) ||
                    uri.AbsolutePath != "/" ||
                    !string.IsNullOrEmpty(uri.Query) ||
                    !string.IsNullOrEmpty(uri.Fragment)))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins contains an invalid HTTP/HTTPS origin.");
            }

            if (isProduction &&
                parsedOrigins.Any(uri => uri!.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "All production CORS origins must use HTTPS.");
            }

            return parsedOrigins
                .Select(uri => uri!.GetLeftPart(UriPartial.Authority))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
