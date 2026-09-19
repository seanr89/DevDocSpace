using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Content;

public class ContentOptions
{
    public const string Section = "Content";
    public string RootPath { get; set; } = "content";
}

public class FileSystemContentStore(IOptions<ContentOptions> options, IHostEnvironment env) : IContentStore
{
    private static readonly string[] DocExtensions = [".md", ".mdx"];

    private readonly string _root = Path.GetFullPath(
        Path.IsPathRooted(options.Value.RootPath)
            ? options.Value.RootPath
            : Path.Combine(env.ContentRootPath, options.Value.RootPath));

    private string DocsRoot => Path.Combine(_root, "docs");
    private string SpecsRoot => Path.Combine(_root, "specs");

    public Task<IReadOnlyList<string>> ListNamespacesAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(DocsRoot)) return Task.FromResult<IReadOnlyList<string>>([]);
        var result = Directory.GetDirectories(DocsRoot).Select(Path.GetFileName).OfType<string>().Order().ToList();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    public Task<IReadOnlyList<DocEntry>> GetDocTreeAsync(string ns, CancellationToken ct = default)
    {
        var dir = SafeJoin(DocsRoot, ns);
        if (dir is null || !Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<DocEntry>>([]);
        return Task.FromResult<IReadOnlyList<DocEntry>>(BuildTree(dir, dir));
    }

    public async Task<string?> GetDocAsync(string ns, string path, CancellationToken ct = default)
    {
        var dir = SafeJoin(DocsRoot, ns);
        if (dir is null) return null;

        var candidates = Path.HasExtension(path)
            ? [path]
            : DocExtensions.Select(ext => path + ext)
                .Concat(DocExtensions.Select(ext => Path.Combine(path, "index" + ext)));

        foreach (var candidate in candidates)
        {
            var file = SafeJoin(dir, candidate);
            if (file is not null && File.Exists(file) && DocExtensions.Contains(Path.GetExtension(file)))
                return await File.ReadAllTextAsync(file, ct);
        }
        return null;
    }

    public Task<IReadOnlyList<SpecEntry>> ListSpecsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(SpecsRoot)) return Task.FromResult<IReadOnlyList<SpecEntry>>([]);
        var result = new List<SpecEntry>();
        foreach (var serviceDir in Directory.GetDirectories(SpecsRoot))
        {
            var service = Path.GetFileName(serviceDir);
            foreach (var versionDir in Directory.GetDirectories(serviceDir))
            {
                var file = Path.Combine(versionDir, "openapi.json");
                if (File.Exists(file))
                    result.Add(new SpecEntry(service, Path.GetFileName(versionDir), Path.GetRelativePath(_root, file)));
            }
        }
        return Task.FromResult<IReadOnlyList<SpecEntry>>(result.OrderBy(s => s.Service).ThenBy(s => s.Version).ToList());
    }

    public async Task<string?> GetSpecAsync(string service, string version, CancellationToken ct = default)
    {
        var file = SafeJoin(SpecsRoot, Path.Combine(service, version, "openapi.json"));
        if (file is null || !File.Exists(file)) return null;
        return await File.ReadAllTextAsync(file, ct);
    }

    private static string? SafeJoin(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSep, StringComparison.Ordinal) ? full : null;
    }

    private static List<DocEntry> BuildTree(string nsRoot, string dir)
    {
        var entries = new List<DocEntry>();
        foreach (var sub in Directory.GetDirectories(dir).Order())
        {
            var children = BuildTree(nsRoot, sub);
            if (children.Count > 0)
                entries.Add(new DocEntry(Rel(nsRoot, sub), Path.GetFileName(sub), true, children));
        }
        foreach (var file in Directory.GetFiles(dir).Order())
        {
            if (!DocExtensions.Contains(Path.GetExtension(file))) continue;
            var rel = Rel(nsRoot, Path.ChangeExtension(file, null));
            entries.Add(new DocEntry(rel, Path.GetFileNameWithoutExtension(file), false, []));
        }
        return entries;
    }

    private static string Rel(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
