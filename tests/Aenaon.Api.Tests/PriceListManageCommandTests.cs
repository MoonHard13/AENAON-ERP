using Aenaon.Api.Commands;
using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Ελέγχει ότι η κονσόλα δεν επιτρέπει σιωπηρή αντικατάσταση νεότερης τιμής.</summary>
[TestClass]
public sealed class PriceListManageCommandTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task PriceWritesRequireExpectedAmountAndForwardItToSharedService()
    {
        var store = new SessionPriceListFixture();
        var authorization = new TenantAuthorizationService(new TenantCommandStore
        {
            Role = TenantMembershipRole.Owner
        });
        var service = new PriceListManagementService(authorization, store);
        var verifier = new TenantCredentialFixture(true);
        var schema = new TenantAccessSchemaFixture();

        async Task<int> ExecuteAsync(string price)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            string[] args =
            [
                "--username", "admin",
                "--tenant-id", Tenant.ToString("D"),
                "--confirm-database", "AenaonERP_Unit",
                "--action", "prices",
                "--id", SessionPriceListFixture.ListId.ToString("D"),
                "--price", price,
                "--apply"
            ];
            return await PriceListManageCommand.ExecuteAsync(args,
                verifier, service, schema, NullLogger.Instance,
                output, error, () => "Fixture-only-Secret!17", true);
        }

        string item = SessionPriceListFixture.ItemId.ToString("D");
        Assert.AreEqual(2, await ExecuteAsync($"{item}=12.5000"));
        Assert.AreEqual(2, await ExecuteAsync($"{item}=12.5000@invalid"));
        Assert.AreEqual(0, store.PriceCalls);

        Assert.AreEqual(0, await ExecuteAsync($"{item}=12.5000@10.0000"));
        Assert.AreEqual(1, store.PriceCalls);
        Assert.IsNotNull(store.LastPriceChanges);
        Assert.IsTrue(store.LastPriceChanges[0].CheckExpected);
        Assert.AreEqual(10.0000m, store.LastPriceChanges[0].ExpectedUnitPrice);

        Assert.AreEqual(0, await ExecuteAsync($"{item}=12.5000@none"));
        Assert.AreEqual(2, store.PriceCalls);
        Assert.IsTrue(store.LastPriceChanges![0].CheckExpected);
        Assert.IsNull(store.LastPriceChanges[0].ExpectedUnitPrice);
    }
}
