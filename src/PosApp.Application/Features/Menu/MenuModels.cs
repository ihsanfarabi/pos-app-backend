using PosApp.Application.Common;

namespace PosApp.Application.Features.Menu;

public sealed record MenuItemResponse(Guid Id, string Name, decimal Price);

public sealed record MenuListResult(IReadOnlyList<MenuItemResponse> Items, PaginationMetadata? Pagination)
{
    public static MenuListResult FromPagedResult(PagedResult<MenuItemResponse> result)
    {
        var metadata = new PaginationMetadata(result.Page, result.PageSize, result.Total);
        return new MenuListResult(result.Items, metadata);
    }

    public static MenuListResult FromItems(IReadOnlyList<MenuItemResponse> items)
    {
        return new MenuListResult(items, null);
    }
}
