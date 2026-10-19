using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
namespace SupportDesk.Application.Features.Tickets;

public sealed record EscalateRequest(string Reason, string EscalatedBy);
public sealed record EscalationResult(TicketDetailDto Ticket, EscalationDto Escalation);
public sealed class EscalateTicketHandler(ITicketRepository tickets, ITriageData data, TriageService triage, IClock clock, IUnitOfWork unit, GetTicketQueryHandler read)
{
    public async Task<EscalationResult> HandleAsync(int id, EscalateRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        ticket.EnsureCanEscalate();
        var now = clock.UtcNow;
        var history = new TicketEscalation(ticket, request.Reason.Trim(), request.EscalatedBy.Trim(), now);
        var decision = await triage.ApplyAsync(ticket, now, true, ct);
        history.Complete(ticket);
        await data.AddEscalationAsync(history, ct);
        // One SaveChanges commits the ticket and its history atomically.
        await unit.SaveChangesAsync(ct);
        var result = await read.HandleAsync(id, ct);
        result.Triage = decision;
        return new(result, EscalationDto.From(history));
    }
}
