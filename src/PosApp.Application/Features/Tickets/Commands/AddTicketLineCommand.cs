using FluentValidation;
using MediatR;
using PosApp.Application.Abstractions.Idempotency;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Tickets.Commands;

public sealed record AddTicketLineCommand(Guid TicketId, AddLineDto Dto) : IIdempotentRequest<Unit>;

public sealed class AddTicketLineCommandValidator : AbstractValidator<AddTicketLineCommand>
{
    public AddTicketLineCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.MenuItemId).NotEmpty();
        RuleFor(x => x.Dto.Qty).GreaterThan(0);
    }
}

public class AddTicketLineCommandHandler(
    ITicketRepository ticketRepository,
    IMenuRepository menuRepository)
    : IRequestHandler<AddTicketLineCommand, Unit>
{
    public async Task<Unit> Handle(AddTicketLineCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            throw new KeyNotFoundException();
        }

        var menuItem = await menuRepository.GetByIdAsync(request.Dto.MenuItemId, cancellationToken);
        if (menuItem is null)
        {
            throw new DomainException("Menu item not found.", "menuItemId");
        }

        ticket.AddLine(menuItem.Id, menuItem.Price, request.Dto.Qty);
        await ticketRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
