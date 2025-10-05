using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using PosApp.Api.Extensions;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Application.Features.Tickets;
using PosApp.Application.Features.Tickets.Commands;
using PosApp.Application.Features.Tickets.Queries;

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

    private static async Task<Created<TicketCreatedResponse>> CreateTicketAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(new CreateTicketCommand(), cancellationToken);
        return TypedResults.Created($"/api/tickets/{id}", new TicketCreatedResponse(id));
    }

    private static async Task<Results<Ok<TicketDetailsResponse>, NotFound>> GetTicketAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(new GetTicketDetailsQuery(id), cancellationToken);
        return ticket is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ticket);
    }

    private static async Task<Ok<TicketListPageResponse>> GetTicketsAsync(
        [AsParameters] TicketListQueryDto query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTicketsQuery(query), cancellationToken);
        var response = new TicketListPageResponse(
            result.Items,
            result.Pagination.Page,
            result.Pagination.PageSize,
            result.Pagination.Total);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<TicketLineCreatedResponse>, NotFound>> AddTicketLineAsync(
        Guid id,
        AddLineDto dto,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new AddTicketLineCommand(id, dto), cancellationToken);
            return TypedResults.Created($"/api/tickets/{id}", new TicketLineCreatedResponse(true));
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Results<Ok<TicketPaymentResponse>, NotFound>> PayTicketCashAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await sender.Send(new PayTicketCashCommand(id), cancellationToken);
            return TypedResults.Ok(payment);
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private sealed record TicketCreatedResponse(Guid Id);

    private sealed record TicketLineCreatedResponse(bool Ok);

    private sealed record TicketListPageResponse(
        IReadOnlyList<TicketListItemResponse> Items,
        int Page,
        int PageSize,
        int Total);
}
