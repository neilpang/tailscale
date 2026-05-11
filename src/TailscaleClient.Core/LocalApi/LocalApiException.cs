using System.Net;

namespace TailscaleClient.Core.LocalApi;

public sealed class LocalApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public LocalApiException(HttpStatusCode statusCode, string? body, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }
}
