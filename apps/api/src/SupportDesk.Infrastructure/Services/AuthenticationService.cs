using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.Infrastructure.Services;
public sealed class AuthenticationService(SupportDbContext db, ITokenIssuer tokens, IClock clock) : IAuthentication
{
    private static readonly PasswordHasher<Agent> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(null!, "Not-A-Real-Account-Password");
    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var agent = await db.Set<Agent>().FirstOrDefaultAsync(a => a.Email == email, ct);
        var verification = Hasher.VerifyHashedPassword(agent!, agent?.PasswordHash ?? DummyHash, request.Password);
        if (agent is null || !agent.IsActive || agent.PasswordHash is null || verification == PasswordVerificationResult.Failed) return null;
        var now = clock.UtcNow;
        var previous = agent.LastSuccessfulLoginAtUtc;
        var user = new SignedInAgent(agent.Id, agent.FullName, agent.Email, now);
        var token = tokens.Issue(user);
        if (verification == PasswordVerificationResult.SuccessRehashNeeded) agent.SetPasswordHash(Hasher.HashPassword(agent, request.Password));
        agent.RecordLogin(now);
        await db.SaveChangesAsync(ct);
        return new(token.Token, token.ExpiresAtUtc, user, previous);
    }
    public Task<SignedInAgent?> MeAsync(int id, CancellationToken ct) => db.Set<Agent>().AsNoTracking()
        .Where(a => a.Id == id && a.IsActive).Select(a => new SignedInAgent(a.Id, a.FullName, a.Email, a.LastSuccessfulLoginAtUtc)).FirstOrDefaultAsync(ct);
}
