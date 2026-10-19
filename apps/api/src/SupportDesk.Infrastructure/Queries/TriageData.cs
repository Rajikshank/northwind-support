using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.Infrastructure.Queries;
public sealed class TriageData(SupportDbContext db) : ITriageData
{
    public Task<Customer?> CustomerAsync(int id, CancellationToken ct) => db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Category?> CategoryAsync(int id, CancellationToken ct) => db.Set<Category>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<AssignmentCandidate>> CandidatesAsync(int categoryId, CancellationToken ct) =>
        await db.Set<Agent>().AsNoTracking().Select(a => new AssignmentCandidate(a.Id, a.IsActive,
            a.Specializations.Any(s => s.CategoryId == categoryId),
            db.Set<Ticket>().Count(t => t.AssignedAgentId == a.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed), a.MaxOpenTickets)).ToListAsync(ct);
    public async Task AddEscalationAsync(TicketEscalation escalation, CancellationToken ct) => await db.Set<TicketEscalation>().AddAsync(escalation, ct);
    public async Task<IReadOnlyList<TicketEscalation>> HistoryAsync(int ticketId, CancellationToken ct) =>
        await db.Set<TicketEscalation>().AsNoTracking().Where(x => x.TicketId == ticketId).OrderByDescending(x => x.EscalatedAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
}
