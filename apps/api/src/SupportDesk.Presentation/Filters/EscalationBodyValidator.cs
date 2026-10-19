using FluentValidation;
using SupportDesk.Presentation.Controllers;
namespace SupportDesk.Presentation.Filters;
public sealed class EscalationBodyValidator : AbstractValidator<EscalationBody>
{
    public EscalationBodyValidator() => RuleFor(x => x.Reason).Must(x => x is not null && x.Trim().Length is >= 5 and <= 500).WithMessage("Reason must contain 5-500 characters.");
}
