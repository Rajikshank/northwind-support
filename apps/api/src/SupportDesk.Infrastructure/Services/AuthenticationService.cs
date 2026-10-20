using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Services;

public sealed class AuthenticationService(SupportDbContext db, ITokenIssuer tokens, IClock clock) : IAuthentication
{
    private const int LoginUpdateAttempts = 3;
    private static readonly PasswordHasher<Agent> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(null!, "Not-A-Real-Account-Password");

    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var agent = await db.Set<Agent>().FirstOrDefaultAsync(account => account.Email == email, ct);

        // Unknown accounts still perform password verification; responses stay generic.
        var verification = Hasher.VerifyHashedPassword(agent!, agent?.PasswordHash ?? DummyHash, request.Password);
        if (agent is null || !agent.IsActive || agent.PasswordHash is null
            || verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var passwordHash = verification == PasswordVerificationResult.SuccessRehashNeeded
            ? Hasher.HashPassword(agent, request.Password)
            : agent.PasswordHash;

        // Compare-and-swap preserves the previous successful login without locking while hashing.
        for (var attempt = 0; attempt < LoginUpdateAttempts; attempt++)
        {
            var previous = agent.LastSuccessfulLoginAtUtc;
            var now = clock.UtcNow;
            if (previous is { } last && last > now)
            {
                now = last;
            }

            var user = new SignedInAgent(agent.Id, agent.FullName, agent.Email, now);
            var token = tokens.Issue(user);
            var expectedHash = agent.PasswordHash;
            var changed = await db.Set<Agent>()
                .Where(account => account.Id == agent.Id && account.IsActive
                    && account.PasswordHash == expectedHash && account.LastSuccessfulLoginAtUtc == previous)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(account => account.LastSuccessfulLoginAtUtc, now)
                    .SetProperty(account => account.PasswordHash, passwordHash), ct);

            await db.Entry(agent).ReloadAsync(ct);
            if (changed == 1)
            {
                return new LoginResult(token.Token, token.ExpiresAtUtc, user, previous);
            }
            if (!agent.IsActive || agent.PasswordHash != expectedHash)
            {
                return null;
            }
        }

        throw new ConflictException("Another sign-in is in progress. Please retry.");
    }

    public Task<SignedInAgent?> MeAsync(int id, CancellationToken ct) =>
        db.Set<Agent>().AsNoTracking()
            .Where(agent => agent.Id == id && agent.IsActive)
            .Select(agent => new SignedInAgent(agent.Id, agent.FullName, agent.Email, agent.LastSuccessfulLoginAtUtc))
            .FirstOrDefaultAsync(ct);
}
