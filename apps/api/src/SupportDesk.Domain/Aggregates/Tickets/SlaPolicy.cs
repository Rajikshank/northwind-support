using SupportDesk.Domain.Aggregates.Customers;
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Configuration snapshot shared by creation and escalation.</summary>
public sealed class SlaPolicy
{
    public Dictionary<string, double> BaseWindowHours { get; set; } = new() { ["Critical"] = 4, ["High"] = 8, ["Medium"] = 24, ["Low"] = 72 };
    public double PremiumCustomerMultiplier { get; set; } = .5;
    public double MinimumWindowHours { get; set; } = 1;
    public double AtRiskThresholdPercent { get; set; } = 25;
    public double WindowHours(TicketPriority priority, CustomerTier tier) => Math.Max(MinimumWindowHours,
        BaseWindowHours[priority.ToString()] * (tier == CustomerTier.Premium ? PremiumCustomerMultiplier : 1));
    public bool IsValid() => Enum.GetValues<TicketPriority>().All(p => BaseWindowHours.TryGetValue(p.ToString(), out var h) && double.IsFinite(h) && h > 0 && h <= 87600)
        && double.IsFinite(PremiumCustomerMultiplier) && PremiumCustomerMultiplier > 0 && PremiumCustomerMultiplier <= 1
        && MinimumWindowHours > 0 && MinimumWindowHours <= 87600 && AtRiskThresholdPercent is >= 0 and <= 100;
}
