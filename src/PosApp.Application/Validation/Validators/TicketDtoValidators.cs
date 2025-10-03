using FluentValidation;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validation.Validators;

public class AddLineDtoValidator : AbstractValidator<AddLineDto>
{
    public AddLineDtoValidator()
    {
        RuleFor(x => x.MenuItemId)
            .NotEmpty();

        RuleFor(x => x.Qty)
            .GreaterThan(0);
    }
}

public class TicketListQueryDtoValidator : AbstractValidator<TicketListQueryDto>
{
    public TicketListQueryDtoValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize.HasValue);
    }
}
