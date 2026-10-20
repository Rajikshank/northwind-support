using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public class TicketConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleEscalationCannotOverwriteAnotherChangeOrAppendHistory(bool resolveFirst)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
        var now = FixedClock.DefaultNow;

        await using (var seed = new SupportDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, now);
            var category = new Category("General", false, false);
            seed.AddRange(customer, category);
            await seed.SaveChangesAsync();
            seed.Add(Ticket.Raise("TCK-0001", "Access failure", "Cannot sign in", customer.Id, category.Id, TicketPriority.Medium, now));
            await seed.SaveChangesAsync();
        }

        // Both requests read the same version before either has saved.
        await using var firstRequest = new SupportDbContext(options);
        await using var staleRequest = new SupportDbContext(options);
        var first = await firstRequest.Set<Ticket>().SingleAsync();
        var stale = await staleRequest.Set<Ticket>().SingleAsync();

        if (resolveFirst)
        {
            first.ChangeStatus(TicketStatus.Resolved, now);
        }
        else
        {
            Escalate(firstRequest, first, now);
        }
        await firstRequest.SaveChangesAsync();

        Escalate(staleRequest, stale, now);
        await Assert.ThrowsAsync<ConflictException>(() => staleRequest.SaveChangesAsync());

        await using var verify = new SupportDbContext(options);
        var saved = await verify.Set<Ticket>().SingleAsync();
        Assert.Equal(resolveFirst ? TicketStatus.Resolved : TicketStatus.New, saved.Status);
        Assert.Equal(resolveFirst ? TicketPriority.Medium : TicketPriority.High, saved.Priority);
        Assert.Equal(resolveFirst ? 0 : 1, await verify.Set<TicketEscalation>().CountAsync());
    }

    private static void Escalate(SupportDbContext db, Ticket ticket, DateTime now)
    {
        var history = new TicketEscalation(ticket, "Customer cannot access service", "agent:1", now);
        ticket.ApplyTriage(TicketPriority.High, null, now.AddHours(8), now.AddHours(6), now);
        history.Complete(ticket);
        db.Add(history);
    }
}
