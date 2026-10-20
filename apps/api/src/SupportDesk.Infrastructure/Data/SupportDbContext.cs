using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// EF Core context for the support desk, and the unit of work that commits each request's
/// changes. Mapping lives in the per-aggregate configuration classes in
/// <c>Data/Configurations</c>.
/// </summary>
public class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options), IUnitOfWork
{
    private const int MaximumDeadlockAttempts = 5;

    public async Task<T> ExecuteTicketWriteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            // The entire decision must be recalculated after a rolled-back deadlock.
            // Reusing tracked entities would retry with the same obsolete workload/owner.
            ChangeTracker.Clear();
            var commitStarted = false;
            try
            {
                // Keep the counted workload ranges stable until assignment is committed.
                // SaveChanges alone cannot protect the capacity reads that precede it.
                await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await operation(ct);
                commitStarted = true;
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception exception) when (!commitStarted && IsDeadlock(exception))
            {
                // SQL error 1205 guarantees rollback. Connection/commit failures do not:
                // never replay an uncertain commit and accidentally create another ticket.
                if (attempt == MaximumDeadlockAttempts)
                {
                    throw new ConflictException("Ticket assignment is busy. Please retry your request.");
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt + Random.Shared.Next(10, 60)), ct);
            }
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        // EF can wrap a SQL failure in both an update and execution-strategy exception.
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is SqlException { Number: 1205 }) return true;
        }
        return false;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Ticket>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            // A new value on every write detects races even when the clock has not advanced.
            entry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // EF rolls back the entire SaveChanges, including any appended history row.
            throw new ConflictException("This ticket changed during your request. Reload it and try again.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.IsSqlServer())
        {
            // Reserve the fixed TCK-0001..0040 development seed range.
            modelBuilder.HasSequence<long>("TicketReferenceNumbers", "dbo").StartsAt(41);
        }
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp in this database is UTC; say so, so that it survives the round trip.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
