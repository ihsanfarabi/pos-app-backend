using FluentValidation;
using PosApp.Application.Contracts;

namespace PosApp.Application.Validations;

public class AddLineDtoValidator : AbstractValidator<AddLineDto>
{
    public AddLineDtoValidator()
    {
        RuleFor(x => x.MenuItemId)
            .NotEmpty().WithErrorCode("NotEmpty");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithErrorCode("GreaterThan");
    }
}

public class TicketListQueryDtoValidator : AbstractValidator<TicketListQueryDto>
{
    public TicketListQueryDtoValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithErrorCode("GreaterThanOrEqualTo");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithErrorCode("GreaterThan")
            .LessThanOrEqualTo(100).WithErrorCode("LessThanOrEqualTo");
    }
}
