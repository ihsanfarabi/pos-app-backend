using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using PosApp.Api.Extensions;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Application.Features.Menu;
using PosApp.Application.Features.Menu.Commands;
using PosApp.Application.Features.Menu.Queries;

namespace PosApp.Api.Endpoints;

public static class MenuEndpoints
{
    public static RouteGroupBuilder MapMenuEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/menu")
            .RequireAuthorization();

        group.MapGet(string.Empty, GetMenuItemsAsync);

        group.MapPost(string.Empty, CreateMenuItemAsync)
            .RequireAuthorization("Admin");

        group.MapPut("/{id:guid}", UpdateMenuItemAsync)
            .RequireAuthorization("Admin");

        group.MapDelete("/{id:guid}", DeleteMenuItemAsync)
            .RequireAuthorization("Admin");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<MenuItemResponse>>, Ok<MenuItemsPageResponse>, ValidationProblem>> GetMenuItemsAsync(
        [AsParameters] MenuQueryDto query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new GetMenuItemsQuery(query), cancellationToken);
            if (result.Pagination is null)
            {
                return TypedResults.Ok(result.Items);
            }

            var pagination = result.Pagination;
            var response = new MenuItemsPageResponse(
                result.Items,
                pagination.Page,
                pagination.PageSize,
                pagination.Total);

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

    private static async Task<Results<Created<MenuItemCreatedResponse>, ValidationProblem>> CreateMenuItemAsync(
        ISender sender,
        CreateMenuItemDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await sender.Send(new CreateMenuItemCommand(dto), cancellationToken);
            var response = new MenuItemCreatedResponse(id);
            return TypedResults.Created($"/api/menu/{id}", response);
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

    private static async Task<Results<Ok<MenuItemUpdatedResponse>, NotFound, ValidationProblem>> UpdateMenuItemAsync(
        Guid id,
        UpdateMenuItemDto dto,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new UpdateMenuItemCommand(id, dto), cancellationToken);
            return TypedResults.Ok(new MenuItemUpdatedResponse(id));
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

    private static async Task<Results<NoContent, NotFound>> DeleteMenuItemAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new DeleteMenuItemCommand(id), cancellationToken);
            return TypedResults.NoContent();
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    

    private sealed record MenuItemsPageResponse(
        IReadOnlyList<MenuItemResponse> Items,
        int Page,
        int PageSize,
        int Total);

    private sealed record MenuItemCreatedResponse(Guid Id);

    private sealed record MenuItemUpdatedResponse(Guid Id);
}
