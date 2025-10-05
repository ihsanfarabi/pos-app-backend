namespace PosApp.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount)
{
    public bool HasPagination => PageSize > 0;
}
