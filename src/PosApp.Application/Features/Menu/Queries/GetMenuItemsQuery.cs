using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Menu.Queries;

public sealed record GetMenuItemsQuery(MenuQueryDto Query) : IRequest<PagedResult<MenuItemResponse>>;

public sealed class GetMenuItemsQueryValidator : AbstractValidator<GetMenuItemsQuery>
{
    public GetMenuItemsQueryValidator()
    {
        RuleFor(x => x.Query.Q)
            .MaximumLength(200).WithErrorCode("MaxLength");

        RuleFor(x => x.Query.PageIndex)
            .GreaterThanOrEqualTo(0).WithErrorCode("GreaterThanOrEqualTo");

        RuleFor(x => x.Query.PageSize)
            .GreaterThan(0).WithErrorCode("GreaterThan")
            .LessThanOrEqualTo(100).WithErrorCode("LessThanOrEqualTo");
    }
}

public class GetMenuItemsQueryHandler(IMenuRepository repository)
    : IRequestHandler<GetMenuItemsQuery, PagedResult<MenuItemResponse>>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<PagedResult<MenuItemResponse>> Handle(GetMenuItemsQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var pageIndex = Math.Max(0, query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);

        var pagedItems = await repository.GetPagedAsync(query.Q, pageIndex, pageSize, cancellationToken);
        var mappedItems = pagedItems.Items.Select(Map).ToList();
        return new PagedResult<MenuItemResponse>(mappedItems, pagedItems.PageIndex, pagedItems.PageSize, pagedItems.TotalCount);
    }

    private static MenuItemResponse Map(MenuItem menuItem) => new(menuItem.Id, menuItem.Name, menuItem.Price);

    private static int NormalizePageSize(int requested)
    {
        if (requested <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(requested, MaxPageSize);
    }
}
