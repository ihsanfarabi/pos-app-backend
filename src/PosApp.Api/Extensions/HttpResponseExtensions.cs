using System.Globalization;

namespace PosApp.Api.Extensions;

public static class HttpResponseExtensions
{
    public static void AddPaginationHeaders(this HttpResponse response, int pageIndex, int pageSize, int totalCount)
    {
        response.Headers["X-Page-Index"] = pageIndex.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Total-Count"] = totalCount.ToString(CultureInfo.InvariantCulture);
    }
}


