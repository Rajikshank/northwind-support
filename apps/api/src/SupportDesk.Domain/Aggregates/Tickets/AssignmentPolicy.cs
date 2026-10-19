namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed record AssignmentCandidate(int Id, bool IsActive, bool IsSpecialist, int OpenTickets, int Limit);
public static class AssignmentPolicy
{
    public static int? Select(IEnumerable<AssignmentCandidate> candidates, bool requiresSpecialist, int? currentAgentId = null)
    {
        var eligible = candidates.Where(a => a.IsActive && (!requiresSpecialist || a.IsSpecialist) && a.OpenTickets < a.Limit).ToArray();
        if (currentAgentId is { } current && eligible.Any(a => a.Id == current)) return current;
        return eligible.OrderBy(a => a.OpenTickets).ThenBy(a => a.Id).Select(a => (int?)a.Id).FirstOrDefault();
    }
}
