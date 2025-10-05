using PosApp.Application.Common;
using PosApp.Domain.Entities;

namespace PosApp.Application.Abstractions.Persistence;

public interface IMenuRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<MenuItem>> GetPagedAsync(string? term, int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task AddAsync(MenuItem menuItem, CancellationToken cancellationToken);

    Task RemoveAsync(MenuItem menuItem, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, MenuItem>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
