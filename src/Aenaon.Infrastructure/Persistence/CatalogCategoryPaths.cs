using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Οικονομική ανάκτηση διαδρομών με batched tenant-scoped αναζητήσεις γονέων.</summary>
internal static class CatalogCategoryPaths
{
    private const int MaxDepth = 64;

    internal static async Task<Dictionary<Guid, string>> ResolveAsync(
        AenaonDbContext context, Guid tenantId,
        IEnumerable<(Guid Id, Guid? ParentId, string Name)> selected,
        CancellationToken cancellationToken = default)
    {
        var nodes = selected.ToDictionary(x => x.Id,
            x => new Node(x.Id, x.ParentId, x.Name));
        Guid[] ids = nodes.Keys.ToArray();
        var missing = nodes.Values.Where(x => x.ParentId.HasValue)
            .Select(x => x.ParentId!.Value).ToHashSet();
        for (int depth = 0; depth < MaxDepth && missing.Count > 0; depth++)
        {
            missing.ExceptWith(nodes.Keys);
            if (missing.Count == 0)
            {
                break;
            }

            List<Node> ancestors = await context.CatalogCategories.AsNoTracking()
                .Where(x => x.TenantId == tenantId && missing.Contains(x.Id))
                .Select(x => new Node(x.Id, x.ParentCategoryId, x.Name))
                .ToListAsync(cancellationToken);
            missing.Clear();
            foreach (Node node in ancestors)
            {
                nodes.TryAdd(node.Id, node);
                if (node.ParentId is { } parent && !nodes.ContainsKey(parent))
                {
                    missing.Add(parent);
                }
            }
        }

        var paths = new Dictionary<Guid, string>(ids.Length);
        foreach (Guid id in ids)
        {
            var parts = new Stack<string>();
            var seen = new HashSet<Guid>();
            Node? node = nodes[id];
            while (node is not null && parts.Count < MaxDepth && seen.Add(node.Id))
            {
                parts.Push(node.Name);
                node = node.ParentId is { } parent && nodes.TryGetValue(parent, out Node? found)
                    ? found : null;
            }

            paths[id] = string.Join(" / ", parts);
        }

        return paths;
    }

    private sealed record Node(Guid Id, Guid? ParentId, string Name);
}
