using Microsoft.AspNetCore.Http.HttpResults;
using PosApp.Api.Contracts;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Tickets;
using PosApp.Application.Features.Tickets.Commands;
using PosApp.Application.Features.Tickets.Queries;
using PosApp.Api.Services;
using PosApp.Api.Extensions;

namespace PosApp.Api.Endpoints;

public static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this IEndpointRouteBuilder routes)
    {
        var versionedApi = routes.NewVersionedApi("Tickets");
        var group = versionedApi
            .MapGroup("/api/tickets")
            .HasApiVersion(1, 0)
            .RequireAuthorization()
            .WithTags("Tickets");

        group.MapPost(string.Empty, CreateTicketAsync)
            .WithName("CreateTicket")
            .WithSummary("Create ticket")
            .WithDescription("Create a new ticket for the current user session.")
            .Produces<TicketCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetTicketAsync)
            .WithName("GetTicket")
            .WithSummary("Get ticket")
            .WithDescription("Get ticket details by identifier.")
            .Produces<TicketDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(string.Empty, GetTicketsAsync)
            .WithName("ListTickets")
            .WithSummary("List tickets")
            .WithDescription("Retrieve a paginated list of tickets.")
            .Produces<PaginatedItems<TicketListItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/lines", AddTicketLineAsync)
            .WithName("AddTicketLine")
            .WithSummary("Add ticket line")
            .WithDescription("Add a line item to a ticket.")
            .Produces<TicketLineCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/pay/cash", PayTicketCashAsync)
            .WithName("PayTicketCash")
            .WithSummary("Pay ticket with cash")
            .WithDescription("Record a cash payment for the specified ticket.")
            .Produces<TicketPaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<TicketCreatedResponse>, ProblemHttpResult>> CreateTicketAsync(
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Creating ticket");

        var id = await services.Sender.Send(new CreateTicketCommand(), cancellationToken);

        services.Logger.LogInformation("Created ticket {TicketId}", id);

        return TypedResults.Created($"/api/tickets/{id}", new TicketCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketDetailsResponse>, NotFound>> GetTicketAsync(
        Guid id,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Retrieving ticket {TicketId}", id);

        var ticket = await services.Sender.Send(new GetTicketDetailsQuery(id), cancellationToken);
        if (ticket is null)
        {
            services.Logger.LogWarning("Ticket {TicketId} not found", id);
            return TypedResults.NotFound();
        }

        services.Logger.LogInformation("Found ticket {TicketId}", id);
        return TypedResults.Ok(ticket);
    }

    private static async Task<Ok<PaginatedItems<TicketListItemResponse>>> GetTicketsAsync(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] TicketServices services,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation(
            "Listing tickets page {PageIndex} size {PageSize}",
            paginationRequest.PageIndex,
            paginationRequest.PageSize);

        var queryDto = new TicketListQueryDto(paginationRequest.PageIndex, paginationRequest.PageSize);
        var result = await services.Sender.Send(new GetTicketsQuery(queryDto), cancellationToken);
        var response = new PaginatedItems<TicketListItemResponse>(
            result.PageIndex,
            result.PageSize,
            result.TotalCount,
            result.Items);
        httpContext.Response.AddPaginationHeaders(result.PageIndex, result.PageSize, result.TotalCount);

        services.Logger.LogInformation(
            "Returning {ItemCount} tickets (total {TotalCount})",
            result.Items.Count,
            result.TotalCount);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<TicketLineCreatedResponse>, ProblemHttpResult>> AddTicketLineAsync(
        Guid id,
        AddLineDto dto,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation(
            "Adding line to ticket {TicketId} with menu item {MenuItemId} qty {Quantity}",
            id,
            dto.MenuItemId,
            dto.Qty);

        await services.Sender.Send(new AddTicketLineCommand(id, dto), cancellationToken);

        services.Logger.LogInformation("Added line to ticket {TicketId}", id);

        return TypedResults.Created($"/api/tickets/{id}", new TicketLineCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketPaymentResponse>, ProblemHttpResult>> PayTicketCashAsync(
        Guid id,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Processing cash payment for ticket {TicketId}", id);

        var payment = await services.Sender.Send(new PayTicketCashCommand(id), cancellationToken);

        services.Logger.LogInformation("Processed cash payment for ticket {TicketId}", id);

        return TypedResults.Ok(payment);
    }

    private sealed record TicketCreatedResponse(Guid Id);

    private sealed record TicketLineCreatedResponse(Guid TicketId);
}
