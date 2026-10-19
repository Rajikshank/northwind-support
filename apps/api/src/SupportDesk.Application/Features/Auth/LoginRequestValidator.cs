using FluentValidation;
using SupportDesk.Application.Abstractions;
namespace SupportDesk.Application.Features.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(1024);
    }
}
