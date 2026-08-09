namespace WorkManagementSystem.Application.Common;

public sealed class PagedResult<T>
{
    public PagedResult(int total, int page, int size, IReadOnlyList<T> data)
    {
        Total = total;
        Page = page;
        Size = size;
        Data = data;
    }

    public int Total { get; }
    public int Page { get; }
    public int Size { get; }
    public IReadOnlyList<T> Data { get; }
}
