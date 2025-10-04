using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Tickets.Commands;

public sealed record CreateTicketCommand() : IRequest<Guid>;

internal sealed class CreateTicketCommandHandler(ITicketRepository ticketRepository)
    : IRequestHandler<CreateTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = Ticket.Create();
        await ticketRepository.AddAsync(ticket, cancellationToken);
        await ticketRepository.SaveChangesAsync(cancellationToken);
        return ticket.Id;
    }
}
