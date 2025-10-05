using MediatR;
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
        var group = routes.MapGroup("/api/tickets")
            .RequireAuthorization();

        group.MapPost(string.Empty, CreateTicketAsync);
        group.MapGet("/{id:guid}", GetTicketAsync);
        group.MapGet(string.Empty, GetTicketsAsync);
        group.MapPost("/{id:guid}/lines", AddTicketLineAsync);
        group.MapPost("/{id:guid}/pay/cash", PayTicketCashAsync);

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

    private static async Task<Results<Created<TicketLineCreatedResponse>, NotFound, ProblemHttpResult>> AddTicketLineAsync(
        Guid id,
        AddLineDto dto,
        [AsParameters] TicketServices services,
        CancellationToken cancellationToken)
    {
        await services.Sender.Send(new AddTicketLineCommand(id, dto), cancellationToken);
        return TypedResults.Created($"/api/tickets/{id}", new TicketLineCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketPaymentResponse>, NotFound, ProblemHttpResult>> PayTicketCashAsync(
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
