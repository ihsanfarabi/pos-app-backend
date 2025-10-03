namespace PosApp.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public bool HasPagination => PageSize > 0;
}
