using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;

namespace SupportDesk.UnitTests.Integration;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPPORTDESK_TEST_SQL")))
        {
            Skip = "Set SUPPORTDESK_TEST_SQL to run against an isolated SQL Server database.";
        }
    }
}

public class SqlServerConcurrencyTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    [SqlServerFact]
    public async Task ConcurrentCreatesHaveUniqueReferencesAfterTheSeedRange()
    {
        await using var factory = new SqlDeskFactory();
        using var client = factory.CreateClient();
        await SignInAsync(client);
        factory.Gate.Arm("ORDER BY [t].[Id] DESC", 4);

        var responses = await Task.WhenAll(Enumerable.Range(1, 4).Select(index => CreateAsync(client, index)));
        Assert.True(responses.All(response => response.StatusCode == HttpStatusCode.Created), string.Join("\n", factory.Errors));
        var tickets = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions)));
        Assert.Equal(4, tickets.Select(ticket => ticket!.Reference).Distinct().Count());
        Assert.All(tickets, ticket => Assert.True(int.Parse(ticket!.Reference[4..]) > 40));
    }

    [SqlServerFact]
    public async Task ConcurrentCreatesCannotTakeTheSameLastCapacitySlot()
    {
        await using var factory = new SqlDeskFactory();
        using var client = factory.CreateClient();
        await SignInAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var agents = await db.Set<Agent>().ToListAsync();
            foreach (var agent in agents.Where(agent => agent.Id != 1)) agent.Deactivate();
            var owner = agents.Single(agent => agent.Id == 1);
            var workload = await db.Set<Ticket>().CountAsync(ticket => ticket.AssignedAgentId == 1
                && ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed);
            db.Entry(owner).Property(agent => agent.MaxOpenTickets).CurrentValue = workload + 1;
            await db.SaveChangesAsync();
        }
        factory.Gate.Arm("[a].[MaxOpenTickets]", 4);

        var responses = await Task.WhenAll(Enumerable.Range(1, 4).Select(index => CreateAsync(client, index)));
        Assert.True(responses.All(response => response.StatusCode == HttpStatusCode.Created), string.Join("\n", factory.Errors));
        var tickets = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions)));
        Assert.Single(tickets, ticket => ticket!.AssignedAgent is not null);
        Assert.Equal(3, tickets.Count(ticket => ticket!.AssignedAgent is null));
    }

    [SqlServerFact]
    public async Task SequenceMigrationStartsBeyondExistingReferences()
    {
        await using var factory = new SqlDeskFactory();
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(factory.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261020010300_AlignEscalationTableAndGuardTicketUpdates");
        var seeder = new SupportDbSeeder(db, new SupportDesk.UnitTests.TestDoubles.FixedClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportDbSeeder>.Instance, new SlaPolicy());
        await seeder.SeedAsync();
        db.Add(Ticket.Raise("TCK-9000", "Existing import", "Legacy reference must remain unique", 2, 1,
            TicketPriority.Medium, DateTime.UtcNow));
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert.Equal("TCK-9001", await new TicketRepository(db).NextReferenceAsync(default));
    }

    [SqlServerFact]
    public async Task CreateAndEscalationShareTheSameCapacityBoundary()
    {
        await using var factory = new SqlDeskFactory();
        using var client = factory.CreateClient();
        await SignInAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var agents = await db.Set<Agent>().ToListAsync();
            foreach (var agent in agents.Where(agent => agent.Id != 1)) agent.Deactivate();
            var workload = await db.Set<Ticket>().CountAsync(ticket => ticket.AssignedAgentId == 1
                && ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed);
            db.Entry(agents.Single(agent => agent.Id == 1)).Property(agent => agent.MaxOpenTickets).CurrentValue = workload + 1;
            await db.SaveChangesAsync();
        }
        factory.Gate.Arm("[a].[MaxOpenTickets]", 2);

        var responses = await Task.WhenAll(CreateAsync(client, 1),
            client.PostAsJsonAsync("/api/tickets/23/escalate", new { reason = "Unassigned customer needs urgent help" }));
        Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        var created = (await responses[0].Content.ReadFromJsonAsync<TicketDetailDto>(JsonOptions))!;
        var escalated = (await responses[1].Content.ReadFromJsonAsync<SupportDesk.Application.Features.Tickets.EscalationResult>(JsonOptions))!;
        Assert.Equal(1, new[] { created.AssignedAgent, escalated.Ticket.AssignedAgent }.Count(agent => agent is not null));
    }

    [SqlServerFact]
    public async Task SimultaneousEscalationsRecordEachActualPriorityStep()
    {
        await using var factory = new SqlDeskFactory();
        using var client = factory.CreateClient();
        await SignInAsync(client);
        factory.Gate.Arm("[a].[MaxOpenTickets]", 2);

        var responses = await Task.WhenAll(Enumerable.Range(1, 2).Select(index =>
            client.PostAsJsonAsync("/api/tickets/23/escalate", new { reason = $"Customer impact increased {index}" })));
        Assert.True(responses.All(response => response.StatusCode == HttpStatusCode.OK), string.Join("\n", factory.Errors));
        var history = (await client.GetFromJsonAsync<EscalationDto[]>("/api/tickets/23/escalations", JsonOptions))!;
        Assert.Equal(2, history.Length);
        Assert.Equal(TicketPriority.High, history[0].FromPriority);
        Assert.Equal(TicketPriority.Critical, history[0].ToPriority);
        Assert.Equal(TicketPriority.Medium, history[1].FromPriority);
        Assert.Equal(TicketPriority.High, history[1].ToPriority);
    }

    internal static async Task SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alex.turner@northwind-support.example",
            password = "LocalDemo-Only!2026"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
    }

    internal static Task<HttpResponseMessage> CreateAsync(HttpClient client, int index) =>
        client.PostAsJsonAsync("/api/tickets", new
        {
            title = $"Concurrent request {index}",
            description = "Database isolation regression fixture",
            customerId = 2,
            categoryId = 1,
            requestedPriority = "Medium"
        });
}

internal sealed class SqlDeskFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"NorthwindTest_{Guid.NewGuid():N}";
    public QueryGate Gate { get; } = new();
    public ConcurrentQueue<string> Errors { get; } = new();
    public string ConnectionString => new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("SUPPORTDESK_TEST_SQL"))
    { InitialCatalog = databaseName }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.AddProvider(new FailureLogProvider(Errors)));
        builder.UseSetting("Jwt:SigningKey", "SqlFixture-Only-Never-Use-In-Production-2026");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.RemoveAll<SupportDbContext>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlServer(ConnectionString).AddInterceptors(Gate));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Each test owns a GUID-named database; never reset the developer's SupportDesk database.
        var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class QueryGate : DbCommandInterceptor
{
    private string? fragment;
    private int remaining;
    private TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Arm(string sqlFragment, int participants)
    {
        fragment = sqlFragment;
        remaining = participants;
        release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
    {
        if (fragment is not null && command.CommandText.Contains(fragment, StringComparison.Ordinal))
        {
            // Force overlapping reads on the faulty query instead of relying on scheduler timing.
            if (Interlocked.Decrement(ref remaining) == 0) release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        return result;
    }
}

internal sealed class FailureLogProvider(ConcurrentQueue<string> errors) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FailureLogger(errors);
    public void Dispose() { }

    private sealed class FailureLogger(ConcurrentQueue<string> errors) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (level >= LogLevel.Error && exception is not null) errors.Enqueue(exception.ToString());
        }
    }
}
