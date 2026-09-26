using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevDocSpace.Api.Content;

// Rules for portal-authored content. Keys are restricted to simple slugs so they are safe as URL and file path segments.
public static partial class ContentValidation
{
    public const int MaxSpecBytes = 5 * 1024 * 1024;
    public const int MaxMarkdownBytes = 1024 * 1024;
    private const int MaxPathSegments = 8;

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$")]
    private static partial Regex SlugPattern();

    public static bool IsSlug(string? value) => value is not null && SlugPattern().IsMatch(value);

    // A doc page path relative to its namespace, without extension, e.g. "guides/auth" or "index".
    public static bool IsDocPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var segments = path.Split('/');
        return segments.Length <= MaxPathSegments
               && segments.All(IsSlug)
               && !path.EndsWith(".md", StringComparison.Ordinal)
               && !path.EndsWith(".mdx", StringComparison.Ordinal);
    }

    // Returns an error message, or null when the JSON looks like an OpenAPI/Swagger document.
    public static string? ValidateSpec(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "Spec content is required.";
        if (Encoding.UTF8.GetByteCount(content) > MaxSpecBytes) return $"Spec content exceeds {MaxSpecBytes / (1024 * 1024)} MB.";
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "Spec must be a JSON object.";
            var hasVersion = (root.TryGetProperty("openapi", out var v) || root.TryGetProperty("swagger", out v))
                             && v.ValueKind == JsonValueKind.String;
            if (!hasVersion) return "Spec must have a string \"openapi\" (or \"swagger\") field.";
            if (!root.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object)
                return "Spec must have an \"info\" object.";
            return null;
        }
        catch (JsonException ex)
        {
            return $"Spec is not valid JSON: {ex.Message}";
        }
    }

    public static string? ValidateMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "Markdown content is required.";
        if (Encoding.UTF8.GetByteCount(markdown) > MaxMarkdownBytes) return $"Markdown exceeds {MaxMarkdownBytes / (1024 * 1024)} MB.";
        return null;
    }
}
