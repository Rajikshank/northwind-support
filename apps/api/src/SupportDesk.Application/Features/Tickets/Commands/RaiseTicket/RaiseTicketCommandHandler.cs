using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger,
    TriageService triage)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        // Capacity reads must remain protected until the selected assignment is saved.
        var result = await unitOfWork.ExecuteTicketWriteAsync(token => CreateAsync(request, token), ct);

        // Report success only after commit, not from an attempt that might be rolled back.
        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            result.Reference, request.CustomerId, request.CategoryId);
        return result;
    }

    private async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            request.RequestedPriority ?? TicketPriority.Medium,
            clock.UtcNow);

        var decision = await triage.ApplyAsync(ticket, ticket.CreatedAtUtc, false, ct, defaultedPriority: request.RequestedPriority is null);
        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var result = await getTicket.HandleAsync(ticket.Id, ct);
        result.Triage = decision;
        return result;
    }
}
