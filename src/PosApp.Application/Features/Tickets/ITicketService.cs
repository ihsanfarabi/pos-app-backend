using PosApp.Application.Contracts;

namespace PosApp.Application.Features.Tickets;

public interface ITicketService
{
    Task<Guid> CreateAsync(CancellationToken cancellationToken);

    Task<TicketDetailsResponse?> GetAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<TicketListResult> GetListAsync(TicketListQueryDto query, CancellationToken cancellationToken);

    Task AddLineAsync(Guid ticketId, AddLineDto dto, CancellationToken cancellationToken);

    Task<TicketPaymentResponse> PayCashAsync(Guid ticketId, CancellationToken cancellationToken);
}
