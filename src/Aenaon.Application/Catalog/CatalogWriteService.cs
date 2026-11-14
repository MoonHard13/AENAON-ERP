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
    bool TracksStock = false,
    Guid? ParentCategoryId = null,
    bool AutoCode = false,
    Guid? RequestId = null,
    CatalogUnitOfMeasure? UnitOfMeasure = null,
    CatalogAccountingType? AccountingType = null,
    string? Barcode = null, string? LongDescription = null,
    bool SetBarcode = false, bool SetLongDescription = false);

public enum CatalogChangeStatus
{
    Saved,
    Denied,
    Missing,
    Invalid,
    Duplicate
}

public sealed record CatalogChangeResult(CatalogChangeStatus Status, Guid? Id = null,
    string? Error = null, string? Code = null);

/// <summary>Σύνοψη μόνο για επεξεργασία· ο server ανακτά ξανά όλες τις ιδιότητες.</summary>
public sealed record CatalogManageRecord(
    Guid Id, string Code, string Name, bool IsActive,
    Guid? CategoryId = null, CatalogItemKind? Kind = null, bool TracksStock = false,
    Guid? ParentCategoryId = null,
    CatalogUnitOfMeasure? UnitOfMeasure = null,
    CatalogAccountingType? AccountingType = null,
    string? Barcode = null, string? LongDescription = null);

public sealed record CatalogCategoryOption(Guid Id, string Code, string Name,
    string? Path = null, Guid? ParentId = null);

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

    Task<IReadOnlyList<CatalogCategoryOption>> SearchCategoriesAsync(
        Guid tenantId, string? term, Guid? selectedId, CancellationToken cancellationToken = default);

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
             ((!command.AutoCode && command.Code is null) || command.Name is null)) ||
            (command.AutoCode && (command.Action != CatalogChangeAction.Create ||
                !string.IsNullOrWhiteSpace(command.Code))) ||
            command.ParentCategoryId == Guid.Empty ||
            command.RequestId == Guid.Empty ||
            (command.Action != CatalogChangeAction.Create && command.AutoCode) ||
            (command.Entity == CatalogManagedEntity.Category &&
             (command.CategoryId.HasValue || command.TracksStock ||
              command.UnitOfMeasure.HasValue || command.AccountingType.HasValue ||
               command.SetBarcode || command.SetLongDescription ||
               command.Barcode is not null || command.LongDescription is not null)) ||
            (command.Action is CatalogChangeAction.Activate or CatalogChangeAction.Deactivate &&
             (command.SetBarcode || command.SetLongDescription ||
              command.Barcode is not null || command.LongDescription is not null)) ||
            (command.Entity == CatalogManagedEntity.Service && command.TracksStock) ||
            (command.UnitOfMeasure is { } unit && !Enum.IsDefined(unit)) ||
            (command.AccountingType is { } accounting && !Enum.IsDefined(accounting)) ||
            (command.Entity == CatalogManagedEntity.Service &&
             (command.UnitOfMeasure is { } serviceUnit &&
              serviceUnit is not (CatalogUnitOfMeasure.Service or CatalogUnitOfMeasure.Hour) ||
              command.AccountingType is { } serviceAccounting &&
              serviceAccounting != CatalogAccountingType.Service)) ||
            (command.Entity == CatalogManagedEntity.Product &&
             (command.UnitOfMeasure == CatalogUnitOfMeasure.Service ||
              command.AccountingType == CatalogAccountingType.Service)))
        {
            return new(CatalogChangeStatus.Invalid);
        }

        try
        {
            // Η ίδια επικύρωση ισχύει πριν από κάθε SQL πρόσβαση και για GUI και για CLI.
            if (command.Entity != CatalogManagedEntity.Category &&
                command.Action is CatalogChangeAction.Create or CatalogChangeAction.Update)
            {
                if (command.Action == CatalogChangeAction.Create || command.SetBarcode)
                {
                    _ = CatalogItemDetails.NormalizeBarcode(command.Barcode);
                }

                if (command.Action == CatalogChangeAction.Create || command.SetLongDescription)
                {
                    _ = CatalogItemDetails.NormalizeLongDescription(command.LongDescription);
                }
            }

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

    /// <summary>Αναζήτηση κατηγοριών επιτρέπεται μόνο με δικαίωμα Manage.</summary>
    public async Task<IReadOnlyList<CatalogCategoryOption>?> SearchCategoriesAsync(
        Guid userId, Guid tenantId, string? term, Guid? selectedId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(userId, tenantId, cancellationToken))
        {
            return null;
        }

        if (term is { Length: > 80 } || term?.Any(char.IsControl) == true)
        {
            return [];
        }

        return await store.SearchCategoriesAsync(tenantId, term?.Trim(), selectedId,
            cancellationToken);
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
