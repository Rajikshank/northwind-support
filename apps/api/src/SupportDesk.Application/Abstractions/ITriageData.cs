using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Categories;
namespace SupportDesk.Application.Abstractions;
public interface ITriageData
{
    Task<Customer?> CustomerAsync(int id, CancellationToken ct);
    Task<Category?> CategoryAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AssignmentCandidate>> CandidatesAsync(int categoryId, CancellationToken ct);
    Task AddEscalationAsync(TicketEscalation escalation, CancellationToken ct);
    Task<IReadOnlyList<TicketEscalation>> HistoryAsync(int ticketId, CancellationToken ct);
}
