namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>SLA status is derived from the promised dates and current time, never stored.</summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? atRiskAtUtc = null)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }
        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }
        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        // Exactly due is still AtRisk; a breach starts after the deadline.
        return atRiskAtUtc is not null && nowUtc >= atRiskAtUtc
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
