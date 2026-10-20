using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TriageData(SupportDbContext db) : ITriageData
{
    public Task<Customer?> CustomerAsync(int id, CancellationToken ct) =>
        db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(customer => customer.Id == id, ct);

    public Task<Category?> CategoryAsync(int id, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking().FirstOrDefaultAsync(category => category.Id == id, ct);

    public async Task<IReadOnlyList<AssignmentCandidate>> CandidatesAsync(int categoryId, CancellationToken ct) =>
        // The command holds a serializable transaction from these reads through its save.
        // Specializations and workloads are scalar subqueries, not one request per agent.
        await db.Set<Agent>().AsNoTracking()
            .Select(agent => new AssignmentCandidate(
                agent.Id,
                agent.IsActive,
                agent.Specializations.Any(specialization => specialization.CategoryId == categoryId),
                db.Set<Ticket>().Count(ticket => ticket.AssignedAgentId == agent.Id
                    && ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed),
                agent.MaxOpenTickets))
            .ToListAsync(ct);

    public async Task AddEscalationAsync(TicketEscalation escalation, CancellationToken ct) =>
        await db.Set<TicketEscalation>().AddAsync(escalation, ct);

    public async Task<IReadOnlyList<TicketEscalation>> HistoryAsync(int ticketId, CancellationToken ct) =>
        await db.Set<TicketEscalation>().AsNoTracking()
            .Where(escalation => escalation.TicketId == ticketId)
            .OrderByDescending(escalation => escalation.EscalatedAtUtc)
            .ThenByDescending(escalation => escalation.Id)
            .ToListAsync(ct);
}
