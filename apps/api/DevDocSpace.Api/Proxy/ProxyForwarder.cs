using DevDocSpace.Api.Auth;
using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Proxy;

public class ProxyForwarder(IHttpClientFactory httpClientFactory, IOptions<ProxyOptions> options)
{
    private static readonly HashSet<string> StrippedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer",
        "Transfer-Encoding", "Upgrade", "Content-Length", "Origin", "Referer", "Cookie",
        "Authorization", ApiKeyAuthenticationHandler.HeaderName,
    };

    private static readonly HashSet<string> StrippedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "TE", "Trailer", "Transfer-Encoding", "Upgrade",
        "Set-Cookie", "Access-Control-Allow-Origin", "Access-Control-Allow-Credentials",
        "Access-Control-Allow-Headers", "Access-Control-Allow-Methods", "Access-Control-Expose-Headers",
    };

    public static Uri BuildUpstreamUri(string baseUrl, string path, QueryString query)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var target = new Uri(baseUri, path.TrimStart('/'));
        if (!baseUri.IsBaseOf(target)) throw new InvalidOperationException("Path escapes upstream base URL");
        return new UriBuilder(target) { Query = query.HasValue ? query.Value!.TrimStart('?') : "" }.Uri;
    }

    public HttpRequestMessage BuildUpstreamRequest(HttpRequest incoming, Uri target, string? credentialKey)
    {
        var upstream = new HttpRequestMessage(new HttpMethod(incoming.Method), target);

        foreach (var (name, values) in incoming.Headers)
        {
            if (StrippedRequestHeaders.Contains(name)) continue;
            // Content-* headers are rejected here and re-applied on the content below.
            upstream.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)values!);
        }

        if (incoming.ContentLength > 0 || incoming.Headers.ContainsKey("Transfer-Encoding"))
        {
            upstream.Content = new StreamContent(incoming.Body);
            if (incoming.ContentType is not null)
                upstream.Content.Headers.TryAddWithoutValidation("Content-Type", incoming.ContentType);
            if (incoming.ContentLength is { } len)
                upstream.Content.Headers.ContentLength = len;
        }

        if (credentialKey is not null && options.Value.Credentials.TryGetValue(credentialKey, out var cred))
            upstream.Headers.TryAddWithoutValidation(cred.Header, cred.Value);

        return upstream;
    }

    public async Task<int> ForwardAsync(HttpContext context, Uri target, string? credentialKey)
    {
        var client = httpClientFactory.CreateClient(ProxyOptions.HttpClientName);
        using var upstreamRequest = BuildUpstreamRequest(context.Request, target, credentialKey);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            return context.Response.StatusCode;
        }
        catch (HttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return context.Response.StatusCode;
        }

        using (upstreamResponse)
        {
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var (name, values) in upstreamResponse.Headers.Concat(upstreamResponse.Content.Headers))
            {
                if (StrippedResponseHeaders.Contains(name)) continue;
                context.Response.Headers[name] = values.ToArray();
            }
            await upstreamResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        return context.Response.StatusCode;
    }
}
