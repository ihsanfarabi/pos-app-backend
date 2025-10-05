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

        group.MapGet(string.Empty, GetMenuItemsAsync);

        group.MapPost(string.Empty, CreateMenuItemAsync)
            .RequireAuthorization("Admin");

        group.MapPut("/{id:guid}", UpdateMenuItemAsync)
            .RequireAuthorization("Admin");

        group.MapDelete("/{id:guid}", DeleteMenuItemAsync)
            .RequireAuthorization("Admin");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<MenuItemResponse>>, Ok<MenuItemsPageResponse>>> GetMenuItemsAsync(
        [AsParameters] MenuQueryDto query,
        ISender sender,
        CancellationToken cancellationToken)
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

    private static async Task<Created<MenuItemCreatedResponse>> CreateMenuItemAsync(
        ISender sender,
        CreateMenuItemDto dto,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(new CreateMenuItemCommand(dto), cancellationToken);
        var response = new MenuItemCreatedResponse(id);
        return TypedResults.Created($"/api/menu/{id}", response);
    }

    private static async Task<Results<Ok<MenuItemUpdatedResponse>, NotFound>> UpdateMenuItemAsync(
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
