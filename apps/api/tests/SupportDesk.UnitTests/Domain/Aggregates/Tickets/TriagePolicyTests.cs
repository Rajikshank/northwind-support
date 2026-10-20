using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TriagePolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    public void WindowUsesPriorityAndTier(TicketPriority priority, CustomerTier tier, double hours) =>
        Assert.Equal(hours, new SlaPolicy().WindowHours(priority, tier));
    [Fact]
    public void PremiumWindowHasAFloor() => Assert.Equal(1, new SlaPolicy { BaseWindowHours = new() { ["High"] = 1 } }.WindowHours(TicketPriority.High, CustomerTier.Premium));

    [Theory]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    public void RemainingWindows(TicketPriority priority, CustomerTier tier, double hours) => Assert.Equal(hours, new SlaPolicy().WindowHours(priority, tier));

    [Fact]
    public void AssignmentSkipsIneligibleCandidatesAndKeepsEligibleOwner()
    {
        AssignmentCandidate[] candidates = [new(1, false, true, 0, 10), new(2, true, false, 0, 10), new(3, true, true, 5, 5), new(4, true, true, 2, 5), new(5, true, true, 1, 5), new(6, true, true, 1, 5)];
        Assert.Equal(5, AssignmentPolicy.Select(candidates, true));
        Assert.Equal(4, AssignmentPolicy.Select(candidates, true, 4));
        Assert.Equal(5, AssignmentPolicy.Select(candidates, true, 3));
        Assert.Equal(2, AssignmentPolicy.Select(candidates, false));
        Assert.Null(AssignmentPolicy.Select(candidates.Take(3), true));
    }

    [Fact]
    public void SlaBoundariesAreExact()
    {
        var due = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        var risk = due.AddHours(-1);
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, risk.AddTicks(-1), risk));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, risk, risk));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, due, risk));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, null, due.AddTicks(1), risk));
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(due, due, due.AddDays(1), risk));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, due.AddTicks(1), due.AddDays(1), risk));
    }
}
