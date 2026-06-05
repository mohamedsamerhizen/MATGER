namespace MATGER.Api.Helpers;

public static class PaginationHelper
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(
        int page,
        int pageSize,
        int defaultPage = DefaultPage,
        int maxPageSize = MaxPageSize)
    {
        return (
            Math.Max(page, defaultPage),
            Math.Clamp(pageSize, 1, maxPageSize));
    }
}