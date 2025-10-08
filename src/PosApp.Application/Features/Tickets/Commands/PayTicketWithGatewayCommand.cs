using FluentValidation;
using MediatR;
using PosApp.Application.Abstractions.Payments;
using PosApp.Application.Abstractions.Persistence;

namespace PosApp.Application.Features.Tickets.Commands;

public sealed record PayTicketWithGatewayCommand(Guid TicketId, bool ShouldSucceed) : IRequest<TicketPaymentResponse>;

public sealed class PayTicketWithGatewayCommandValidator : AbstractValidator<PayTicketWithGatewayCommand>
{
    public PayTicketWithGatewayCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
    }
}

public sealed class PayTicketWithGatewayCommandHandler(
    ITicketRepository ticketRepository,
    IPaymentGateway paymentGateway)
    : IRequestHandler<PayTicketWithGatewayCommand, TicketPaymentResponse>
{
    public async Task<TicketPaymentResponse> Handle(PayTicketWithGatewayCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            throw new KeyNotFoundException();
        }

        var amount = ticket.GetTotal();
        var chargeResult = await paymentGateway.ChargeAsync(new PaymentChargeRequest(ticket.Id, amount, request.ShouldSucceed), cancellationToken);
        if (!chargeResult.Success)
        {
            throw new InvalidOperationException(chargeResult.Error ?? "Payment declined");
        }

        ticket.PayCash();
        await ticketRepository.SaveChangesAsync(cancellationToken);

        return new TicketPaymentResponse(ticket.Id, ticket.Status.ToString(), amount);
    }
}


