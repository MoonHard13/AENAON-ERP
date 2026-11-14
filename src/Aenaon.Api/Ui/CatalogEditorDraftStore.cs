using Aenaon.Domain.Catalog;
using Microsoft.Extensions.Caching.Memory;

namespace Aenaon.Api.Ui;

/// <summary>Προσωρινή επαναφορά μη μυστικών πεδίων φόρμας, με δέσμευση στον χρήστη και στον Tenant.</summary>
public sealed record CatalogEditorDraft(
    Guid UserId, Guid TenantId, Guid? Id, string View,
    string Code, string Name, Guid? CategoryId, Guid? ParentCategoryId,
    bool TracksStock, bool AutoCode, string ErrorCode, Guid? StoreId = null,
    CatalogUnitOfMeasure? UnitOfMeasure = null,
    CatalogAccountingType? AccountingType = null,
    string? Barcode = null, string? LongDescription = null);

public sealed class CatalogEditorDraftStore : IDisposable
{
    // Η ξεχωριστή cache έχει αυστηρό όριο για να μην αυξάνεται απεριόριστα η μνήμη.
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 500 });

    public void Dispose() => cache.Dispose();
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

    /// <summary>Επαναφέρει με ασφάλεια μόνο πρόχειρο προϊόντος ή υπηρεσίας του ίδιου χρήστη/tenant.</summary>
    public CatalogEditorDraft? ReadCategoryReturn(Guid token, Guid userId, Guid tenantId)
    {
        if (token == Guid.Empty ||
            !cache.TryGetValue("catalog-editor:" + token.ToString("N"),
                out CatalogEditorDraft? draft) || draft is null)
        {
            return null;
        }

        return draft.UserId == userId && draft.TenantId == tenantId &&
            draft.View is ("products" or "services") ? draft : null;
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
