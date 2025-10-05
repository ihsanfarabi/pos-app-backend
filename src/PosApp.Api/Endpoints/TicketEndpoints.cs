using MediatR;
using FluentValidation;
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

    private static async Task<Results<Created<TicketCreatedResponse>, ValidationProblem>> CreateTicketAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await sender.Send(new CreateTicketCommand(), cancellationToken);
            return TypedResults.Created($"/api/tickets/{id}", new TicketCreatedResponse(id));
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<Ok<TicketDetailsResponse>, NotFound, ValidationProblem>> GetTicketAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await sender.Send(new GetTicketDetailsQuery(id), cancellationToken);
            return ticket is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(ticket);
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<Ok<TicketListPageResponse>, ValidationProblem>> GetTicketsAsync(
        [AsParameters] TicketListQueryDto query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new GetTicketsQuery(query), cancellationToken);
            var response = new TicketListPageResponse(
                result.Items,
                result.Pagination.Page,
                result.Pagination.PageSize,
                result.Pagination.Total);

            return TypedResults.Ok(response);
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<Created<TicketLineCreatedResponse>, ValidationProblem, NotFound>> AddTicketLineAsync(
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
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Results<Ok<TicketPaymentResponse>, ValidationProblem, NotFound>> PayTicketCashAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await sender.Send(new PayTicketCashCommand(id), cancellationToken);
            return TypedResults.Ok(payment);
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
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
