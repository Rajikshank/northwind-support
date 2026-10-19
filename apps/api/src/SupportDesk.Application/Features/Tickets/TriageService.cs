using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
namespace SupportDesk.Application.Features.Tickets;
public sealed record TriageDecision(TicketPriority AppliedPriority, string PriorityReason, double SlaWindowHours, string SlaReason, string AssignmentReason);
public sealed class TriageService(ITriageData data, SlaPolicy sla)
{
    public async Task<TriageDecision> ApplyAsync(Ticket ticket, DateTime now, bool escalation, CancellationToken ct)
    {
        var customer = await data.CustomerAsync(ticket.CustomerId, ct) ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await data.CategoryAsync(ticket.CategoryId, ct) ?? throw new NotFoundException("Category", ticket.CategoryId);
        var priority = escalation ? ticket.Priority + 1 : category.ForcesCriticalPriority ? TicketPriority.Critical : ticket.Priority;
        var hours = sla.WindowHours(priority, customer.Tier);
        var due = now.AddHours(hours);
        var owner = AssignmentPolicy.Select(await data.CandidatesAsync(category.Id, ct), category.RequiresSpecialist, escalation ? ticket.AssignedAgentId : null);
        var kept = escalation && owner is not null && owner == ticket.AssignedAgentId;
        ticket.ApplyTriage(priority, owner, due, due.AddHours(-hours * sla.AtRiskThresholdPercent / 100), now);
        return new(priority, escalation ? "Raised one priority level." : category.ForcesCriticalPriority ? $"Category '{category.Name}' forces Critical." : "Requested priority, or Medium by default.",
            hours, $"{priority}: {hours:g} hours for {customer.Tier}; configured minimum applied.",
            kept ? "Current owner remains eligible." : owner is null ? "No active eligible agent has available capacity." : "Fewest open tickets among eligible agents; ties use lowest agent ID.");
    }
}
