using PosApp.Application.Common;
using PosApp.Application.Contracts;

namespace PosApp.Application.Features.Menu;

public interface IMenuService
{
    Task<MenuListResult> GetAsync(MenuQueryDto query, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(CreateMenuItemDto dto, CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, UpdateMenuItemDto dto, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
