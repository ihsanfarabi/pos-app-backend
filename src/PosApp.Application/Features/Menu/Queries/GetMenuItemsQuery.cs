using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Menu.Queries;

public sealed record GetMenuItemsQuery(MenuQueryDto Query) : IRequest<MenuListResult>;

internal sealed class GetMenuItemsQueryHandler(IMenuRepository repository)
    : IRequestHandler<GetMenuItemsQuery, MenuListResult>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<MenuListResult> Handle(GetMenuItemsQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        if (query.Page is null && query.PageSize is null)
        {
            var items = await repository.SearchAsync(query.Q, cancellationToken);
            var responseItems = items
                .Select(Map)
                .ToList();
            return MenuListResult.FromItems(responseItems);
        }

        var page = query.Page ?? 1;
        var pageSize = NormalizePageSize(query.PageSize);

        var pagedItems = await repository.GetPagedAsync(query.Q, page, pageSize, cancellationToken);
        var mappedItems = pagedItems.Items.Select(Map).ToList();
        var result = new PagedResult<MenuItemResponse>(mappedItems, pagedItems.Page, pagedItems.PageSize, pagedItems.Total);
        return MenuListResult.FromPagedResult(result);
    }

    private static MenuItemResponse Map(MenuItem menuItem) => new(menuItem.Id, menuItem.Name, menuItem.Price);

    private static int NormalizePageSize(int? requested)
    {
        if (!requested.HasValue)
        {
            return DefaultPageSize;
        }

        var value = requested.Value;
        if (value <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.", "pageSize");
        }

        return Math.Min(value, MaxPageSize);
    }
}
