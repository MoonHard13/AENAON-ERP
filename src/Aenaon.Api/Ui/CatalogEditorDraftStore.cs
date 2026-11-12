using Microsoft.Extensions.Caching.Memory;

namespace Aenaon.Api.Ui;

/// <summary>Προσωρινή επαναφορά μη μυστικών πεδίων φόρμας, με δέσμευση στον χρήστη και στον Tenant.</summary>
public sealed record CatalogEditorDraft(
    Guid UserId, Guid TenantId, Guid? Id, string View,
    string Code, string Name, Guid? CategoryId, Guid? ParentCategoryId,
    bool TracksStock, bool AutoCode, string ErrorCode);

public sealed class CatalogEditorDraftStore(IMemoryCache cache)
{
    public Guid Save(CatalogEditorDraft draft)
    {
        Guid token = Guid.NewGuid();
        cache.Set("catalog-editor:" + token.ToString("N"), draft,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                Size = 1
            });
        return token;
    }

    public CatalogEditorDraft? Read(Guid token, Guid userId, Guid tenantId,
        Guid? id, string view)
    {
        if (token == Guid.Empty ||
            !cache.TryGetValue("catalog-editor:" + token.ToString("N"),
                out CatalogEditorDraft? value) || value is null)
        {
            return null;
        }

        return value.UserId == userId && value.TenantId == tenantId &&
            value.Id == id && string.Equals(value.View, view, StringComparison.Ordinal)
            ? value : null;
    }
}
