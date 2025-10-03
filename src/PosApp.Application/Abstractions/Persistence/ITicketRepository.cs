using PosApp.Application.Common;
using PosApp.Domain.Entities;

namespace PosApp.Application.Abstractions.Persistence;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Ticket?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Ticket>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);

    Task<decimal> GetTotalAsync(Guid ticketId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
