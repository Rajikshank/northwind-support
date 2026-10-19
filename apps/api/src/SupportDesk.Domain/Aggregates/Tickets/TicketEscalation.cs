using SupportDesk.Domain.Common;
namespace SupportDesk.Domain.Aggregates.Tickets;
/// <summary>An append-only snapshot. No application operation updates or deletes history.</summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation() { }
    public TicketEscalation(Ticket ticket, string reason, string actor, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("Reason must contain 5-500 characters.", nameof(reason));
        if (string.IsNullOrWhiteSpace(actor) || actor.Trim().Length > 100) throw new ArgumentException("Actor is required and must fit 100 characters.", nameof(actor));
        TicketId = ticket.Id; FromPriority = ticket.Priority; FromAgentId = ticket.AssignedAgentId;
        FromDueAtUtc = ticket.DueAtUtc; Reason = reason; EscalatedBy = actor; EscalatedAtUtc = now;
    }
    public int TicketId { get; private set; }
    public TicketPriority FromPriority { get; private set; }
    public TicketPriority ToPriority { get; private set; }
    public int? FromAgentId { get; private set; }
    public int? ToAgentId { get; private set; }
    public DateTime? FromDueAtUtc { get; private set; }
    public DateTime? ToDueAtUtc { get; private set; }
    public string Reason { get; private set; } = "";
    public string EscalatedBy { get; private set; } = "";
    public DateTime EscalatedAtUtc { get; private set; }
    public void Complete(Ticket ticket) { if (ToPriority != 0) throw new InvalidOperationException("History is immutable once completed."); ToPriority = ticket.Priority; ToAgentId = ticket.AssignedAgentId; ToDueAtUtc = ticket.DueAtUtc; }
}
