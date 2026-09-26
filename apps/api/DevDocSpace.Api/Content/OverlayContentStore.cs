using DevDocSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Content;

// Serves portal-authored content from the database, falling back to the file store.
// Database rows win over files at the same key, so admins can override ingested content and revert by deleting the row.
public class OverlayContentStore(AppDbContext db, FileSystemContentStore files) : IContentStore
{
    public async Task<IReadOnlyList<string>> ListNamespacesAsync(CancellationToken ct = default)
    {
        var fromFiles = await files.ListNamespacesAsync(ct);
        var fromDb = await db.DocPages.Select(p => p.Namespace).Distinct().ToListAsync(ct);
        return fromFiles.Union(fromDb).Order().ToList();
    }

    public async Task<IReadOnlyList<DocEntry>> GetDocTreeAsync(string ns, CancellationToken ct = default)
    {
        var tree = await files.GetDocTreeAsync(ns, ct);
        var dbPaths = await db.DocPages.Where(p => p.Namespace == ns).Select(p => p.Path).ToListAsync(ct);
        return dbPaths.Count == 0 ? tree : DocTreeMerge.Merge(tree, dbPaths);
    }

    public async Task<string?> GetDocAsync(string ns, string path, CancellationToken ct = default)
    {
        var key = NormalizeDocPath(path);
        var candidates = new[] { key, key == "" ? "index" : key + "/index" };
        var pages = await db.DocPages.Where(p => p.Namespace == ns && candidates.Contains(p.Path)).ToListAsync(ct);
        var page = pages.FirstOrDefault(p => p.Path == candidates[0]) ?? pages.FirstOrDefault();
        return page?.Markdown ?? await files.GetDocAsync(ns, path, ct);
    }

    public Task<IReadOnlyList<SpecEntry>> ListSpecsAsync(CancellationToken ct = default) => files.ListSpecsAsync(ct);

    public async Task<string?> GetSpecAsync(string service, string version, CancellationToken ct = default)
    {
        var content = await db.ApiSpecs
            .Where(s => s.Service == service && s.Version == version)
            .Select(s => s.Content)
            .SingleOrDefaultAsync(ct);
        return content ?? await files.GetSpecAsync(service, version, ct);
    }

    public static string NormalizeDocPath(string path)
    {
        var trimmed = path.Trim('/');
        foreach (var ext in new[] { ".mdx", ".md" })
            if (trimmed.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return trimmed[..^ext.Length];
        return trimmed;
    }
}

internal static class DocTreeMerge
{
    private sealed class Node(string path, string title, bool isDirectory)
    {
        public string Path { get; } = path;
        public string Title { get; } = title;
        public bool IsDirectory { get; } = isDirectory;
        public List<Node> Children { get; } = [];
    }

    // Inserts database page paths into a file tree, adding directory nodes as needed and skipping pages already present.
    public static IReadOnlyList<DocEntry> Merge(IReadOnlyList<DocEntry> tree, IEnumerable<string> pagePaths)
    {
        var root = tree.Select(ToNode).ToList();
        foreach (var pagePath in pagePaths)
        {
            var segments = pagePath.Split('/');
            var level = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var dirPath = string.Join('/', segments[..(i + 1)]);
                var dir = level.FirstOrDefault(n => n.IsDirectory && n.Path == dirPath);
                if (dir is null)
                {
                    dir = new Node(dirPath, segments[i], true);
                    level.Add(dir);
                }
                level = dir.Children;
            }
            if (!level.Any(n => !n.IsDirectory && n.Path == pagePath))
                level.Add(new Node(pagePath, segments[^1], false));
        }
        return ToEntries(root);
    }

    private static Node ToNode(DocEntry e)
    {
        var node = new Node(e.Path, e.Title, e.IsDirectory);
        node.Children.AddRange(e.Children.Select(ToNode));
        return node;
    }

    // Matches FileSystemContentStore ordering: directories first, then pages, each sorted by path.
    private static List<DocEntry> ToEntries(List<Node> nodes) =>
        nodes.OrderBy(n => n.IsDirectory ? 0 : 1).ThenBy(n => n.Path, StringComparer.Ordinal)
            .Select(n => new DocEntry(n.Path, n.Title, n.IsDirectory, ToEntries(n.Children)))
            .ToList();
}
