using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Exceptions;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Tickets.Commands;

public sealed record PayTicketCashCommand(Guid TicketId) : IRequest<TicketPaymentResponse>;

internal sealed class PayTicketCashCommandHandler(ITicketRepository ticketRepository)
    : IRequestHandler<PayTicketCashCommand, TicketPaymentResponse>
{
    public async Task<TicketPaymentResponse> Handle(PayTicketCashCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", request.TicketId.ToString());
        }

        ticket.PayCash();
        await ticketRepository.SaveChangesAsync(cancellationToken);

        var total = ticket.GetTotal();
        return new TicketPaymentResponse(ticket.Id, ticket.Status.ToString(), total);
    }
}
