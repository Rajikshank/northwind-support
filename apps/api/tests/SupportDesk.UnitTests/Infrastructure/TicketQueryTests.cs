using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Categories;
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
        var empty = await queries.GetPagedAsync(new TicketQuery { Search = "unknown" }, default);
        Assert.Empty(empty.Items);
    }
}
