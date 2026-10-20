namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed record AssignmentCandidate(int Id, bool IsActive, bool IsSpecialist, int OpenTickets, int Limit);

public static class AssignmentPolicy
{
    public static int? Select(IEnumerable<AssignmentCandidate> candidates, bool requiresSpecialist, int? currentAgentId = null)
    {
        var eligible = candidates
            .Where(agent => agent.IsActive
                && (!requiresSpecialist || agent.IsSpecialist)
                && agent.OpenTickets < agent.Limit)
            .ToArray();

        // Escalation keeps an eligible owner even if another agent has fewer tickets.
        if (currentAgentId is { } current && eligible.Any(agent => agent.Id == current))
        {
            return current;
        }

        return eligible.OrderBy(agent => agent.OpenTickets)
            .ThenBy(agent => agent.Id)
            .Select(agent => (int?)agent.Id)
            .FirstOrDefault();
    }
}
