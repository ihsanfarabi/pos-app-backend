using MediatR;
using PosApp.Application.Abstractions.Persistence;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record DeleteMenuItemCommand(Guid Id) : IRequest<bool>;

internal sealed class DeleteMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<DeleteMenuItemCommand, bool>
{
    public async Task<bool> Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (menuItem is null)
        {
            return false;
        }

        await repository.RemoveAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
