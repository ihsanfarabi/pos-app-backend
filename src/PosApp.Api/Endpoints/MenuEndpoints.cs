using MediatR;
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

        group.MapGet(string.Empty, GetMenuItemsAsync)
            .WithValidator<MenuQueryDto>();

        group.MapPost(string.Empty, CreateMenuItemAsync)
            .WithValidator<CreateMenuItemDto>()
            .RequireAuthorization("Admin");

        group.MapPut("/{id:guid}", UpdateMenuItemAsync)
            .WithValidator<UpdateMenuItemDto>()
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
        catch (ValidationException ex)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<Created<MenuItemCreatedResponse>> CreateMenuItemAsync(
        ISender sender,
        CreateMenuItemDto dto,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(new CreateMenuItemCommand(dto), cancellationToken);
        var response = new MenuItemCreatedResponse(id);
        return TypedResults.Created($"/api/menu/{id}", response);
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
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeleteMenuItemAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteMenuItemCommand(id), cancellationToken);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }

    private static ValidationProblem ValidationProblem(ValidationException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.PropertyName)
            ? "error"
            : char.ToLowerInvariant(exception.PropertyName[0]) + exception.PropertyName[1..];

        var payload = new Dictionary<string, string[]>
        {
            [key] = new[] { exception.Message }
        };

        return TypedResults.ValidationProblem(payload);
    }

    private sealed record MenuItemsPageResponse(
        IReadOnlyList<MenuItemResponse> Items,
        int Page,
        int PageSize,
        int Total);

    private sealed record MenuItemCreatedResponse(Guid Id);

    private sealed record MenuItemUpdatedResponse(Guid Id);
}
