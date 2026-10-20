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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp in this database is UTC; say so, so that it survives the round trip.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
