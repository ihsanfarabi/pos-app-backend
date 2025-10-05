using FluentValidation;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validation;

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
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100);
    }
}
