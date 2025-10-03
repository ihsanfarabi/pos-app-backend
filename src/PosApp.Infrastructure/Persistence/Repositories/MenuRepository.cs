using Microsoft.EntityFrameworkCore;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Domain.Entities;

namespace PosApp.Infrastructure.Persistence.Repositories;

public sealed class MenuRepository(AppDbContext dbContext) : IMenuRepository
{
    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Menu.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MenuItem>> SearchAsync(string? term, CancellationToken cancellationToken)
    {
        var query = dbContext.Menu.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLower();
            query = query.Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{normalized}%"));
        }

        query = query.OrderBy(m => m.Name);
        var items = await query.ToListAsync(cancellationToken);
        return items;
    }

    public async Task<PagedResult<MenuItem>> GetPagedAsync(string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Menu.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = term.Trim().ToLower();
            query = query.Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{normalized}%"));
        }

        query = query.OrderBy(m => m.Name);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MenuItem>(items, page, pageSize, total);
    }

    public async Task AddAsync(MenuItem menuItem, CancellationToken cancellationToken)
    {
        await dbContext.Menu.AddAsync(menuItem, cancellationToken);
    }

    public Task RemoveAsync(MenuItem menuItem, CancellationToken cancellationToken)
    {
        dbContext.Menu.Remove(menuItem);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<Guid, MenuItem>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, MenuItem>();
        }

        var items = await dbContext.Menu
            .AsNoTracking()
            .Where(m => idList.Contains(m.Id))
            .ToListAsync(cancellationToken);
        return items.ToDictionary(m => m.Id);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
