using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Infrastructure.Health
{
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;

        public DatabaseHealthCheck(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("Database connection is available.")
                    : HealthCheckResult.Unhealthy("Database connection is unavailable.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy(
                    "Database health check failed.",
                    exception);
            }
        }
    }
}
