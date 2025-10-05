using Microsoft.EntityFrameworkCore;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Domain.Entities;

namespace PosApp.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository(AppDbContext dbContext) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Ticket?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Tickets
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Ticket>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Tickets
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Ticket>(items, pageIndex, pageSize, total);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        await dbContext.Tickets.AddAsync(ticket, cancellationToken);
    }

    public async Task<decimal> GetTotalAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var total = await dbContext.TicketLines
            .Where(l => l.TicketId == ticketId)
            .SumAsync(l => l.Qty * l.UnitPrice, cancellationToken);
        return total;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
