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
        string? search, int page, int pageSize, CancellationToken token)
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

        List<PriceListItemRow> rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new PriceListItemRow(x.Id, x.Code, x.Name, x.Kind,
                context.PriceListItems.Where(y => y.TenantId == tenantId &&
                    y.PriceListId == listId && y.CatalogItemId == x.Id)
                    .Select(y => (decimal?)y.UnitPrice).FirstOrDefault()))
            .ToListAsync(token);
        bool more = rows.Count > pageSize;
        if (more)
        {
            rows.RemoveAt(pageSize);
        }
        return new(rows, page, more);
    }

    public async Task<PriceListResult> ApplyAsync(Guid tenantId, PriceListChange change,
        CancellationToken token)
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            PriceList list;
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
        IReadOnlyList<PriceListPriceChange> changes, CancellationToken token)
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
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

            foreach (PriceListPriceChange change in changes)
            {
                if (existing.TryGetValue(change.CatalogItemId, out PriceListItem? stored))
                {
                    stored.ChangePrice(change.UnitPrice);
                }
                else
                {
                    context.PriceListItems.Add(PriceListItem.Create(
                        tenantId, priceListId, change.CatalogItemId, change.UnitPrice));
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
        Guid catalogItemId, decimal expectedUnitPrice, CancellationToken token)
    {
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
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

    private static bool HasSql(DbUpdateException ex, params int[] numbers) =>
        ex.InnerException is SqlException sql &&
        sql.Errors.Cast<SqlError>().Any(x => numbers.Contains(x.Number));
}
