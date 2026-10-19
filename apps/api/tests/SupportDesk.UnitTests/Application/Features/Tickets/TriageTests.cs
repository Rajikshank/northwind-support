using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.UnitTests.TestDoubles;
namespace SupportDesk.UnitTests.Application.Features.Tickets;
public class TriageTests
{
    private readonly FixedClock clock = new();
    private readonly Mock<ITriageData> data = new();
    private TriageService Service(bool forced = false)
    {
        data.Setup(d => d.CustomerAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Customer("Premium", "p@example.com", null, CustomerTier.Premium, clock.UtcNow));
        data.Setup(d => d.CategoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Category("Category", true, forced));
        data.Setup(d => d.CandidatesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new AssignmentCandidate(3, true, true, 4, 8), new AssignmentCandidate(5, true, true, 6, 6) });
        return new TriageService(data.Object, new SlaPolicy());
    }
    [Fact]
    public async Task CreateForcesCriticalAndExplainsAssignment()
    {
        var service = Service(true);
        var ticket = Ticket.Raise("TCK-0001", "Title", "Description", 1, 1, TicketPriority.Low, clock.UtcNow);
        var decision = await service.ApplyAsync(ticket, clock.UtcNow, false, default);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(clock.UtcNow.AddHours(2), ticket.DueAtUtc);
        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Contains("forces Critical", decision.PriorityReason);
    }
    [Fact]
    public async Task EscalationUsesNowAndWritesBeforeAfterInOneSave()
    {
        var service = Service();
        var ticket = Ticket.Raise("TCK-0001", "Title", "Description", 1, 1, TicketPriority.Medium, clock.UtcNow);
        ticket.ApplyTriage(TicketPriority.Medium, 3, clock.UtcNow.AddHours(12), clock.UtcNow.AddHours(9), clock.UtcNow);
        var originalDue = ticket.DueAtUtc;
        clock.Advance(TimeSpan.FromHours(3));
        var repo = new Mock<ITicketRepository>(); repo.Setup(r => r.GetByIdAsync(0, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var unit = new Mock<IUnitOfWork>();
        var queries = new Mock<ITicketQueries>(); queries.Setup(q => q.GetDetailAsync(0, It.IsAny<CancellationToken>())).ReturnsAsync(new TicketDetailDto());
        var handler = new EscalateTicketHandler(repo.Object, data.Object, service, clock, unit.Object, new GetTicketQueryHandler(queries.Object));
        var result = await handler.HandleAsync(0, new("Customer is impacted", "Alex"), default);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(clock.UtcNow.AddHours(4), ticket.DueAtUtc);
        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Equal(originalDue, result.Escalation.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, result.Escalation.ToDueAtUtc);
        Assert.Equal(TicketPriority.Medium, result.Escalation.FromPriority);
        Assert.Equal("Alex", result.Escalation.EscalatedBy);
        unit.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.Open)]
    [InlineData(TicketPriority.Low, TicketStatus.Resolved)]
    [InlineData(TicketPriority.Low, TicketStatus.Closed)]
    public void RejectsIneligibleState(TicketPriority priority, TicketStatus status)
    {
        var ticket = Ticket.Raise("TCK-0001", "Title", "Description", 1, 1, priority, clock.UtcNow);
        ticket.ChangeStatus(status, clock.UtcNow);
        Assert.Throws<SupportDesk.Domain.Exceptions.BusinessRuleViolationException>(ticket.EnsureCanEscalate);
    }
}
