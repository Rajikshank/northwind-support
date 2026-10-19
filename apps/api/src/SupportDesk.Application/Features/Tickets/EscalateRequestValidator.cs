using FluentValidation;
namespace SupportDesk.Application.Features.Tickets;
public sealed class EscalateRequestValidator : AbstractValidator<EscalateRequest>
{
    public EscalateRequestValidator()
    {
        RuleFor(x => x.Reason).Must(x => x is not null && x.Trim().Length is >= 5 and <= 500).WithMessage("Reason must contain 5–500 characters.");
        RuleFor(x => x.EscalatedBy).NotEmpty().MaximumLength(100);
    }
}
