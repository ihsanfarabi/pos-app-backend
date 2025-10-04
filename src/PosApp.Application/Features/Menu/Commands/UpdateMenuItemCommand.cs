using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record UpdateMenuItemCommand(Guid Id, UpdateMenuItemDto Dto) : IRequest;

internal sealed class UpdateMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<UpdateMenuItemCommand>
{
    public async Task Handle(UpdateMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (menuItem is null)
        {
            throw new NotFoundException("MenuItem", request.Id.ToString());
        }

        try
        {
            menuItem.Update(request.Dto.Name, request.Dto.Price);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            throw new ValidationException(ex.Message, ex.PropertyName);
        }
    }
}
