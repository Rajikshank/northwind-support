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
        var passwordHash = verification == PasswordVerificationResult.SuccessRehashNeeded
            ? Hasher.HashPassword(agent, request.Password) : agent.PasswordHash;
        // Compare-and-swap serializes successful-login timestamps without holding a lock while hashing.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var previous = agent.LastSuccessfulLoginAtUtc;
            var now = previous is { } last && last > clock.UtcNow ? last : clock.UtcNow;
            var user = new SignedInAgent(agent.Id, agent.FullName, agent.Email, now);
            var token = tokens.Issue(user);
            var expectedHash = agent.PasswordHash;
            var changed = await db.Set<Agent>()
                .Where(a => a.Id == agent.Id && a.IsActive && a.PasswordHash == expectedHash && a.LastSuccessfulLoginAtUtc == previous)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.LastSuccessfulLoginAtUtc, now).SetProperty(a => a.PasswordHash, passwordHash), ct);
            if (changed == 1)
            {
                await db.Entry(agent).ReloadAsync(ct);
                return new(token.Token, token.ExpiresAtUtc, user, previous);
            }
            await db.Entry(agent).ReloadAsync(ct);
            if (!agent.IsActive || agent.PasswordHash != expectedHash) return null;
        }
        throw new SupportDesk.Application.Exceptions.ConflictException("Another sign-in is in progress. Please retry.");
    }

    public Task<SignedInAgent?> MeAsync(int id, CancellationToken ct) => db.Set<Agent>().AsNoTracking()
        .Where(a => a.Id == id && a.IsActive).Select(a => new SignedInAgent(a.Id, a.FullName, a.Email, a.LastSuccessfulLoginAtUtc)).FirstOrDefaultAsync(ct);
}
