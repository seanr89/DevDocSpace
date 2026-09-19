namespace DevDocSpace.Api.Proxy;

public class ProxyOptions
{
    public const string Section = "Proxy";
    public const string HttpClientName = "upstream";

    public long MaxRequestBodyBytes { get; set; } = 5 * 1024 * 1024;
    public int TimeoutSeconds { get; set; } = 30;
    public int RateLimitPermitsPerMinute { get; set; } = 60;
    public Dictionary<string, UpstreamCredential> Credentials { get; set; } = new();
}

public class UpstreamCredential
{
    public string Header { get; set; } = "Authorization";
    public string Value { get; set; } = "";
}
