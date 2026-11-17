using Aenaon.Application.Tenancy;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Menus;

/// <summary>Οι ίδιες ενέργειες χρησιμοποιούνται από CLI, Blazor και μελλοντικό API.</summary>
public enum MenuDraftAction
{
    CreateMenu = 1,
    UpdateMenu = 2,
    AddCategory = 3,
    UpdateCategory = 4,
    MoveCategory = 5,
    RemoveCategory = 6,
    AddItem = 7,
    RenameItem = 8,
    MoveItem = 9,
    RemoveItem = 10,
    ImportCatalogContents = 11,
    EnsureStoreDefault = 12
}

public enum MenuDraftStatus
{
    Saved = 1,
    Denied = 2,
    Missing = 3,
    Invalid = 4,
    Duplicate = 5,
    Conflict = 6
}

/// <summary>Ατομική εντολή μεταβολής με mandatory idempotency και expected revision.</summary>
public sealed record MenuDraftCommand(
    MenuDraftAction Action,
    Guid RequestId,
    Guid? MenuId = null,
    long? ExpectedRevision = null,
    Guid? CategoryId = null,
    Guid? ItemId = null,
    Guid? CatalogItemId = null,
    string? Code = null,
    string? Name = null,
    string? NameOverride = null,
    int? TargetPosition = null,
    bool? IsVisible = null,
    Guid? StoreId = null);

public sealed record MenuDraftMutationResult(
    MenuDraftStatus Status, Guid? RecordId = null, Guid? MenuId = null,
    long? Revision = null, string? Error = null);

public sealed record MenuDraftSummary(
    Guid MenuId, string Code, string Name, long Revision, DateTime EditedUtc);

public sealed record MenuDraftCategoryRecord(
    Guid CategoryId, string Name, int Position, bool IsVisible);

public sealed record MenuDraftItemRecord(
    Guid ItemId, Guid CategoryId, Guid CatalogItemId, string CatalogCode,
    string CatalogName, string? NameOverride, bool IsCatalogActive, int Position);

public sealed record MenuCatalogOption(Guid CatalogItemId, string Code, string Name);

public sealed record MenuDraftDetails(
    MenuDraftSummary Menu,
    IReadOnlyList<MenuDraftCategoryRecord> Categories,
    IReadOnlyList<MenuDraftItemRecord> Items);

public sealed record MenuDraftReadResult(
    bool IsAuthorized,
    IReadOnlyList<MenuDraftSummary> Menus,
    MenuDraftDetails? Details,
    IReadOnlyList<MenuCatalogOption> Catalog)
{
    public static MenuDraftReadResult Denied => new(false, [], null, []);
}

/// <summary>Αποθηκευτικός μηχανισμός που επιβάλλει επιπλέον SQL Tenant constraints.</summary>
public interface IMenuDraftStore
{
    Task<MenuDraftMutationResult> ApplyAsync(Guid tenantId, Guid userId,
        MenuDraftCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuDraftSummary>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetDefaultMenuIdAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default);

    Task<MenuDraftDetails?> GetAsync(Guid tenantId, Guid menuId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuCatalogOption>> SearchCatalogAsync(Guid tenantId,
        string? search, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ελέγχει υποχρεωτικά τα πραγματικά δικαιώματα του χρήστη πριν από SQL ή CLI.
/// Δεν εκδίδει φορολογικά παραστατικά ούτε μεταβάλλει ERP Catalog και τιμοκαταλόγους.
/// </summary>
public sealed class MenuDraftManagementService(
    TenantAuthorizationService authorization,
    IMenuDraftStore store)
{
    public async Task<MenuDraftMutationResult> ApplyAsync(Guid userId, Guid tenantId,
        MenuDraftCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TenantAccessResult access = await authorization.CheckAsync(
            userId, tenantId,
            command.Action == MenuDraftAction.EnsureStoreDefault ? command.StoreId : null,
            TenantPermission.Manage, cancellationToken);
        if (access.Status != TenantAccessStatus.Granted)
        {
            return new(MenuDraftStatus.Denied);
        }

        if (command.RequestId == Guid.Empty || !Enum.IsDefined(command.Action) ||
            (command.Action is MenuDraftAction.CreateMenu or MenuDraftAction.EnsureStoreDefault &&
                (command.MenuId.HasValue || command.ExpectedRevision.HasValue)) ||
            (command.Action == MenuDraftAction.EnsureStoreDefault &&
                command.StoreId is not { } store || store == Guid.Empty) ||
            (command.Action is not (MenuDraftAction.CreateMenu or MenuDraftAction.EnsureStoreDefault) &&
                (command.MenuId is not { } menuId || menuId == Guid.Empty ||
                 command.ExpectedRevision is not > 0)) ||
            (command.Action == MenuDraftAction.CreateMenu &&
                (command.Code is null || command.Name is null)) ||
            (command.Action == MenuDraftAction.UpdateMenu && command.Name is null) ||
            (command.Action is MenuDraftAction.AddCategory or MenuDraftAction.UpdateCategory &&
                command.Name is null) ||
            (command.Action is MenuDraftAction.UpdateCategory or MenuDraftAction.MoveCategory or
                MenuDraftAction.RemoveCategory or MenuDraftAction.AddItem or
                MenuDraftAction.RenameItem or MenuDraftAction.MoveItem or MenuDraftAction.RemoveItem &&
                (command.CategoryId is not { } catId || catId == Guid.Empty)) ||
            (command.Action is MenuDraftAction.RenameItem or MenuDraftAction.MoveItem or
                MenuDraftAction.RemoveItem &&
                (command.ItemId is not { } itemId || itemId == Guid.Empty)) ||
            (command.Action == MenuDraftAction.AddItem &&
                (command.CatalogItemId is not { } catalogId || catalogId == Guid.Empty)) ||
            (command.Action is MenuDraftAction.MoveItem or MenuDraftAction.MoveCategory &&
                command.TargetPosition is not > 0) ||
            (command.Action == MenuDraftAction.ImportCatalogContents &&
                (command.CategoryId.HasValue || command.ItemId.HasValue ||
                 command.CatalogItemId.HasValue || command.TargetPosition.HasValue)) ||
            (command.Action is MenuDraftAction.UpdateCategory &&
                command.IsVisible is null))
        {
            return new(MenuDraftStatus.Invalid, Error: "menu.command.invalid");
        }

        try
        {
            MenuDraftMutationResult result = await store.ApplyAsync(tenantId, userId,
                command, cancellationToken);
            return result;
        }
        catch (DomainRuleException ex)
        {
            return new(MenuDraftStatus.Invalid, Error: ex.Code);
        }
    }

    public async Task<MenuDraftReadResult> ReadAsync(Guid userId, Guid tenantId,
        Guid? menuId, string? catalogSearch, CancellationToken cancellationToken = default,
        Guid? storeId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TenantAccessResult access = await authorization.CheckAsync(
            userId, tenantId, storeId, TenantPermission.Read, cancellationToken);
        if (access.Status != TenantAccessStatus.Granted)
        {
            return MenuDraftReadResult.Denied;
        }

        IReadOnlyList<MenuDraftSummary> menus = await store.ListAsync(tenantId, cancellationToken);
        Guid? preferred = storeId.HasValue
            ? await store.GetDefaultMenuIdAsync(tenantId, storeId.Value, cancellationToken)
            : null;
        Guid? selected = menuId ?? preferred ?? menus.FirstOrDefault()?.MenuId;
        MenuDraftDetails? details = selected.HasValue
            ? await store.GetAsync(tenantId, selected.Value, cancellationToken)
            : null;
        IReadOnlyList<MenuCatalogOption> catalog =
            await store.SearchCatalogAsync(tenantId, catalogSearch, cancellationToken);
        return new(true, menus, details, catalog);
    }
}
