namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Commits every change made to the aggregates loaded in the current request, atomically.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>Reads triage facts and commits its decision under one short serializable transaction.</summary>
    Task<T> ExecuteTicketWriteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
