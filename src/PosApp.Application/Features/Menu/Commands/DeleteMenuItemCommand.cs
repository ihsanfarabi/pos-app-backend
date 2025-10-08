using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record DeleteMenuItemCommand(Guid Id) : IRequest;

public sealed class DeleteMenuItemCommandValidator : AbstractValidator<DeleteMenuItemCommand>
{
    public DeleteMenuItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<DeleteMenuItemCommand>
{
    public async Task Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (menuItem is null)
        {
            throw new KeyNotFoundException();
        }

        await repository.RemoveAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
