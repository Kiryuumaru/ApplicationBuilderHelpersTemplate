using Microsoft.AspNetCore.Mvc;
using RestfulHelpers.Common;
using RestfulHelpers.Interface;
using Microsoft.AspNetCore.Http;

namespace Application.Common.Features;

public class CachedWrappedBlobStreamContentResult(HttpResult<Stream> result) : ActionResult
{
    public static CachedWrappedBlobStreamContentResult Create(HttpResult<Stream> result)
    {
        return new CachedWrappedBlobStreamContentResult(result);
    }

    private readonly HttpResult<Stream> _result = result;

    private const int _bufferSize = 1 * 1024 * 1024; // 1MB chunk size

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;

        if (!_result.IsSuccess)
        {
            response.StatusCode = (int)_result.StatusCode;
            if (context.HttpContext.Request.Method == HttpMethod.Get.Method)
            {
                await response.WriteAsJsonAsync<IHttpResult>(_result);
            }
            return;
        }

        var contentType = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("content-type", StringComparison.InvariantCultureIgnoreCase)).Value;
        var contentLength = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("content-length", StringComparison.InvariantCultureIgnoreCase)).Value;
        var cacheControl = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("cache-control", StringComparison.InvariantCultureIgnoreCase)).Value;
        var etag = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("etag", StringComparison.InvariantCultureIgnoreCase)).Value;
        var contentRange = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("content-range", StringComparison.InvariantCultureIgnoreCase)).Value;
        var lastModified = _result.ResponseHeaders.FirstOrDefault(i => i.Key.Equals("last-modified", StringComparison.InvariantCultureIgnoreCase)).Value;

        long? length = null;
        if (contentLength != null && contentLength.Length > 0 && long.TryParse(contentLength[0], out var len))
        {
            length = len;
        }

        response.Headers.CacheControl = cacheControl == null || cacheControl.Length == 0 ? "public, no-cache" : cacheControl;
        response.Headers.AcceptRanges = "bytes";
        response.Headers.ETag = etag;
        response.Headers.LastModified = lastModified;
        response.Headers.ContentType = contentType;
        response.Headers.ContentRange = contentRange;

        if (context.HttpContext.Request.Method == HttpMethod.Head.Method)
        {
            response.Headers.ContentLength = length;
        }

        response.StatusCode = contentRange != null && contentRange.Length != 0 ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;

        if (_result.HasValue)
        {
            using (_result.Value)
            {
                try
                {
                    var buffer = new byte[_bufferSize];
                    int bytesRead;
                    while ((bytesRead = await _result.Value.ReadAsync(buffer)) > 0)
                    {
                        await response.BodyWriter.WriteAsync(buffer.AsMemory(0, bytesRead));
                        await response.BodyWriter.FlushAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    context.HttpContext.Abort();
                }
            }
        }
    }
}
