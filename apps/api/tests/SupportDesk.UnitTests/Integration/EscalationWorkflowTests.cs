using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.Integration;

public class EscalationWorkflowTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("inactive")]
    [InlineData("full")]
    [InlineData("specialization")]
    [InlineData("no-candidate")]
    public async Task EscalationReassignsAnIneligibleOwnerAndPreservesSnapshots(string changedEligibility)
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        await SignInAsync(client);
        var created = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Customer cannot access portal",
            description = "All users are affected",
            customerId = 1,
            categoryId = 1,
            requestedPriority = "Medium"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions))!;
        Assert.Equal(1, original.AssignedAgent!.Id);

        int? replacementId = null;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var owner = await db.Set<Agent>().SingleAsync(agent => agent.Id == 1);
            if (changedEligibility is "inactive" or "no-candidate")
            {
                owner.Deactivate();
            }
            else if (changedEligibility == "full")
            {
                db.Entry(owner).Property(agent => agent.MaxOpenTickets).CurrentValue = 1;
            }
            else
            {
                var category = await db.Set<Category>().SingleAsync();
                db.Entry(category).Property(value => value.RequiresSpecialist).CurrentValue = true;
            }

            if (changedEligibility != "no-candidate")
            {
                var replacement = new Agent("Replacement", "replacement@example.com", 8, DateTime.UtcNow);
                replacement.AddSpecialization(1);
                db.Add(replacement);
                await db.SaveChangesAsync();
                replacementId = replacement.Id;
            }
            else
            {
                await db.SaveChangesAsync();
            }
        }

        var first = await EscalateAsync(client, original.Id, "Customer impact has increased");
        var second = await EscalateAsync(client, original.Id, "Further impact requires escalation");
        Assert.Equal(TicketPriority.High, first.Ticket.Priority);
        Assert.Equal(TicketPriority.Critical, second.Ticket.Priority);
        Assert.Equal(replacementId, first.Ticket.AssignedAgent?.Id);
        Assert.Equal(replacementId, second.Ticket.AssignedAgent?.Id);

        var history = (await client.GetFromJsonAsync<EscalationDto[]>($"/api/tickets/{original.Id}/escalations", JsonOptions))!;
        Assert.Equal(2, history.Length);
        Assert.Equal(second.Escalation.Id, history[0].Id);
        Assert.Equal(first.Escalation.Id, history[1].Id);
        Assert.Equal(TicketPriority.Medium, history[1].FromPriority);
        Assert.Equal(TicketPriority.High, history[1].ToPriority);
        Assert.Equal(1, history[1].FromAgentId);
        Assert.Equal(replacementId, history[1].ToAgentId);
        Assert.Equal(original.DueAtUtc, history[1].FromDueAtUtc);
        Assert.Equal(first.Ticket.DueAtUtc, history[1].ToDueAtUtc);
        Assert.Equal(history[1].ToPriority, history[0].FromPriority);
        Assert.Equal(history[1].ToAgentId, history[0].FromAgentId);
        Assert.Equal(history[1].ToDueAtUtc, history[0].FromDueAtUtc);
        Assert.Equal(second.Ticket.DueAtUtc, history[0].ToDueAtUtc);
        Assert.All(history, row => Assert.Equal("agent:1", row.EscalatedBy));
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alex@example.com",
            password = "LocalFixture-Password!"
        });
        var session = (await response.Content.ReadFromJsonAsync<LoginResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
    }

    private static async Task<SupportDesk.Application.Features.Tickets.EscalationResult> EscalateAsync(
        HttpClient client, int id, string reason)
    {
        var response = await client.PostAsJsonAsync($"/api/tickets/{id}/escalate", new { reason });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SupportDesk.Application.Features.Tickets.EscalationResult>(JsonOptions))!;
    }
}
