using FluentValidation;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validation.Validators;

public class CreateMenuItemDtoValidator : AbstractValidator<CreateMenuItemDto>
{
    public CreateMenuItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0);
    }
}

public class UpdateMenuItemDtoValidator : AbstractValidator<UpdateMenuItemDto>
{
    public UpdateMenuItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0);
    }
}

public class MenuQueryDtoValidator : AbstractValidator<MenuQueryDto>
{
    public MenuQueryDtoValidator()
    {
        RuleFor(x => x.Q)
            .MaximumLength(200);

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize.HasValue);
    }
}
