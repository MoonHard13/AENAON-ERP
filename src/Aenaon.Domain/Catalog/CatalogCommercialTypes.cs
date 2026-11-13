namespace Aenaon.Domain.Catalog;

/// <summary>Βασική μονάδα μέτρησης είδους, ανεξάρτητη από φορολογικούς χαρακτηρισμούς.</summary>
public enum CatalogUnitOfMeasure
{
    Piece = 1,
    Kilogram = 2,
    Gram = 3,
    Liter = 4,
    Milliliter = 5,
    Portion = 6,
    Hour = 7,
    Service = 8
}

/// <summary>Εμπορικός/λογιστικός τύπος είδους· δεν αποτελεί αυτόματο χαρακτηρισμό myDATA.</summary>
public enum CatalogAccountingType
{
    Merchandise = 1,
    FinishedProduct = 2,
    RawMaterial = 3,
    Consumable = 4,
    Service = 5,
    FixedAsset = 6
}
