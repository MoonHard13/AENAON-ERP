using Aenaon.Application.Menus;

namespace Aenaon.Api.Tests;

/// <summary>Σταθερά δεδομένα για HTTP security/SSR assertions, όχι πραγματική δοκιμή βάσης.</summary>
internal sealed class SessionMenuDraftFixture : IMenuDraftStore
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    public static readonly Guid MenuId = Guid.Parse("e1d21dcb-4bbf-4b88-a403-0c0a19540b68");
    public static readonly Guid CategoryId = Guid.Parse("4e4ac1d7-5a9c-49d7-a1bd-67e6f0d7c2a8");
    public static readonly Guid ProductId = Guid.Parse("519f1811-6215-4841-81d2-62e921b22e3d");
    public static readonly Guid SecondaryCategoryId = Guid.Parse("31377943-6f5a-4d9f-b238-cf4e0bc323e6");
    public static readonly Guid SecondaryProductId = Guid.Parse("dcfa55da-480f-4bb8-9a23-8b22be790ed9");
    public static readonly Guid SourceCategoryId = Guid.Parse("9d75fab2-d478-4c7c-a3bb-84911d19a54b");
    public static readonly Guid OtherSourceCategoryId = Guid.Parse("618eb187-d012-4441-812a-a5d4df4634c6");

    public int ApplyCalls { get; private set; }
    public MenuDraftCommand? LastCommand { get; private set; }
    public Guid LastTenant { get; private set; }
    public int ReadCalls { get; private set; }
    public bool EmptyDraft { get; set; }
    public bool EmptyCategory { get; set; }
    public bool ExtraCategory { get; set; }
    public bool ExtraItem { get; set; }
    public bool NestedCategory { get; set; }
    public bool RootItem { get; set; }

    public Task<MenuDraftMutationResult> ApplyAsync(Guid tenantId, Guid userId,
        MenuDraftCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastTenant = tenantId;
        ApplyCalls++;
        LastCommand = command;
        return Task.FromResult(new MenuDraftMutationResult(
            MenuDraftStatus.Saved, MenuId, MenuId, 2));
    }

    public Task<IReadOnlyList<MenuDraftSummary>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ReadCalls++;
        return Task.FromResult<IReadOnlyList<MenuDraftSummary>>(
            tenantId == Tenant
                ? [new MenuDraftSummary(MenuId, "MAIN", "Κυρίως", 1,
                    new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc))]
                : []);
    }

    public Task<Guid?> GetDefaultMenuIdAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(tenantId == Tenant ? MenuId : null);

    public Task<MenuDraftDetails?> GetAsync(Guid tenantId, Guid menuId,
        CancellationToken cancellationToken = default)
    {
        MenuDraftDetails? value = tenantId == Tenant && menuId == MenuId
            ? new(
                new(MenuId, "MAIN", "Κυρίως", 1,
                    new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc)),
                EmptyDraft ? [] : ExtraCategory || NestedCategory
                    ? [new MenuDraftCategoryRecord(CategoryId, "Ορεκτικά", 1, true),
                        new MenuDraftCategoryRecord(SecondaryCategoryId, "Ποτά",
                            NestedCategory ? 1 : 2, false,
                            NestedCategory ? CategoryId : null)]
                    : [new MenuDraftCategoryRecord(CategoryId, "Ορεκτικά", 1, true)],
                EmptyDraft ? [] : RootItem
                    ? [new MenuDraftItemRecord(Guid.NewGuid(), CategoryId, ProductId,
                            "SKU001", "Πίτσα", null, true, 1),
                        new MenuDraftItemRecord(Guid.NewGuid(), null, SecondaryProductId,
                            "SKU002", "Σαλάτα", null, true, 1)]
                    : EmptyCategory ? [] : ExtraItem
                    ? [new MenuDraftItemRecord(Guid.NewGuid(), CategoryId, ProductId,
                            "SKU001", "Πίτσα", null, true, 1),
                        new MenuDraftItemRecord(Guid.NewGuid(), CategoryId, SecondaryProductId,
                            "SKU002", "Σαλάτα", null, true, 2)]
                    : [new MenuDraftItemRecord(Guid.NewGuid(), CategoryId, ProductId,
                        "SKU001", "Πίτσα", null, true, 1)])
            : null;
        return Task.FromResult(value);
    }

    public Task<IReadOnlyList<MenuCatalogCategoryOption>> ListCatalogCategoriesAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MenuCatalogCategoryOption>>(
            tenantId == Tenant
                ? [new(SourceCategoryId, "FOOD", "Φαγητά", null),
                    new(OtherSourceCategoryId, "DRINK", "Ροφήματα", null)]
                : []);

    public Task<IReadOnlyList<MenuCatalogOption>> SearchCatalogAsync(Guid tenantId,
        string? search, CancellationToken cancellationToken = default,
        Guid? catalogCategoryId = null)
    {
        IReadOnlyList<MenuCatalogOption> items =
            tenantId == Tenant &&
            (!catalogCategoryId.HasValue || catalogCategoryId == SourceCategoryId) &&
            (string.IsNullOrWhiteSpace(search) ||
             "Πίτσα".StartsWith(search, StringComparison.OrdinalIgnoreCase) ||
             "SKU001".StartsWith(search, StringComparison.OrdinalIgnoreCase))
                ? [new MenuCatalogOption(ProductId, "SKU001", "Πίτσα")]
                : [];
        return Task.FromResult(items);
    }
}
