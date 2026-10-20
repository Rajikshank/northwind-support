using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads and stores <see cref="Ticket"/> aggregates. Loaded tickets are tracked, so the unit
/// of work saves whatever their methods changed.
/// </summary>
public sealed class TicketRepository(SupportDbContext db) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Ticket>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        // SQL Server allocates each value atomically across requests and API processes.
        // A rollback can leave a gap; references identify tickets, they are not a row count.
        // Do not compose this query: NEXT VALUE FOR is not valid inside an EF subquery.
        var values = await db.Database.SqlQueryRaw<long>(
            "SELECT NEXT VALUE FOR dbo.TicketReferenceNumbers AS [Value]").ToListAsync(ct);
        return $"TCK-{values.Single():D4}";
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct) =>
        await db.Set<Ticket>().AddAsync(ticket, ct);
}
