namespace SupportDesk.Application.Abstractions;

public sealed record LoginRequest(string Email, string Password);
public sealed record SignedInAgent(int Id, string FullName, string Email, DateTime? LastSuccessfulLoginAtUtc);
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
public sealed record LoginResult(string Token, DateTime ExpiresAtUtc, SignedInAgent User, DateTime? PreviousSuccessfulLoginAtUtc);
public interface ITokenIssuer { AccessToken Issue(SignedInAgent agent); }
public interface IAuthentication
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<SignedInAgent?> MeAsync(int id, CancellationToken ct);
}
