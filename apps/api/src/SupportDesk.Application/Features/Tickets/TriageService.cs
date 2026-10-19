using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
namespace SupportDesk.Application.Features.Tickets;

public sealed record TriageDecision(TicketPriority AppliedPriority, string PriorityReason, double SlaWindowHours, string SlaReason, string AssignmentReason);
public sealed class TriageService(ITriageData data, SlaPolicy sla)
{
    public async Task<TriageDecision> ApplyAsync(Ticket ticket, DateTime now, bool escalation, CancellationToken ct, bool defaultedPriority = false)
    {
        var customer = await data.CustomerAsync(ticket.CustomerId, ct) ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await data.CategoryAsync(ticket.CategoryId, ct) ?? throw new NotFoundException("Category", ticket.CategoryId);
        var priority = escalation ? ticket.Priority + 1 : category.ForcesCriticalPriority ? TicketPriority.Critical : ticket.Priority;
        var hours = sla.WindowHours(priority, customer.Tier);
        var due = now.AddHours(hours);
        var owner = AssignmentPolicy.Select(await data.CandidatesAsync(category.Id, ct), category.RequiresSpecialist, escalation ? ticket.AssignedAgentId : null);
        var kept = escalation && owner is not null && owner == ticket.AssignedAgentId;
        ticket.ApplyTriage(priority, owner, due, due.AddHours(-hours * sla.AtRiskThresholdPercent / 100), now);
        var priorityReason = escalation ? "Raised one priority level."
            : category.ForcesCriticalPriority ? $"Category '{category.Name}' forces Critical."
            : defaultedPriority ? "No priority requested; defaulted to Medium." : $"Requested {priority} priority respected.";
        var multiplier = customer.Tier == SupportDesk.Domain.Aggregates.Customers.CustomerTier.Premium ? sla.PremiumCustomerMultiplier : 1;
        var slaReason = $"{priority} base {sla.BaseWindowHours[priority.ToString()]:g}h, {customer.Tier} multiplier {multiplier:g}: {hours:g}h window (Premium minimum {sla.MinimumWindowHours:g}h).";
        return new(priority, priorityReason, hours, slaReason,
            kept ? "Current owner remains eligible." : owner is null
                ? $"No active eligible agent has available capacity{(category.RequiresSpecialist ? $" with specialization in {category.Name}" : "")}."
                : $"Fewest open tickets among eligible{(category.RequiresSpecialist ? $" {category.Name} specialist" : "")} agents; ties use lowest agent ID.");
    }
}
