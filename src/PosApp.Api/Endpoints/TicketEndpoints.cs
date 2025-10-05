using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
        var id = await services.Sender.Send(new CreateTicketCommand(), cancellationToken);
        return TypedResults.Created($"/api/tickets/{id}", new TicketCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketDetailsResponse>, NotFound>> GetTicketAsync(
        Guid id,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        var ticket = await services.Sender.Send(new GetTicketDetailsQuery(id), cancellationToken);
        return ticket is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ticket);
    }

    private static async Task<Ok<PaginatedItems<TicketListItemResponse>>> GetTicketsAsync(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] TicketServices services,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var queryDto = new TicketListQueryDto(paginationRequest.PageIndex, paginationRequest.PageSize);
        var result = await services.Sender.Send(new GetTicketsQuery(queryDto), cancellationToken);
        var response = new PaginatedItems<TicketListItemResponse>(
            result.PageIndex,
            result.PageSize,
            result.TotalCount,
            result.Items);
        httpContext.Response.AddPaginationHeaders(result.PageIndex, result.PageSize, result.TotalCount);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<TicketLineCreatedResponse>, ProblemHttpResult>> AddTicketLineAsync(
        Guid id,
        AddLineDto dto,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        await services.Sender.Send(new AddTicketLineCommand(id, dto), cancellationToken);
        return TypedResults.Created($"/api/tickets/{id}", new TicketLineCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketPaymentResponse>, ProblemHttpResult>> PayTicketCashAsync(
        Guid id,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        var payment = await services.Sender.Send(new PayTicketCashCommand(id), cancellationToken);
        return TypedResults.Ok(payment);
    }

    private sealed record TicketCreatedResponse(Guid Id);

    private sealed record TicketLineCreatedResponse(Guid TicketId);
}
