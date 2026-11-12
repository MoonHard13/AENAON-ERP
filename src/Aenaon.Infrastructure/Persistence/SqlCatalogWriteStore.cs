using System.Data;
using System.Globalization;
using System.Text;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Tenant-scoped εγγραφές με transaction/application lock, σειριακούς κωδικούς και idempotency.
/// Δεν διαγράφονται κατηγορίες και δεν ενημερώνονται φορολογικά δεδομένα.
/// </summary>
public sealed class SqlCatalogWriteStore(AenaonDbContext context) : ICatalogWriteStore
{
    private const int MaxHierarchyDepth = 64;

    public async Task<CatalogChangeResult> ApplyAsync(Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new(CatalogChangeStatus.Denied);
        }

        try
        {
            // Ένα αποκλειστικό applock ανά επιχείρηση καλύπτει αριθμούς, γονείς και receipts.
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
                    cancellationToken);
            await LockTenantAsync(tenantId, cancellationToken);

            if (command.RequestId is { } requestId)
            {
                CatalogChangeResult? prior = await ReadReceiptAsync(
                    tenantId, requestId, cancellationToken);
                if (prior is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return prior;
                }
            }

            string? numericCode = null;
            if (command.Action == CatalogChangeAction.Create && command.AutoCode)
            {
                int scope = command.Entity == CatalogManagedEntity.Category ? 1 : 2;
                long lastNumber = await ReadLastNumberAsync(tenantId, scope, cancellationToken);
                if (lastNumber == long.MaxValue)
                {
                    return new(CatalogChangeStatus.Invalid, Error: "catalog.code.exhausted");
                }

                numericCode = (lastNumber + 1).ToString(CultureInfo.InvariantCulture);
            }

            CatalogChangeCommand resolved = numericCode is not null
                ? command with { Code = numericCode }
                : command;
            CatalogChangeResult result =
                command.Entity == CatalogManagedEntity.Category
                    ? await ApplyCategoryAsync(tenantId, resolved, cancellationToken)
                    : await ApplyItemAsync(tenantId, resolved, cancellationToken);

            if (result.Status != CatalogChangeStatus.Saved)
            {
                return result;
            }

            if (command.Action == CatalogChangeAction.Create &&
                long.TryParse(result.Code, NumberStyles.None, CultureInfo.InvariantCulture,
                    out long numeric) && numeric > 0)
            {
                await UpsertLastNumberAsync(tenantId,
                    command.Entity == CatalogManagedEntity.Category ? 1 : 2,
                    numeric, cancellationToken);
            }

            if (command.RequestId is { } receiptId)
            {
                await InsertReceiptAsync(tenantId, receiptId, command.Entity,
                    result, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (HasSqlNumber(exception, 2601, 2627))
        {
            return new(CatalogChangeStatus.Duplicate, Error: "catalog.code.duplicate");
        }
        catch (DbUpdateException exception) when (HasSqlNumber(exception, 547))
        {
            return new(CatalogChangeStatus.Invalid, Error: "catalog.reference.invalid");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CatalogChangeStatus.Missing);
        }
        finally
        {
            // Δεν επαναλαμβάνεται αποτυχημένο INSERT σε επόμενη ενέργεια του ίδιου DbContext.
            context.ChangeTracker.Clear();
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
                    .Select(x => new CatalogManageRecord(x.Id, x.Code, x.Name, x.IsActive,
                        ParentCategoryId: x.ParentCategoryId))
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

        Guid? selectedCategory = entity == CatalogManagedEntity.Category
            ? record?.ParentCategoryId : record?.CategoryId;
        IReadOnlyList<CatalogCategoryOption> options = await SearchCategoriesAsync(
            tenantId, null, selectedCategory, cancellationToken);
        return new(record, options, false);
    }

    public async Task<IReadOnlyList<CatalogCategoryOption>> SearchCategoriesAsync(
        Guid tenantId, string? term, Guid? selectedId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return [];
        }

        IQueryable<CatalogCategory> query = context.CatalogCategories.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (x.IsActive || x.Id == selectedId));

        if (!string.IsNullOrWhiteSpace(term))
        {
            string prefix = term.Trim();
            query = query.Where(x => x.Code.StartsWith(prefix) ||
                x.Name.StartsWith(prefix));
        }

        var selected = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Select(x => new CategoryNode(x.Id, x.ParentCategoryId, x.Code, x.Name))
            .Take(31).ToListAsync(cancellationToken);
        if (selected.Count > 30)
        {
            selected.RemoveAt(30);
        }

        if (selectedId.HasValue && selected.All(x => x.Id != selectedId.Value))
        {
            CategoryNode? existing = await context.CatalogCategories.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == selectedId.Value)
                .Select(x => new CategoryNode(x.Id, x.ParentCategoryId, x.Code, x.Name))
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                selected.Add(existing);
            }
        }

        Dictionary<Guid, CategoryNode> nodes = selected.ToDictionary(x => x.Id);
        HashSet<Guid> requested = selected.Where(x => x.ParentId.HasValue)
            .Select(x => x.ParentId!.Value).ToHashSet();
        for (int depth = 0; depth < MaxHierarchyDepth && requested.Count > 0; depth++)
        {
            requested.ExceptWith(nodes.Keys);
            if (requested.Count == 0)
            {
                break;
            }

            List<CategoryNode> parents = await context.CatalogCategories.AsNoTracking()
                .Where(x => x.TenantId == tenantId && requested.Contains(x.Id))
                .Select(x => new CategoryNode(x.Id, x.ParentCategoryId, x.Code, x.Name))
                .ToListAsync(cancellationToken);
            requested.Clear();
            foreach (CategoryNode parent in parents)
            {
                nodes[parent.Id] = parent;
                if (parent.ParentId.HasValue && !nodes.ContainsKey(parent.ParentId.Value))
                {
                    requested.Add(parent.ParentId.Value);
                }
            }
        }

        return selected.Select(x => new CatalogCategoryOption(
            x.Id, x.Code, x.Name, BuildPath(x, nodes), x.ParentId)).ToList();
    }

    private static string BuildPath(CategoryNode leaf, Dictionary<Guid, CategoryNode> nodes)
    {
        var segments = new Stack<string>();
        var visited = new HashSet<Guid>();
        CategoryNode? current = leaf;
        while (current is not null && segments.Count < MaxHierarchyDepth && visited.Add(current.Id))
        {
            segments.Push(current.Name);
            current = current.ParentId is { } parent && nodes.TryGetValue(parent, out var found)
                ? found : null;
        }

        return string.Join(" / ", segments);
    }

    private async Task<CatalogChangeResult> ApplyCategoryAsync(Guid tenantId,
        CatalogChangeCommand command, CancellationToken cancellationToken)
    {
        CatalogCategory category;
        if (command.Action == CatalogChangeAction.Create)
        {
            category = CatalogCategory.Create(tenantId, command.Code, command.Name);
            if (!await ValidParentAsync(tenantId, category.Id,
                command.ParentCategoryId, cancellationToken))
            {
                return new(CatalogChangeStatus.Invalid, Error: "catalog.parent.invalid");
            }

            category.ChangeParent(command.ParentCategoryId);
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
                if (!await ValidParentAsync(tenantId, category.Id,
                    command.ParentCategoryId, cancellationToken))
                {
                    return new(CatalogChangeStatus.Invalid, Error: "catalog.parent.invalid");
                }

                category.Update(command.Code, command.Name);
                category.ChangeParent(command.ParentCategoryId);
            }
            else if (command.Action == CatalogChangeAction.Deactivate)
            {
                bool activeChildren = await context.CatalogCategories.AsNoTracking()
                    .AnyAsync(x => x.TenantId == tenantId &&
                        x.ParentCategoryId == category.Id && x.IsActive, cancellationToken);
                bool activeItems = await context.CatalogItems.AsNoTracking()
                    .AnyAsync(x => x.TenantId == tenantId &&
                        x.CategoryId == category.Id && x.IsActive, cancellationToken);
                if (activeChildren || activeItems)
                {
                    return new(CatalogChangeStatus.Invalid, Error: "catalog.category.in_use");
                }

                category.ChangeActive(false);
            }
            else
            {
                if (!await ValidParentAsync(tenantId, category.Id,
                    category.ParentCategoryId, cancellationToken))
                {
                    return new(CatalogChangeStatus.Invalid, Error: "catalog.parent.invalid");
                }

                category.ChangeActive(true);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(CatalogChangeStatus.Saved, category.Id, Code: category.Code);
    }

    private async Task<bool> ValidParentAsync(Guid tenantId, Guid categoryId, Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return true;
        }

        Guid? current = parentId;
        var visited = new HashSet<Guid> { categoryId };
        for (int depth = 0; depth < MaxHierarchyDepth && current.HasValue; depth++)
        {
            if (!visited.Add(current.Value))
            {
                return false;
            }

            var parent = await context.CatalogCategories.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == current.Value)
                .Select(x => new { x.IsActive, x.ParentCategoryId })
                .SingleOrDefaultAsync(cancellationToken);
            if (parent is null || !parent.IsActive)
            {
                return false;
            }

            current = parent.ParentCategoryId;
        }

        return !current.HasValue;
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
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == command.Id &&
                    x.Kind == kind, cancellationToken);
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
        return new(CatalogChangeStatus.Saved, item.Id, Code: item.Code);
    }

    private Task<bool> CategoryIsActiveAsync(Guid tenantId, Guid? id,
        CancellationToken cancellationToken) =>
        context.CatalogCategories.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.IsActive, cancellationToken);

    private async Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = Command("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            SELECT @result;
            """);
        cmd.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = "AenaonCatalog:" + tenantId.ToString("N");
        int result = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new InvalidOperationException("Αποτυχία κλειδώματος καταλόγου για ταυτόχρονη εγγραφή.");
        }
    }

    private async Task<long> ReadLastNumberAsync(Guid tenantId, int scope,
        CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = Command("""
            SELECT LastNumber FROM dbo.CatalogCodeCounters
            WHERE TenantId = @tenant AND Scope = @scope;
            """);
        cmd.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
        cmd.Parameters.Add("@scope", SqlDbType.TinyInt).Value = scope;
        object? value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private async Task UpsertLastNumberAsync(Guid tenantId, int scope, long value,
        CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = Command("""
            UPDATE dbo.CatalogCodeCounters SET LastNumber =
                CASE WHEN LastNumber < @number THEN @number ELSE LastNumber END
            WHERE TenantId = @tenant AND Scope = @scope;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.CatalogCodeCounters (TenantId, Scope, LastNumber)
                VALUES (@tenant, @scope, @number);
            """);
        cmd.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
        cmd.Parameters.Add("@scope", SqlDbType.TinyInt).Value = scope;
        cmd.Parameters.Add("@number", SqlDbType.BigInt).Value = value;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<CatalogChangeResult?> ReadReceiptAsync(Guid tenantId, Guid requestId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = Command("""
            SELECT RecordId, Code FROM dbo.CatalogMutationReceipts
            WHERE TenantId = @tenant AND RequestId = @request;
            """);
        cmd.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
        cmd.Parameters.Add("@request", SqlDbType.UniqueIdentifier).Value = requestId;
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new(CatalogChangeStatus.Saved, reader.GetGuid(0), Code: reader.GetString(1));
    }

    private async Task InsertReceiptAsync(Guid tenantId, Guid requestId,
        CatalogManagedEntity entity, CatalogChangeResult result, CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = Command("""
            INSERT INTO dbo.CatalogMutationReceipts
                (TenantId, RequestId, RecordId, Entity, Code, CompletedAtUtc)
            VALUES (@tenant, @request, @id, @entity, @code, SYSUTCDATETIME());
            """);
        cmd.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
        cmd.Parameters.Add("@request", SqlDbType.UniqueIdentifier).Value = requestId;
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = result.Id!.Value;
        cmd.Parameters.Add("@entity", SqlDbType.Int).Value = (int)entity;
        cmd.Parameters.Add("@code", SqlDbType.NVarChar, 40).Value = result.Code!;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqlCommand Command(string sql)
    {
        var connection = (SqlConnection)context.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = (SqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

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

    private sealed record CategoryNode(Guid Id, Guid? ParentId, string Code, string Name);
}
