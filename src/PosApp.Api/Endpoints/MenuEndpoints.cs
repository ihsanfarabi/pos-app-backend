using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using PosApp.Api.Contracts;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Menu;
using PosApp.Application.Features.Menu.Commands;
using PosApp.Application.Features.Menu.Queries;
using PosApp.Api.Services;
using PosApp.Api.Extensions;

namespace PosApp.Api.Endpoints;

public static class MenuEndpoints
{
    public static RouteGroupBuilder MapMenuEndpoints(this IEndpointRouteBuilder routes)
    {
        var versionedApi = routes.NewVersionedApi("Menu");
        var group = versionedApi
            .MapGroup("/api/menu")
            .HasApiVersion(1, 0)
            .RequireAuthorization()
            .WithTags("Menu");

        group.MapGet(string.Empty, GetMenuItemsAsync)
            .WithName("ListMenuItems")
            .WithSummary("List menu items")
            .WithDescription("Retrieve a paginated list of menu items.")
            .Produces<PaginatedItems<MenuItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost(string.Empty, CreateMenuItemAsync)
            .RequireAuthorization("Admin")
            .WithName("CreateMenuItem")
            .WithSummary("Create menu item")
            .WithDescription("Create a new menu item.")
            .Produces<MenuItemCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdateMenuItemAsync)
            .RequireAuthorization("Admin")
            .WithName("UpdateMenuItem")
            .WithSummary("Update menu item")
            .WithDescription("Update an existing menu item.")
            .Produces<MenuItemUpdatedResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteMenuItemAsync)
            .RequireAuthorization("Admin")
            .WithName("DeleteMenuItem")
            .WithSummary("Delete menu item")
            .WithDescription("Delete an existing menu item.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<PaginatedItems<MenuItemResponse>>> GetMenuItemsAsync(
        [AsParameters] PaginationRequest paginationRequest,
        string? q,
        [AsParameters] MenuServices services,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation(
            "Listing menu items page {PageIndex} size {PageSize} query {Query}",
            paginationRequest.PageIndex,
            paginationRequest.PageSize,
            q ?? string.Empty);

        var queryDto = new MenuQueryDto(q, paginationRequest.PageIndex, paginationRequest.PageSize);
        var result = await services.Sender.Send(new GetMenuItemsQuery(queryDto), cancellationToken);
        httpContext.Response.AddPaginationHeaders(result.PageIndex, result.PageSize, result.TotalCount);
        var response = new PaginatedItems<MenuItemResponse>(
            result.PageIndex,
            result.PageSize,
            result.TotalCount,
            result.Items);

        services.Logger.LogInformation(
            "Returning {ItemCount} menu items (total {TotalCount})",
            result.Items.Count,
            result.TotalCount);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<MenuItemCreatedResponse>, ProblemHttpResult>> CreateMenuItemAsync(
        [AsParameters] MenuServices services,
        CreateMenuItemDto dto,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Creating menu item {MenuItemName}", dto.Name);

        var id = await services.Sender.Send(new CreateMenuItemCommand(dto), cancellationToken);
        var response = new MenuItemCreatedResponse(id);

        services.Logger.LogInformation("Created menu item {MenuItemId}", id);

        return TypedResults.Created($"/api/menu/{id}", response);
    }

    private static async Task<Results<Ok<MenuItemUpdatedResponse>, ProblemHttpResult>> UpdateMenuItemAsync(
        Guid id,
        UpdateMenuItemDto dto,
        [AsParameters] MenuServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Updating menu item {MenuItemId}", id);

        await services.Sender.Send(new UpdateMenuItemCommand(id, dto), cancellationToken);

        services.Logger.LogInformation("Updated menu item {MenuItemId}", id);

        return TypedResults.Ok(new MenuItemUpdatedResponse(id));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteMenuItemAsync(
        Guid id,
        [AsParameters] MenuServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Deleting menu item {MenuItemId}", id);

        await services.Sender.Send(new DeleteMenuItemCommand(id), cancellationToken);

        services.Logger.LogInformation("Deleted menu item {MenuItemId}", id);

        return TypedResults.NoContent();
    }

    private sealed record MenuItemCreatedResponse(Guid Id);

    private sealed record MenuItemUpdatedResponse(Guid Id);
}
