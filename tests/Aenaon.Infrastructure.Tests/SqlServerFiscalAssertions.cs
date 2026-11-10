using System.Data;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Πραγματικοί έλεγχοι απομόνωσης Tenant και αναφορών στην απομονωμένη SQL του CI.</summary>
internal static class SqlServerFiscalAssertions
{
    public static async Task RunAsync(SqlConnection connection, AenaonDbContext context,
        Guid tenantId, Guid foreignTenantId, CancellationToken cancellationToken)
    {
        DateOnly start = new(2026, 1, 1);
        DateOnly transactionDate = new(2026, 11, 10);
        var verified = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        const string sourceUrl = "https://developers.s1ecos.com/vat-tax-codes-869826m0";

        FiscalReferenceRecord Make(FiscalCodeSystem system) =>
            FiscalReferenceRecord.Create(
                FiscalReferenceCode.Create(FiscalReferenceSource.Impact, system,
                    "SYNTHETIC-TEST-CODE", "CI-ONLY", start),
                "Τεχνητός κωδικός αποκλειστικά για CI", sourceUrl, verified);

        var vat = Make(FiscalCodeSystem.MyDataVatCategory);
        var income = Make(FiscalCodeSystem.MyDataIncomeCategory);
        var type = Make(FiscalCodeSystem.MyDataIncomeType);
        context.FiscalReferenceCodes.AddRange(vat, income, type);
        await context.SaveChangesAsync(cancellationToken);

        var catalogItem = await context.CatalogItems.AsNoTracking()
            .SingleAsync(value => value.TenantId == tenantId && value.Code == "SKU01", cancellationToken);
        var mapping = FiscalTaxMappingRecord.Create(tenantId, catalogItem.Id,
            FiscalCustomerChannel.B2C, start, null, vat.Id, income.Id, type.Id);
        context.FiscalTaxMappings.Add(mapping);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var service = new FiscalMappingPreviewService(context, NullLogger<FiscalMappingPreviewService>.Instance);
        FiscalTransactionContext MakeContext(Guid tenant, FiscalCustomerChannel channel,
            FiscalOperationKind operation = FiscalOperationKind.FiscalDocument) =>
            FiscalTransactionContext.Create(tenant, Guid.NewGuid(), catalogItem.Id,
                CatalogItemKind.Product, transactionDate, channel, operation);

        var result = await service.PreviewAsync(MakeContext(tenantId, FiscalCustomerChannel.B2C),
            cancellationToken);
        Assert.IsFalse(result.CanIssueFiscalDocument);
        Assert.AreEqual(mapping.Id, result.MappingId);
        Assert.AreEqual(vat.Code, result.Candidate.VatCategory.Code);

        var foreign = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            service.PreviewAsync(MakeContext(foreignTenantId, FiscalCustomerChannel.B2C), cancellationToken));
        Assert.AreEqual("fiscal.mapping.not_found", foreign.Code);

        var order = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            service.PreviewAsync(MakeContext(tenantId, FiscalCustomerChannel.B2C, FiscalOperationKind.Order),
                cancellationToken));
        Assert.AreEqual("fiscal.profile.order.not_fiscal", order.Code);

        var b2g = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            service.PreviewAsync(MakeContext(tenantId, FiscalCustomerChannel.B2G), cancellationToken));
        Assert.AreEqual("fiscal.mapping.b2g.pending", b2g.Code);

        // Η βάση απαγορεύει σύνδεση είδους άλλης επιχείρησης.
        SqlException crossTenant = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertMappingAsync(connection, foreignTenantId, catalogItem.Id, 2, vat.Id, income.Id, type.Id,
                cancellationToken));
        Assert.AreEqual(547, crossTenant.Number);

        // Έγκυρο foreign key δεν αρκεί αν ο κωδικός ανήκει σε άλλο namespace.
        context.FiscalTaxMappings.Add(FiscalTaxMappingRecord.Create(tenantId, catalogItem.Id,
            FiscalCustomerChannel.B2B, start, null, income.Id, vat.Id, type.Id));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        var wrongNamespace = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            service.PreviewAsync(MakeContext(tenantId, FiscalCustomerChannel.B2B), cancellationToken));
        Assert.AreEqual("fiscal.mapping.reference.invalid", wrongNamespace.Code);

        // Χρονικά επικαλυπτόμενες προτάσεις απορρίπτονται αντί να επιλέγεται τυχαία μία.
        context.FiscalTaxMappings.Add(FiscalTaxMappingRecord.Create(tenantId, catalogItem.Id,
            FiscalCustomerChannel.B2C, start.AddDays(1), null, vat.Id, income.Id, type.Id));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        var ambiguous = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            service.PreviewAsync(MakeContext(tenantId, FiscalCustomerChannel.B2C), cancellationToken));
        Assert.AreEqual("fiscal.mapping.ambiguous", ambiguous.Code);
    }

    private static async Task InsertMappingAsync(SqlConnection connection, Guid tenantId, Guid itemId,
        int channel, Guid vatId, Guid incomeId, Guid typeId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.FiscalTaxMappings
                (Id, TenantId, CatalogItemId, CustomerChannel, EffectiveFrom, EffectiveUntil,
                 VatCategoryId, IncomeCategoryId, IncomeTypeId, CreatedAtUtc)
            VALUES (@id, @tenant, @item, @channel, @from, NULL, @vat, @income, @type, @created);
            """;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@item", SqlDbType.UniqueIdentifier).Value = itemId;
        command.Parameters.Add("@channel", SqlDbType.Int).Value = channel;
        command.Parameters.Add("@from", SqlDbType.Date).Value = new DateTime(2026, 1, 1);
        command.Parameters.Add("@vat", SqlDbType.UniqueIdentifier).Value = vatId;
        command.Parameters.Add("@income", SqlDbType.UniqueIdentifier).Value = incomeId;
        command.Parameters.Add("@type", SqlDbType.UniqueIdentifier).Value = typeId;
        command.Parameters.Add("@created", SqlDbType.DateTimeOffset).Value = DateTimeOffset.UtcNow;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
