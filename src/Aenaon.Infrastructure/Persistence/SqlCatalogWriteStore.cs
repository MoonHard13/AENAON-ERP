using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Αποθηκεύει μη φορολογικές μεταβολές με tenant-scoped SQL και χωρίς φυσικές διαγραφές.</summary>
public sealed class SqlCatalogWriteStore(AenaonDbContext context) : ICatalogWriteStore
{
    public async Task<CatalogChangeResult> ApplyAsync(Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new(CatalogChangeStatus.Denied);
        }

        try
        {
            return command.Entity == CatalogManagedEntity.Category
                ? await ApplyCategoryAsync(tenantId, command, cancellationToken)
                : await ApplyItemAsync(tenantId, command, cancellationToken);
        }
        catch (DbUpdateException exception) when (HasSqlNumber(exception, 2601, 2627))
        {
            return new(CatalogChangeStatus.Duplicate);
        }
        catch (DbUpdateException exception) when (HasSqlNumber(exception, 547))
        {
            return new(CatalogChangeStatus.Invalid, Error: "catalog.reference.invalid");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CatalogChangeStatus.Missing);
        }
    }

    public async Task<CatalogManageDetails> GetDetailsAsync(Guid tenantId,
        CatalogManagedEntity entity, Guid? id, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new(null, []);
        }

        CatalogManageRecord? record = null;
        if (id.HasValue)
        {
            if (entity == CatalogManagedEntity.Category)
            {
                record = await context.CatalogCategories.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.Id == id.Value)
                    .Select(x => new CatalogManageRecord(x.Id, x.Code, x.Name, x.IsActive))
                    .SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                CatalogItemKind kind = entity == CatalogManagedEntity.Service
                    ? CatalogItemKind.Service : CatalogItemKind.Product;
                record = await context.CatalogItems.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.Id == id.Value && x.Kind == kind)
                    .Select(x => new CatalogManageRecord(x.Id, x.Code, x.Name, x.IsActive,
                        x.CategoryId, x.Kind, x.TracksStock))
                    .SingleOrDefaultAsync(cancellationToken);
            }
        }

        if (entity == CatalogManagedEntity.Category)
        {
            return new(record, []);
        }

        // Η λίστα επιλογής περιορίζεται σε 300 καταχωρήσεις για αποφυγή υπερβολικού payload.
        List<CatalogCategoryOption> options = await context.CatalogCategories.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                (x.IsActive || (record != null && x.Id == record.CategoryId)))
            .OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Select(x => new CatalogCategoryOption(x.Id, x.Code, x.Name))
            .Take(301)
            .ToListAsync(cancellationToken);
        bool hasMore = options.Count > 300;
        if (hasMore)
        {
            options.RemoveAt(300);
        }

        return new(record, options, hasMore);
    }

    private async Task<CatalogChangeResult> ApplyCategoryAsync(Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken)
    {
        CatalogCategory category;
        if (command.Action == CatalogChangeAction.Create)
        {
            category = CatalogCategory.Create(tenantId, command.Code, command.Name);
            context.CatalogCategories.Add(category);
        }
        else
        {
            CatalogCategory? existing = await context.CatalogCategories
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == command.Id,
                    cancellationToken);
            if (existing is null)
            {
                return new(CatalogChangeStatus.Missing);
            }

            category = existing;
            if (command.Action == CatalogChangeAction.Update)
            {
                category.Update(command.Code, command.Name);
            }
            else
            {
                category.ChangeActive(command.Action == CatalogChangeAction.Activate);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(CatalogChangeStatus.Saved, category.Id);
    }

    private async Task<CatalogChangeResult> ApplyItemAsync(Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken)
    {
        CatalogItemKind kind = command.Entity == CatalogManagedEntity.Service
            ? CatalogItemKind.Service : CatalogItemKind.Product;

        CatalogItem item;
        if (command.Action == CatalogChangeAction.Create)
        {
            if (!await CategoryIsActiveAsync(tenantId, command.CategoryId, cancellationToken))
            {
                return new(CatalogChangeStatus.Invalid, Error: "catalog.category.inactive_or_missing");
            }

            item = CatalogItem.Create(tenantId, command.CategoryId!.Value,
                command.Code, command.Name, kind, command.TracksStock);
            context.CatalogItems.Add(item);
        }
        else
        {
            CatalogItem? existing = await context.CatalogItems
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == command.Id && x.Kind == kind,
                    cancellationToken);
            if (existing is null)
            {
                return new(CatalogChangeStatus.Missing);
            }

            item = existing;
            if (command.Action == CatalogChangeAction.Update)
            {
                if (command.CategoryId != item.CategoryId &&
                    !await CategoryIsActiveAsync(tenantId, command.CategoryId, cancellationToken))
                {
                    return new(CatalogChangeStatus.Invalid, Error: "catalog.category.inactive_or_missing");
                }

                item.Update(command.CategoryId ?? Guid.Empty, command.Code, command.Name,
                    command.TracksStock);
            }
            else
            {
                item.ChangeActive(command.Action == CatalogChangeAction.Activate);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(CatalogChangeStatus.Saved, item.Id);
    }

    private Task<bool> CategoryIsActiveAsync(Guid tenantId, Guid? id,
        CancellationToken cancellationToken) =>
        context.CatalogCategories.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.IsActive, cancellationToken);

    private static bool HasSqlNumber(Exception exception, params int[] numbers)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && numbers.Contains(sql.Number))
            {
                return true;
            }
        }

        return false;
    }
}
