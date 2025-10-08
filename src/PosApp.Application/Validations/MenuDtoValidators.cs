using FluentValidation;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validations;

public class CreateMenuItemDtoValidator : AbstractValidator<CreateMenuItemDto>
{
    public CreateMenuItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("NotEmpty")
            .MaximumLength(200).WithErrorCode("MaxLength");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithErrorCode("GreaterThan");
    }
}

public class UpdateMenuItemDtoValidator : AbstractValidator<UpdateMenuItemDto>
{
    public UpdateMenuItemDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("NotEmpty")
            .MaximumLength(200).WithErrorCode("MaxLength");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithErrorCode("GreaterThan");
    }
}

public class MenuQueryDtoValidator : AbstractValidator<MenuQueryDto>
{
    public MenuQueryDtoValidator()
    {
        RuleFor(x => x.Q)
            .MaximumLength(200).WithErrorCode("MaxLength");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithErrorCode("GreaterThanOrEqualTo");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithErrorCode("GreaterThan")
            .LessThanOrEqualTo(100).WithErrorCode("LessThanOrEqualTo");
    }
}
