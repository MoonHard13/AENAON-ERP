using System.Data;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Ανάγνωση και σειριοποιημένες εγγραφές εμπορικών τιμοκαταλόγων ανά Tenant.</summary>
public sealed class SqlPriceListManagementStore(AenaonDbContext context) : IPriceListManagementStore
{
    public async Task<PriceListPage> ListAsync(Guid tenantId, string? search,
        bool includeInactive, int page, int pageSize, CancellationToken token)
    {
        IQueryable<PriceList> query = context.PriceLists.AsNoTracking()
            .Where(x => x.TenantId == tenantId);
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Code.StartsWith(search) || x.Name.StartsWith(search));
        }

        List<PriceListSummary> rows = await query
            .OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new PriceListSummary(x.Id, x.Code, x.Name, x.StoreId,
                x.Channel, x.PricesIncludeVat, x.ValidFrom, x.ValidUntil, x.IsActive,
                context.PriceListItems.Count(y => y.TenantId == tenantId &&
                    y.PriceListId == x.Id)))
            .ToListAsync(token);
        bool more = rows.Count > pageSize;
        if (more)
        {
            rows.RemoveAt(pageSize);
        }
        return new(rows, page, more);
    }

    public async Task<PriceListSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken token) =>
        await context.PriceLists.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new PriceListSummary(x.Id, x.Code, x.Name, x.StoreId,
                x.Channel, x.PricesIncludeVat, x.ValidFrom, x.ValidUntil, x.IsActive,
                context.PriceListItems.Count(y => y.TenantId == tenantId && y.PriceListId == x.Id)))
            .SingleOrDefaultAsync(token);

    public async Task<PriceListItemsPage?> ReadItemsAsync(Guid tenantId, Guid listId,
        string? search, int page, int pageSize, CancellationToken token,
        string sortBy = "code", bool descending = false)
    {
        bool exists = await context.PriceLists.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == listId, token);
        if (!exists)
        {
            return null;
        }

        IQueryable<CatalogItem> query = context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Code.StartsWith(search) || x.Name.StartsWith(search));
        }

        // Η ταξινόμηση προηγείται του Skip/Take και γίνεται εξ ολοκλήρου στη SQL.
        // Ο αριθμητικός κωδικός ακολουθεί φυσική σειρά, όπως στον κύριο κατάλογο ειδών.
        var joined = query.Select(item => new
        {
            Item = item,
            IsTextCode = item.Code == "" || EF.Functions.Like(item.Code, "%[^0-9]%"),
            UnitPrice = context.PriceListItems.Where(price =>
                    price.TenantId == tenantId && price.PriceListId == listId &&
                    price.CatalogItemId == item.Id)
                .Select(price => (decimal?)price.UnitPrice).FirstOrDefault()
        });

        var ordered = (sortBy, descending) switch
        {
            ("name", false) => joined.OrderBy(x => x.Item.Name)
                .ThenBy(x => x.Item.Id),
            ("name", true) => joined.OrderByDescending(x => x.Item.Name)
                .ThenBy(x => x.Item.Id),
            ("kind", false) => joined.OrderBy(x => x.Item.Kind)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("kind", true) => joined.OrderByDescending(x => x.Item.Kind)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            // Οι τιμές που δεν έχουν οριστεί εμφανίζονται στο τέλος και στις δύο κατευθύνσεις.
            ("price", false) => joined.OrderBy(x => x.UnitPrice == null)
                .ThenBy(x => x.UnitPrice).ThenBy(x => x.Item.Id),
            ("price", true) => joined.OrderBy(x => x.UnitPrice == null)
                .ThenByDescending(x => x.UnitPrice).ThenBy(x => x.Item.Id),
            // Η στήλη ενέργειας ισοδυναμεί με την παρουσία ή απουσία τιμής.
            ("priced", false) => joined.OrderBy(x => x.UnitPrice != null)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("priced", true) => joined.OrderByDescending(x => x.UnitPrice != null)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            (_, false) => joined.OrderBy(x => x.IsTextCode)
                .ThenBy(x => x.IsTextCode ? 0L : x.Item.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Item.Code))
                .ThenBy(x => x.IsTextCode ? 0 : x.Item.Code.Length > 18
                    ? x.Item.Code.Length : 0)
                .ThenBy(x => x.Item.Code).ThenBy(x => x.Item.Id),
            _ => joined.OrderBy(x => x.IsTextCode)
                .ThenByDescending(x => x.IsTextCode ? 0L : x.Item.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Item.Code))
                .ThenByDescending(x => x.IsTextCode ? 0 : x.Item.Code.Length > 18
                    ? x.Item.Code.Length : 0)
                .ThenByDescending(x => x.Item.Code).ThenBy(x => x.Item.Id)
        };

        List<PriceListItemRow> rows = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new PriceListItemRow(x.Item.Id, x.Item.Code,
                x.Item.Name, x.Item.Kind, x.UnitPrice))
            .ToListAsync(token);
        bool more = rows.Count > pageSize;
        if (more)
        {
            rows.RemoveAt(pageSize);
        }
        return new(rows, page, more);
    }

    /// <summary>Επιστρέφει καταχωρημένο ιστορικό ανά τιμοκατάλογο, με ανθεκτική σειρά.</summary>
    public async Task<PriceListHistoryPage?> ReadHistoryAsync(Guid tenantId, Guid priceListId,
        int page, int pageSize, CancellationToken token)
    {
        bool exists = await context.PriceLists.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == priceListId, token);
        if (!exists)
        {
            return null;
        }

        List<PriceListAuditRow> rows = await context.PriceListAuditEntries.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PriceListId == priceListId)
            .OrderByDescending(x => x.OccurredUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new PriceListAuditRow(x.Id, x.OccurredUtc, x.Action,
                x.CatalogItemId, context.CatalogItems.Where(y => y.TenantId == tenantId &&
                    y.Id == x.CatalogItemId).Select(y => y.Code).FirstOrDefault(),
                x.ActorUserId, x.Source, x.BeforePrice, x.AfterPrice,
                x.BeforePolicy, x.AfterPolicy))
            .ToListAsync(token);
        bool more = rows.Count > pageSize;
        if (more)
        {
            rows.RemoveAt(pageSize);
        }
        return new(rows, page, more);
    }

    public async Task<PriceListResult> ApplyAsync(Guid tenantId, PriceListChange change,
        CancellationToken token, Guid? actorUserId = null, string source = "System")
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            await AcquireTenantLockAsync(tenantId, token);
            PriceList list;
            string? beforePolicy = null;
            if (change.Action == PriceListAction.Create)
            {
                if (change.StoreId.HasValue &&
                    !await context.Stores.AsNoTracking().AnyAsync(x =>
                        x.TenantId == tenantId && x.Id == change.StoreId.Value, token))
                {
                    return new(PriceListStatus.Invalid, Error: "pricelist.store.invalid");
                }

                list = PriceList.Create(tenantId, change.StoreId, change.Code,
                    change.Name, change.Channel, change.PricesIncludeVat,
                    change.ValidFrom, change.ValidUntil);
                context.PriceLists.Add(list);
            }
            else
            {
                PriceList? existing = await context.PriceLists.SingleOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == change.Id, token);
                if (existing is null)
                {
                    return new(PriceListStatus.Missing);
                }
                list = existing;
                beforePolicy = PolicySnapshot(existing);
                if (change.CheckExpectedDetails &&
                    (list.Name != change.ExpectedName ||
                     list.ValidFrom != change.ExpectedValidFrom ||
                     list.ValidUntil != change.ExpectedValidUntil))
                {
                    return new(PriceListStatus.Conflict, Error: "pricelist.profile.conflict");
                }

                if (change.Action == PriceListAction.Update)
                {
                    if (!change.EditCommercialProfile)
                    {
                        list.Update(change.Name, change.ValidFrom, change.ValidUntil);
                    }
                    else
                    {
                        // Όλες οι αλλαγές πλαισίου ελέγχονται με την αρχική εικόνα της φόρμας.
                        if (list.StoreId != change.ExpectedStoreId ||
                            list.Channel != change.ExpectedChannel ||
                            list.PricesIncludeVat != change.ExpectedPricesIncludeVat)
                        {
                            return new(PriceListStatus.Conflict, Error: "pricelist.profile.conflict");
                        }

                        bool changed = list.StoreId != change.StoreId ||
                            list.Channel != change.Channel ||
                            list.PricesIncludeVat != change.PricesIncludeVat;

                        if (changed)
                        {
                            if (change.StoreId.HasValue &&
                                !await context.Stores.AsNoTracking().AnyAsync(x =>
                                    x.TenantId == tenantId && x.Id == change.StoreId.Value, token))
                            {
                                return new(PriceListStatus.Invalid, Error: "pricelist.store.invalid");
                            }

                            // Η αλλαγή τιμής με/χωρίς ΦΠΑ αλλάζει την οικονομική σημασία:
                            // απαιτείται ρητή επιβεβαίωση όταν ο τιμοκατάλογος έχει τιμές.
                            if (!change.ConfirmPolicyChange &&
                                await context.PriceListItems.AsNoTracking().AnyAsync(x =>
                                    x.TenantId == tenantId && x.PriceListId == list.Id, token))
                            {
                                return new(PriceListStatus.Invalid, Error: "pricelist.policy.confirm_required");
                            }
                        }

                        list.UpdateCommercialProfile(change.Name, change.StoreId,
                            change.Channel, change.PricesIncludeVat,
                            change.ValidFrom, change.ValidUntil);
                    }
                }
                else
                {
                    list.ChangeActive(change.Action == PriceListAction.Activate);
                }
            }

            // Ο έλεγχος γίνεται με τα ενεργά κοινά είδη της ίδιας κατάταξης.
            if (list.IsActive && await HasOverlappingPricesAsync(list, null, token))
            {
                return new(PriceListStatus.Conflict, Error: "pricelist.overlap.conflict");
            }

            string afterPolicy = PolicySnapshot(list);
            if (beforePolicy != afterPolicy)
            {
                PriceListAuditAction action = change.Action switch
                {
                    PriceListAction.Create => PriceListAuditAction.CreateList,
                    PriceListAction.Activate => PriceListAuditAction.ActivateList,
                    PriceListAction.Deactivate => PriceListAuditAction.DeactivateList,
                    _ => PriceListAuditAction.UpdateList
                };
                context.PriceListAuditEntries.Add(PriceListAuditEntry.Create(
                    tenantId, list.Id, null, actorUserId, source, action,
                    beforePolicy: beforePolicy, afterPolicy: afterPolicy));
            }

            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return new(PriceListStatus.Saved, list.Id);
        }
        catch (DbUpdateException ex) when (HasSql(ex, 2601, 2627))
        {
            return new(PriceListStatus.Duplicate, Error: "pricelist.code.duplicate");
        }
        catch (DbUpdateException ex) when (HasSql(ex, 547))
        {
            return new(PriceListStatus.Invalid, Error: "pricelist.reference.invalid");
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    public async Task<PriceListResult> SetPricesAsync(Guid tenantId, Guid priceListId,
        IReadOnlyList<PriceListPriceChange> changes, CancellationToken token,
        Guid? actorUserId = null, string source = "System")
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            await AcquireTenantLockAsync(tenantId, token);
            PriceList? list = await context.PriceLists.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId &&
                    x.Id == priceListId, token);
            if (list is null)
            {
                return new(PriceListStatus.Missing);
            }
            if (!list.IsActive)
            {
                return new(PriceListStatus.Invalid, Error: "pricelist.inactive");
            }

            Guid[] itemIds = changes.Select(x => x.CatalogItemId).ToArray();
            int validItems = await context.CatalogItems.AsNoTracking()
                .CountAsync(x => x.TenantId == tenantId && x.IsActive &&
                    itemIds.Contains(x.Id), token);
            if (validItems != itemIds.Length)
            {
                return new(PriceListStatus.Invalid, Error: "pricelist.item.invalid");
            }

            Dictionary<Guid, PriceListItem> existing = await context.PriceListItems
                .Where(x => x.TenantId == tenantId && x.PriceListId == priceListId &&
                    itemIds.Contains(x.CatalogItemId))
                .ToDictionaryAsync(x => x.CatalogItemId, token);
            // Η σύγκριση γίνεται στο ίδιο σειριοποιημένο transaction πριν από οποιαδήποτε αλλαγή.
            foreach (PriceListPriceChange change in changes)
            {
                decimal? current = existing.TryGetValue(change.CatalogItemId, out PriceListItem? row)
                    ? row.UnitPrice : null;
                if (change.CheckExpected && current != change.ExpectedUnitPrice)
                {
                    return new(PriceListStatus.Conflict, Error: "pricelist.price.conflict");
                }
            }

            if (await HasOverlappingPricesAsync(list, itemIds, token))
            {
                return new(PriceListStatus.Conflict, Error: "pricelist.overlap.conflict");
            }

            foreach (PriceListPriceChange change in changes)
            {
                if (existing.TryGetValue(change.CatalogItemId, out PriceListItem? stored))
                {
                    if (stored.UnitPrice == change.UnitPrice)
                    {
                        continue;
                    }
                    context.PriceListAuditEntries.Add(PriceListAuditEntry.Create(
                        tenantId, priceListId, change.CatalogItemId, actorUserId, source,
                        PriceListAuditAction.UpdatePrice, beforePrice: stored.UnitPrice,
                        afterPrice: change.UnitPrice));
                    stored.ChangePrice(change.UnitPrice);
                }
                else
                {
                    context.PriceListItems.Add(PriceListItem.Create(
                        tenantId, priceListId, change.CatalogItemId, change.UnitPrice));
                    context.PriceListAuditEntries.Add(PriceListAuditEntry.Create(
                        tenantId, priceListId, change.CatalogItemId, actorUserId, source,
                        PriceListAuditAction.AddPrice, afterPrice: change.UnitPrice));
                }
            }

            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return new(PriceListStatus.Saved, priceListId);
        }
        catch (DbUpdateException ex) when (HasSql(ex, 2601, 2627, 547))
        {
            return new(PriceListStatus.Invalid, Error: "pricelist.price.conflict");
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>Διαγράφει αποκλειστικά την εμπορική τιμή, ποτέ το προϊόν ή τον τιμοκατάλογο.</summary>
    public async Task<PriceListResult> RemovePriceAsync(Guid tenantId, Guid priceListId,
        Guid catalogItemId, decimal expectedUnitPrice, CancellationToken token,
        Guid? actorUserId = null, string source = "System")
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            await AcquireTenantLockAsync(tenantId, token);
            PriceList? list = await context.PriceLists.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId &&
                    x.Id == priceListId, token);
            if (list is null)
            {
                return new(PriceListStatus.Missing);
            }
            if (!list.IsActive)
            {
                return new(PriceListStatus.Invalid, Error: "pricelist.inactive");
            }

            PriceListItem? existing = await context.PriceListItems.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.PriceListId == priceListId &&
                    x.CatalogItemId == catalogItemId, token);
            if (existing is null)
            {
                return new(PriceListStatus.Missing, Error: "pricelist.price.missing");
            }
            if (existing.UnitPrice != expectedUnitPrice)
            {
                return new(PriceListStatus.Conflict, Error: "pricelist.price.conflict");
            }

            // Η διαγραφή περιορίζεται από σύνθετα FK στο ίδιο Tenant και τιμοκατάλογο.
            context.PriceListItems.Remove(existing);
            context.PriceListAuditEntries.Add(PriceListAuditEntry.Create(
                tenantId, priceListId, catalogItemId, actorUserId, source,
                PriceListAuditAction.RemovePrice, beforePrice: existing.UnitPrice));
            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return new(PriceListStatus.Saved, priceListId);
        }
        catch (DbUpdateException ex) when (HasSql(ex, 547))
        {
            return new(PriceListStatus.Invalid, Error: "pricelist.price.referenced");
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>Κοινό αποκλειστικό transaction lock για αποφυγή ταυτόχρονων επικαλύψεων.</summary>
    private async Task AcquireTenantLockAsync(Guid tenantId, CancellationToken token)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255)
        {
            Value = $"AenaonPricePolicy:{tenantId:N}"
        };
        await context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 15000;",
            [result, resource], token);
        if (result.Value is not int status || status < 0)
        {
            throw new InvalidOperationException("Δεν κατέστη δυνατός ο συγχρονισμός τιμοκαταλόγων.");
        }
    }

    /// <summary>Επικάλυψη υπάρχει μόνο για το ίδιο κατάστημα, κανάλι, είδος και περίοδο.</summary>
    private async Task<bool> HasOverlappingPricesAsync(PriceList list,
        Guid[]? candidateItemIds, CancellationToken token)
    {
        if (!list.IsActive || candidateItemIds is { Length: 0 })
        {
            return false;
        }

        DateOnly lastDay = list.ValidUntil ?? DateOnly.MaxValue;
        IQueryable<PriceList> overlapping = context.PriceLists.AsNoTracking()
            .Where(x => x.TenantId == list.TenantId && x.Id != list.Id &&
                x.IsActive && x.StoreId == list.StoreId && x.Channel == list.Channel &&
                x.ValidFrom <= lastDay &&
                (!x.ValidUntil.HasValue || x.ValidUntil >= list.ValidFrom));
        if (candidateItemIds is { Length: > 0 })
        {
            return await overlapping.AnyAsync(x =>
                context.PriceListItems.Any(y => y.TenantId == list.TenantId &&
                    y.PriceListId == x.Id && candidateItemIds.Contains(y.CatalogItemId)), token);
        }

        IQueryable<Guid> ownIds = context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == list.TenantId && x.PriceListId == list.Id)
            .Select(x => x.CatalogItemId);
        return await overlapping.AnyAsync(x =>
            context.PriceListItems.Any(y => y.TenantId == list.TenantId &&
                y.PriceListId == x.Id && ownIds.Contains(y.CatalogItemId)), token);
    }

    private static string PolicySnapshot(PriceList list) =>
        $"Code={list.Code}; Name={list.Name}; Store={list.StoreId?.ToString("D") ?? "All"}; " +
        $"Channel={list.Channel}; Currency={list.Currency}; IncludeVat={list.PricesIncludeVat}; " +
        $"From={list.ValidFrom:yyyy-MM-dd}; Until={list.ValidUntil?.ToString("yyyy-MM-dd") ?? "Open"}; " +
        $"Active={list.IsActive}";

    private static bool HasSql(DbUpdateException ex, params int[] numbers) =>
        ex.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(x => numbers.Contains(x.Number));
}
