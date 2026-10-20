using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets;

public sealed record TriageDecision(
    TicketPriority AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason);

/// <summary>Loads triage facts once and applies the same policies to create and escalate.</summary>
public sealed class TriageService(ITriageData data, SlaPolicy sla)
{
    public async Task<TriageDecision> ApplyAsync(
        Ticket ticket,
        DateTime now,
        bool escalation,
        CancellationToken ct,
        bool defaultedPriority = false)
    {
        var customer = await data.CustomerAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await data.CategoryAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var priority = ticket.Priority;
        string priorityReason;
        if (escalation)
        {
            priority++;
            priorityReason = "Raised one priority level.";
        }
        else if (category.ForcesCriticalPriority)
        {
            priority = TicketPriority.Critical;
            priorityReason = $"Category '{category.Name}' forces Critical.";
        }
        else
        {
            priorityReason = defaultedPriority
                ? "No priority requested; defaulted to Medium."
                : $"Requested {priority} priority respected.";
        }

        var hours = sla.WindowHours(priority, customer.Tier);
        var due = now.AddHours(hours);
        var candidates = await data.CandidatesAsync(category.Id, ct);
        var currentOwner = escalation ? ticket.AssignedAgentId : null;
        var owner = AssignmentPolicy.Select(candidates, category.RequiresSpecialist, currentOwner);
        var keptOwner = owner is not null && owner == currentOwner;

        // Preserve the promised risk boundary even if configuration changes later.
        var atRisk = due.AddHours(-hours * sla.AtRiskThresholdPercent / 100);
        ticket.ApplyTriage(priority, owner, due, atRisk, now);

        var multiplier = customer.Tier == CustomerTier.Premium ? sla.PremiumCustomerMultiplier : 1;
        var slaReason = $"{priority} base {sla.BaseWindowHours[priority.ToString()]:g}h, "
            + $"{customer.Tier} multiplier {multiplier:g}: {hours:g}h window "
            + $"(Premium minimum {sla.MinimumWindowHours:g}h).";
        var assignmentReason = AssignmentReason(owner, keptOwner, category.RequiresSpecialist, category.Name);

        return new TriageDecision(priority, priorityReason, hours, slaReason, assignmentReason);
    }

    private static string AssignmentReason(int? owner, bool keptOwner, bool specialist, string categoryName)
    {
        if (keptOwner)
        {
            return "Current owner remains eligible.";
        }
        if (owner is null)
        {
            var qualification = specialist ? $" with specialization in {categoryName}" : string.Empty;
            return $"No active eligible agent has available capacity{qualification}.";
        }

        var pool = specialist ? $" {categoryName} specialist" : string.Empty;
        return $"Fewest open tickets among eligible{pool} agents; ties use lowest agent ID.";
    }
}
