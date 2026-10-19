using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Infrastructure;

public class AuthenticationTests
{
    [Fact]
    public void PasswordHashesAreSaltedAndVerifyOnlyTheCorrectPassword()
    {
        var user = new Agent("Alex", "alex@example.com", 8, FixedClock.DefaultNow);
        var hasher = new PasswordHasher<Agent>();
        var hash = hasher.HashPassword(user, "LocalDemo-Only!2026");
        Assert.NotEqual(hash, hasher.HashPassword(user, "LocalDemo-Only!2026"));
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "LocalDemo-Only!2026"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "wrong"));
    }
    [Fact]
    public async Task LoginChangesTimestampOnlyOnSuccessAndReturnsPreviousLogin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new FixedClock();
        var user = new Agent("Alex", "alex@example.com", 8, clock.UtcNow);
        user.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(user, "correct-password")); db.Add(user); await db.SaveChangesAsync();
        var tokens = new Mock<ITokenIssuer>(); tokens.Setup(t => t.Issue(It.IsAny<SignedInAgent>())).Returns(new AccessToken("test-token", clock.UtcNow.AddMinutes(30)));
        var auth = new AuthenticationService(db, tokens.Object, clock);
        Assert.Null(await auth.LoginAsync(new("alex@example.com", "wrong"), default));
        Assert.Null(await auth.LoginAsync(new("missing@example.com", "wrong"), default));
        Assert.Null(user.LastSuccessfulLoginAtUtc);
        tokens.Verify(t => t.Issue(It.IsAny<SignedInAgent>()), Times.Never);
        var first = await auth.LoginAsync(new(" ALEX@example.com ", "correct-password"), default);
        Assert.NotNull(first); Assert.Null(first.PreviousSuccessfulLoginAtUtc);
        clock.Advance(TimeSpan.FromHours(1));
        var second = await auth.LoginAsync(new("alex@example.com", "correct-password"), default);
        Assert.Equal(FixedClock.DefaultNow, second!.PreviousSuccessfulLoginAtUtc);
        Assert.Equal(clock.UtcNow, user.LastSuccessfulLoginAtUtc);
        user.Deactivate(); await db.SaveChangesAsync();
        Assert.Null(await auth.LoginAsync(new("alex@example.com", "correct-password"), default));
    }
}
