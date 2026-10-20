using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
namespace SupportDesk.UnitTests.Integration;

public sealed class DeskFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", "IntegrationFixture-Only-Never-Use-In-Production");
        builder.UseSetting("ConnectionStrings:Default", "Server=unused;Database=unused");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.RemoveAll<SupportDbContext>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(connection));
        });
    }
    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var agent = new Agent("Alex", "alex@example.com", 8, now);
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, "LocalFixture-Password!"));
        var inactive = new Agent("Inactive", "inactive@example.com", 8, now);
        inactive.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(inactive, "LocalFixture-Password!")); inactive.Deactivate();
        db.AddRange(agent, inactive, new Customer("Contoso", "test@example.com", null, CustomerTier.Premium, now), new Category("General", false, false));
        await db.SaveChangesAsync();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}

public class DeskApiTests
{
    [Fact]
    public async Task RealMiddlewareProtectsEveryEndpointAndLoginFailuresAreGeneric()
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        await factory.SeedAsync();
        foreach (var path in new[] { "/api/tickets", "/api/tickets/1", "/api/customers", "/api/agents", "/api/categories", "/api/tickets/1/escalations", "/api/auth/me" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/tickets", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/tickets/1/escalate", new { reason = "Cannot access" })).StatusCode);
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = "alex@example.com", password = "wrong" });
        var missing = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = "wrong" });
        var inactive = await client.PostAsJsonAsync("/api/auth/login", new { email = "inactive@example.com", password = "LocalFixture-Password!" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        var failure = await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(failure!.Detail, (await missing.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Detail);
        Assert.Equal(failure.Detail, (await inactive.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())!.Detail);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alex@example.com", password = "LocalFixture-Password!" });
        var session = await login.Content.ReadFromJsonAsync<LoginResult>();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets?priority=999")).StatusCode);
        var issuer = new SupportDesk.Presentation.Security.JwtTokenIssuer(
            new SupportDesk.Presentation.Security.JwtSettings { SigningKey = "IntegrationFixture-Only-Never-Use-In-Production" },
            new SupportDesk.UnitTests.TestDoubles.FixedClock(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(session.User).Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task CreateEscalateAndHistoryUseTheAuthenticatedActor()
    {
        using var factory = new DeskFactory(); using var client = factory.CreateClient(); await factory.SeedAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alex@example.com", password = "LocalFixture-Password!" });
        var session = (await login.Content.ReadFromJsonAsync<LoginResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var created = await client.PostAsJsonAsync("/api/tickets", new { title = "Portal unavailable", description = "Users cannot access the portal", customerId = 1, categoryId = 1, requestedPriority = "High" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions))!;
        Assert.Equal(TicketPriority.High, ticket.Priority); Assert.NotNull(ticket.AssignedAgent); Assert.NotNull(ticket.Triage);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/escalate", new { reason = "bad" })).StatusCode);
        var result = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/escalate", new { reason = "Customer site is down", escalatedBy = "spoofed" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var history = (await client.GetFromJsonAsync<EscalationDto[]>($"/api/tickets/{ticket.Id}/escalations", JsonOptions))!;
        Assert.Single(history); Assert.Equal("agent:1", history[0].EscalatedBy);
        Assert.Equal(TicketPriority.Critical, history[0].ToPriority);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/escalate", new { reason = "Try again" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/tickets/999/escalations")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var agent = await db.Set<Agent>().SingleAsync(a => a.Id == 1);
            db.Entry(agent).Property(a => a.MaxOpenTickets).CurrentValue = 1;
            await db.SaveChangesAsync();
        }
        var unassignedResponse = await client.PostAsJsonAsync("/api/tickets", new { title = "Second portal issue", description = "Another user cannot access", customerId = 1, categoryId = 1, requestedPriority = "Medium" });
        Assert.Equal(HttpStatusCode.Created, unassignedResponse.StatusCode);
        var unassigned = (await unassignedResponse.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions))!;
        Assert.Null(unassigned.AssignedAgent);
        Assert.Contains("No active eligible", unassigned.Triage!.AssignmentReason);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
    [Fact]
    public async Task FailedHistoryInsertRollsBackTheTicketChange()
    {
        using var factory = new DeskFactory(); using var client = factory.CreateClient(); await factory.SeedAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alex@example.com", password = "LocalFixture-Password!" });
        var session = (await login.Content.ReadFromJsonAsync<LoginResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var response = await client.PostAsJsonAsync("/api/tickets", new { title = "Portal failure", description = "All users are affected", customerId = 1, categoryId = 1, requestedPriority = "High" });
        var original = (await response.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_history BEFORE INSERT ON TicketEscalations BEGIN SELECT RAISE(ABORT, 'injected history failure'); END;");
        }
        var failed = await client.PostAsJsonAsync($"/api/tickets/{original.Id}/escalate", new { reason = "Customer is impacted" });
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var unchanged = (await client.GetFromJsonAsync<TicketDetailDto>($"/api/tickets/{original.Id}", JsonOptions))!;
        Assert.Equal(original.Priority, unchanged.Priority);
        Assert.Equal(original.DueAtUtc, unchanged.DueAtUtc);
        Assert.Empty((await client.GetFromJsonAsync<EscalationDto[]>($"/api/tickets/{original.Id}/escalations", JsonOptions))!);
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}
