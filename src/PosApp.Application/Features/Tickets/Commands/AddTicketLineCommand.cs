using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Tickets.Commands;

public sealed record AddTicketLineCommand(Guid TicketId, AddLineDto Dto) : IRequest;

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

internal sealed class AddTicketLineCommandHandler(
    ITicketRepository ticketRepository,
    IMenuRepository menuRepository)
    : IRequestHandler<AddTicketLineCommand>
{
    public async Task Handle(AddTicketLineCommand request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", request.TicketId.ToString());
        }

        var menuItem = await menuRepository.GetByIdAsync(request.Dto.MenuItemId, cancellationToken);
        if (menuItem is null)
        {
            throw new DomainException("Menu item not found.", "menuItemId");
        }

        ticket.AddLine(menuItem.Id, menuItem.Price, request.Dto.Qty);
        await ticketRepository.SaveChangesAsync(cancellationToken);
    }
}
