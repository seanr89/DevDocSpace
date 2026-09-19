namespace DevDocSpace.Api.Content;

public record DocEntry(string Path, string Title, bool IsDirectory, IReadOnlyList<DocEntry> Children);

public record SpecEntry(string Service, string Version, string Path);

public interface IContentStore
{
    Task<IReadOnlyList<string>> ListNamespacesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocEntry>> GetDocTreeAsync(string ns, CancellationToken ct = default);
    Task<string?> GetDocAsync(string ns, string path, CancellationToken ct = default);
    Task<IReadOnlyList<SpecEntry>> ListSpecsAsync(CancellationToken ct = default);
    Task<string?> GetSpecAsync(string service, string version, CancellationToken ct = default);
}
