using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Menu;

public sealed class MenuService(IMenuRepository repository) : IMenuService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<MenuListResult> GetAsync(MenuQueryDto query, CancellationToken cancellationToken)
    {
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

    public async Task<Guid> CreateAsync(CreateMenuItemDto dto, CancellationToken cancellationToken)
    {
        var menuItem = new MenuItem
        {
            Name = dto.Name.Trim(),
            Price = dto.Price
        };

        await repository.AddAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return menuItem.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateMenuItemDto dto, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(id, cancellationToken);
        if (menuItem is null)
        {
            throw new NotFoundException("MenuItem", id.ToString());
        }

        menuItem.Name = dto.Name.Trim();
        menuItem.Price = dto.Price;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(id, cancellationToken);
        if (menuItem is null)
        {
            return false;
        }

        await repository.RemoveAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
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
