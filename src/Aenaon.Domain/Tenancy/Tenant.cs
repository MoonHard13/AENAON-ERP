using System.Collections.ObjectModel;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Tenancy;

/// <summary>Μια ανεξάρτητη επιχείρηση και τα καταστήματα που της ανήκουν.</summary>
public sealed class Tenant
{
    public const int MaxNameLength = 160;

    private readonly List<Store> _stores = [];
    private readonly ReadOnlyCollection<Store> _storeView;

    // Χρησιμοποιείται μόνο για ανασύσταση αποθηκευμένων δεδομένων από το EF Core.
    private Tenant()
    {
        Name = "";
        _storeView = _stores.AsReadOnly();
    }

    private Tenant(string? name)
    {
        Name = OrganizationText.RequiredName(name, MaxNameLength, "Όνομα επιχείρησης", "tenant.name.invalid");
        Id = Guid.NewGuid();
        _storeView = _stores.AsReadOnly();
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyList<Store> Stores => _storeView;

    public static Tenant Create(string? name)
    {
        return new Tenant(name);
    }

    /// <summary>Προσθέτει κατάστημα με μοναδικό κωδικό μέσα στην ίδια επιχείρηση.</summary>
    public Store AddStore(string? code, string? name)
    {
        var store = new Store(Id, code, name);
        if (_stores.Any(existing => StringComparer.OrdinalIgnoreCase.Equals(existing.Code, store.Code)))
        {
            throw new DomainRuleException("store.code.duplicate",
                "Υπάρχει ήδη κατάστημα με αυτόν τον κωδικό στην επιχείρηση.");
        }

        _stores.Add(store);
        return store;
    }
}
