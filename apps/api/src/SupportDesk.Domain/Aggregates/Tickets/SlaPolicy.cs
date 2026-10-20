using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Configuration snapshot shared by creation and escalation.</summary>
public sealed class SlaPolicy
{
    private const double MaximumWindowHours = 87600;

    public Dictionary<string, double> BaseWindowHours { get; set; } = new()
    {
        ["Critical"] = 4,
        ["High"] = 8,
        ["Medium"] = 24,
        ["Low"] = 72
    };

    public double PremiumCustomerMultiplier { get; set; } = .5;
    public double MinimumWindowHours { get; set; } = 1;
    public double AtRiskThresholdPercent { get; set; } = 25;

    public double WindowHours(TicketPriority priority, CustomerTier tier)
    {
        var baseHours = BaseWindowHours[priority.ToString()];
        return tier == CustomerTier.Premium
            ? Math.Max(MinimumWindowHours, baseHours * PremiumCustomerMultiplier)
            : baseHours;
    }

    public bool IsValid() =>
        Enum.GetValues<TicketPriority>().All(priority =>
            BaseWindowHours.TryGetValue(priority.ToString(), out var hours)
            && double.IsFinite(hours) && hours > 0 && hours <= MaximumWindowHours)
        && double.IsFinite(PremiumCustomerMultiplier)
        && PremiumCustomerMultiplier is > 0 and <= 1
        && MinimumWindowHours is > 0 and <= MaximumWindowHours
        && AtRiskThresholdPercent is >= 0 and <= 100;
}
