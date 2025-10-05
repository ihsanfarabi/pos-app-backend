namespace PosApp.Api.Contracts;

public sealed class PaginatedItems<T>(int pageIndex, int pageSize, long count, IEnumerable<T> data)
{
    public int PageIndex { get; } = pageIndex;

    public int PageSize { get; } = pageSize;

    public long Count { get; } = count;

    public IEnumerable<T> Data { get; } = data;
}
