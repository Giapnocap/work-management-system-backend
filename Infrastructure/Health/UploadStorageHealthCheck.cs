using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkManagementSystem.Infrastructure.Health;

public sealed class UploadStorageHealthCheck : IHealthCheck
{
    private readonly IWebHostEnvironment _environment;

    public UploadStorageHealthCheck(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var uploadsRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "Uploads"));
        if (!Directory.Exists(uploadsRoot))
            return HealthCheckResult.Unhealthy("Upload storage directory is unavailable.");

        var probePath = Path.Combine(uploadsRoot, $".health-{Guid.NewGuid():N}.tmp");
        try
        {
            await using var stream = new FileStream(probePath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 1,
                Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
            });
            await stream.WriteAsync(new byte[] { 0 }, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            return HealthCheckResult.Healthy("Upload storage is writable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy(
                "Upload storage is not writable.",
                exception);
        }
        finally
        {
            try
            {
                File.Delete(probePath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // DeleteOnClose normally removes the probe; cleanup is best-effort.
            }
        }
    }
}
