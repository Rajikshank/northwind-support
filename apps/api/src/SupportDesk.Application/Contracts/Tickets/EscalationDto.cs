using SupportDesk.Domain.Aggregates.Tickets;
namespace SupportDesk.Application.Contracts.Tickets;

public sealed record EscalationDto(int Id, int TicketId, TicketPriority FromPriority, TicketPriority ToPriority,
    int? FromAgentId, int? ToAgentId, DateTime? FromDueAtUtc, DateTime? ToDueAtUtc,
    string Reason, string EscalatedBy, DateTime EscalatedAtUtc)
{
    public static EscalationDto From(TicketEscalation row) => new(row.Id, row.TicketId, row.FromPriority, row.ToPriority,
        row.FromAgentId, row.ToAgentId, row.FromDueAtUtc, row.ToDueAtUtc, row.Reason, row.EscalatedBy, row.EscalatedAtUtc);
}
