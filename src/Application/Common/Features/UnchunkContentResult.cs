using Microsoft.AspNetCore.Mvc;
using RestfulHelpers.Common;
using RestfulHelpers.Interface;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Application.Common.Features;

public class UnchunkContentResult(IHttpResult result, Func<JsonDocument?>? postValueWrite = null) : ActionResult
{
    public static UnchunkContentResult Create(HttpResult httpResult)
    {
        return new UnchunkContentResult(httpResult);
    }

    public static UnchunkContentResult Create(HttpResult<JsonDocument> httpResult)
    {
        return new UnchunkContentResult(httpResult, () => httpResult.Value);
    }

    private readonly IHttpResult _result = result;
    private readonly Func<JsonDocument?>? _postValueWrite = postValueWrite;

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;

        response.StatusCode = (int)_result.StatusCode;

        foreach (var header in _result.ResponseHeaders)
        {
            if (header.Key.Equals("transfer-encoding", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
            response.Headers[header.Key] = header.Value.ToArray();
        }

        if (context.HttpContext.Request.Method != HttpMethod.Head.Method &&
            _result.StatusCode != HttpStatusCode.NoContent)
        {
            if (_result.IsSuccess && _postValueWrite != null && _postValueWrite() is JsonDocument postValue)
            {
                await WriteUnchunk(context, postValue.RootElement.ToString());
            }
            else
            {
                await WriteUnchunk(context, JsonSerializer.Serialize(_result, JsonSerializerOptions.Web));
            }
        }
    }

    private static async Task WriteUnchunk(ActionContext context, string content)
    {
        var response = context.HttpContext.Response;

        var contentBytes = Encoding.UTF8.GetBytes(content);

        response.ContentLength = contentBytes.Length;

        if (string.IsNullOrEmpty(response.ContentType))
        {
            response.ContentType = "application/json";
        }

        await response.BodyWriter.WriteAsync(contentBytes.AsMemory(0, contentBytes.Length));
        await response.BodyWriter.FlushAsync();
    }
}
