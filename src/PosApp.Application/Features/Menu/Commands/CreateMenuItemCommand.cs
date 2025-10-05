using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Contracts;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Menu.Commands;

public sealed record CreateMenuItemCommand(CreateMenuItemDto Dto) : IRequest<Guid>;

public sealed class CreateMenuItemCommandValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Name)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.Dto.Price)
            .GreaterThan(0);
    }
}

internal sealed class CreateMenuItemCommandHandler(IMenuRepository repository)
    : IRequestHandler<CreateMenuItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = MenuItem.Create(request.Dto.Name, request.Dto.Price);
        await repository.AddAsync(menuItem, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return menuItem.Id;
    }
}
