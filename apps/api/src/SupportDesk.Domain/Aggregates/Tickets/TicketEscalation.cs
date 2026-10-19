using SupportDesk.Domain.Common;
namespace SupportDesk.Domain.Aggregates.Tickets;
/// <summary>An append-only snapshot. No application operation updates or deletes history.</summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation() { }
    public TicketEscalation(Ticket ticket, string reason, string actor, DateTime now)
    {
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
    public void Complete(Ticket ticket) { ToPriority = ticket.Priority; ToAgentId = ticket.AssignedAgentId; ToDueAtUtc = ticket.DueAtUtc; }
}
