using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
namespace SupportDesk.Application.Features.Tickets;

public sealed class TicketQueryValidator : AbstractValidator<TicketQuery>
{
    public TicketQueryValidator()
    {
        RuleFor(x => x.Priority).Must(x => x is null || Enum.IsDefined(x.Value));
        RuleFor(x => x.Status).Must(x => x is null || Enum.IsDefined(x.Value));
        RuleFor(x => x.SlaStatus).Must(x => x is null || Enum.IsDefined(x.Value));
        RuleFor(x => x.CategoryId).Must(x => x is null || x > 0);
        RuleFor(x => x.CustomerId).Must(x => x is null || x > 0);
        RuleFor(x => x.AssignedAgentId).Must(x => x is null || x > 0);
        RuleFor(x => x.SortDirection).Must(x => string.Equals(x, "asc", StringComparison.OrdinalIgnoreCase) || string.Equals(x, "desc", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.SortBy).Must(x => new[] { "createdAtUtc", "updatedAtUtc", "dueAtUtc", "priority", "status" }.Contains(x, StringComparer.OrdinalIgnoreCase));
    }
}
