using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Catalog;

/// <summary>Οι μεταβολές του καταλόγου δεν διαγράφουν ιστορικά δεδομένα.</summary>
public enum CatalogChangeAction
{
    Create,
    Update,
    Activate,
    Deactivate
}

/// <summary>Ο τύπος και η επιχείρηση δεν αλλάζουν μετά τη δημιουργία του είδους.</summary>
public enum CatalogManagedEntity
{
    Category,
    Product,
    Service
}

/// <summary>Μόνο τα μη φορολογικά πεδία μπορούν να μεταβληθούν από το Back Office.</summary>
public sealed record CatalogChangeCommand(
    CatalogManagedEntity Entity,
    CatalogChangeAction Action,
    Guid? Id = null,
    string? Code = null,
    string? Name = null,
    Guid? CategoryId = null,
    bool TracksStock = false);

public enum CatalogChangeStatus
{
    Saved,
    Denied,
    Missing,
    Invalid,
    Duplicate
}

public sealed record CatalogChangeResult(CatalogChangeStatus Status, Guid? Id = null,
    string? Error = null);

/// <summary>Σύνοψη μόνο για επεξεργασία· ο server ανακτά ξανά όλες τις ιδιότητες.</summary>
public sealed record CatalogManageRecord(
    Guid Id, string Code, string Name, bool IsActive,
    Guid? CategoryId = null, CatalogItemKind? Kind = null, bool TracksStock = false);

public sealed record CatalogCategoryOption(Guid Id, string Code, string Name);

public sealed record CatalogManageDetails(
    CatalogManageRecord? Record, IReadOnlyList<CatalogCategoryOption> Categories,
    bool HasAdditionalCategories = false);

public sealed record CatalogManageResult(bool IsAuthorized, CatalogManageDetails? Details)
{
    public static CatalogManageResult Denied => new(false, null);
}

/// <summary>Προσφέρει αποκλειστικά tenant-scoped δημιουργία, ενημέρωση και soft activation.</summary>
public interface ICatalogWriteStore
{
    Task<CatalogChangeResult> ApplyAsync(Guid tenantId, CatalogChangeCommand command,
        CancellationToken cancellationToken = default);

    Task<CatalogManageDetails> GetDetailsAsync(Guid tenantId,
        CatalogManagedEntity entity, Guid? id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Επιβάλλει το δικαίωμα Manage πριν από οποιαδήποτε πρόσβαση στο write store.
/// Ο κατάλογος παραμένει ανεξάρτητος από τιμές, ΦΠΑ και διαβίβαση παραστατικών.
/// </summary>
public sealed class CatalogWriteService(
    TenantAuthorizationService authorization, ICatalogWriteStore store)
{
    public async Task<CatalogChangeResult> ApplyAsync(Guid userId, Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await CanManageAsync(userId, tenantId, cancellationToken))
        {
            return new(CatalogChangeStatus.Denied);
        }

        if (!Enum.IsDefined(command.Entity) || !Enum.IsDefined(command.Action) ||
            (command.Action == CatalogChangeAction.Create && command.Id.HasValue) ||
            (command.Action != CatalogChangeAction.Create &&
             (!command.Id.HasValue || command.Id == Guid.Empty)) ||
            (command.Action is CatalogChangeAction.Create or CatalogChangeAction.Update &&
             (command.Code is null || command.Name is null)) ||
            (command.Entity == CatalogManagedEntity.Category &&
             (command.CategoryId.HasValue || command.TracksStock)) ||
            (command.Entity == CatalogManagedEntity.Service && command.TracksStock))
        {
            return new(CatalogChangeStatus.Invalid);
        }

        try
        {
            return await store.ApplyAsync(tenantId, command, cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            return new(CatalogChangeStatus.Invalid, Error: exception.Code);
        }
    }

    public async Task<CatalogManageResult> GetDetailsAsync(Guid userId, Guid tenantId,
        CatalogManagedEntity entity, Guid? id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await CanManageAsync(userId, tenantId, cancellationToken))
        {
            return CatalogManageResult.Denied;
        }

        if (!Enum.IsDefined(entity) || id == Guid.Empty)
        {
            return new(true, null);
        }

        return new(true, await store.GetDetailsAsync(tenantId, entity, id, cancellationToken));
    }

    private async Task<bool> CanManageAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
        {
            return false;
        }

        TenantAccessResult access = await authorization.CheckAsync(
            userId, tenantId, null, TenantPermission.Manage, cancellationToken);
        return access.Status == TenantAccessStatus.Granted;
    }
}
