using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Exceptions;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record DeleteMenuItemCommand(Guid Id) : IRequest;

internal sealed class DeleteMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<DeleteMenuItemCommand>
{
    public async Task Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (menuItem is null)
        {
            throw new NotFoundException("MenuItem", request.Id.ToString());
        }

        await repository.RemoveAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
