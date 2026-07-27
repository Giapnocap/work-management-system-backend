namespace WorkManagementSystem.Application.Common
{
    public static class Paging
    {
        public const int DefaultPage = 1;
        public const int DefaultPageSize = 10;
        public const int DefaultHistoryPageSize = 20;
        public const int MaxPageSize = 100;

        public static PagingQuery Normalize(int page, int size, int defaultSize = DefaultPageSize)
        {
            var normalizedPage = page <= 0 ? DefaultPage : page;
            var fallbackSize = defaultSize <= 0 ? DefaultPageSize : defaultSize;
            var normalizedSize = size <= 0 ? fallbackSize : Math.Min(size, MaxPageSize);

            return new PagingQuery(normalizedPage, normalizedSize);
        }
    }

    public readonly record struct PagingQuery(int Page, int Size);
}
