using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.Infrastructure.Services;

public sealed class DatabaseHealthCheck(SupportDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database is not reachable.");
}
