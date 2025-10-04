using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record CreateMenuItemCommand(CreateMenuItemDto Dto) : IRequest<Guid>;

internal sealed class CreateMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<CreateMenuItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateMenuItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var menuItem = MenuItem.Create(request.Dto.Name, request.Dto.Price);
            await repository.AddAsync(menuItem, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return menuItem.Id;
        }
        catch (DomainException ex)
        {
            throw new ValidationException(ex.Message, ex.PropertyName);
        }
    }
}
