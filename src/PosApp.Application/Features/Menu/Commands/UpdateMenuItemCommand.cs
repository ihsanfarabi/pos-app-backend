using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Exceptions;
using FluentValidation;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record UpdateMenuItemCommand(Guid Id, UpdateMenuItemDto Dto) : IRequest;

public sealed class UpdateMenuItemCommandValidator : AbstractValidator<UpdateMenuItemCommand>
{
    public UpdateMenuItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Name)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.Dto.Price)
            .GreaterThan(0);
    }
}

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

        menuItem.Update(request.Dto.Name, request.Dto.Price);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
