using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public class TicketQueryTests
{
    [Fact]
    public async Task FiltersBeforeCountingAndPaging_WithExactPriorityAndCustomerSearch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var customer = new Customer("Acme", "test@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("Help", false, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();
        foreach (var (id, priority) in new[] { (1, TicketPriority.High), (2, TicketPriority.Low), (3, TicketPriority.Critical) })
            db.Add(Ticket.Raise($"TCK-{id:D4}", "Printer", "Cannot print", customer.Id, category.Id, priority, FixedClock.DefaultNow));
        await db.SaveChangesAsync();
        var queries = new TicketQueries(db, new FixedClock());
        var result = await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High, Search = " Acme ", CategoryId = category.Id, PageSize = 1 }, default);
        Assert.Single(result.Items);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, (await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.Low }, default)).TotalCount);
        Assert.Equal(3, (await queries.GetPagedAsync(new TicketQuery { Search = "   ", UnassignedOnly = true }, default)).TotalCount);
        Assert.Equal(0, (await queries.GetPagedAsync(new TicketQuery { UnassignedOnly = true, AssignedAgentId = 1 }, default)).TotalCount);
        Assert.Equal(0, (await queries.GetPagedAsync(new TicketQuery { Status = TicketStatus.Closed }, default)).TotalCount);
        Assert.Equal(0, (await queries.GetPagedAsync(new TicketQuery { CustomerId = 999 }, default)).TotalCount);
        Assert.Equal(0, (await queries.GetPagedAsync(new TicketQuery { CategoryId = 999 }, default)).TotalCount);
        Assert.Equal(1, (await queries.GetPagedAsync(new TicketQuery { Search = "TCK-0002" }, default)).TotalCount);
        var empty = await queries.GetPagedAsync(new TicketQuery { Search = "unknown" }, default);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task SqlSlaFilterAndProjectionAgreeAtEveryBoundary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var statements = new List<string>();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).LogTo(statements.Add).Options);
        await db.Database.EnsureCreatedAsync();
        var now = FixedClock.DefaultNow;
        var customer = new Customer("Acme", "a@example.com", null, CustomerTier.Standard, now);
        var category = new Category("General", false, false);
        db.AddRange(customer, category); await db.SaveChangesAsync();
        var tickets = new List<Ticket>();
        for (var i = 1; i <= 6; i++) tickets.Add(Ticket.Raise($"TCK-{i:D4}", "Issue", "Details", customer.Id, category.Id, TicketPriority.High, now));
        tickets[1].ApplyTriage(TicketPriority.High, null, now, now.AddHours(-1), now);
        tickets[2].ApplyTriage(TicketPriority.High, null, now.AddTicks(-1), now.AddHours(-1), now);
        tickets[3].ApplyTriage(TicketPriority.High, null, now.AddHours(4), now.AddHours(3), now);
        tickets[4].ApplyTriage(TicketPriority.High, null, now.AddHours(4), now, now);
        tickets[5].ApplyTriage(TicketPriority.High, null, now, now.AddHours(-1), now);
        tickets[5].ChangeStatus(TicketStatus.Resolved, now);
        db.AddRange(tickets); await db.SaveChangesAsync();
        var queries = new TicketQueries(db, new FixedClock());
        foreach (var state in Enum.GetValues<SlaStatus>())
        {
            var result = await queries.GetPagedAsync(new TicketQuery { SlaStatus = state, PageSize = 1 }, default);
            Assert.NotEmpty(result.Items); Assert.All(result.Items, row => Assert.Equal(state, row.SlaStatus));
            var expected = tickets.Count(t => SlaEvaluator.Evaluate(t.DueAtUtc, t.ResolvedAtUtc, now, t.SlaAtRiskAtUtc) == state);
            Assert.Equal(expected, result.TotalCount);
        }
        Assert.Contains(statements, sql => sql.Contains("CASE") && sql.Contains("WHERE"));
        Assert.Contains(statements, sql => sql.Contains("COUNT(*)") && sql.Contains("CASE"));
    }
}
