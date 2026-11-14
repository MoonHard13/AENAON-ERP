using Aenaon.Domain.Catalog;

namespace Aenaon.Api.Ui;

/// <summary>Σταθερή αριθμητική τιμή και ονομασία για τον ενιαίο επιλογέα.</summary>
public sealed record CatalogSelectOption(int Value, string Label);

/// <summary>Σταθερές ελληνικές ονομασίες εμπορικών πεδίων για τις φόρμες του καταλόγου.</summary>
public static class CatalogCommercialOptions
{
    public static IReadOnlyList<CatalogUnitOfMeasure> ProductUnits { get; } =
    [
        CatalogUnitOfMeasure.Piece, CatalogUnitOfMeasure.Kilogram,
        CatalogUnitOfMeasure.Gram, CatalogUnitOfMeasure.Liter,
        CatalogUnitOfMeasure.Milliliter, CatalogUnitOfMeasure.Portion,
        CatalogUnitOfMeasure.Hour
    ];

    public static IReadOnlyList<CatalogUnitOfMeasure> ServiceUnits { get; } =
        [CatalogUnitOfMeasure.Service, CatalogUnitOfMeasure.Hour];

    public static IReadOnlyList<CatalogAccountingType> ProductAccountingTypes { get; } =
    [
        CatalogAccountingType.Merchandise,
        CatalogAccountingType.FinishedProduct,
        CatalogAccountingType.RawMaterial,
        CatalogAccountingType.Consumable,
        CatalogAccountingType.FixedAsset
    ];

    public static IReadOnlyList<CatalogSelectOption> ProductUnitOptions { get; } =
        ProductUnits.Select(unit => new CatalogSelectOption((int)unit, UnitName(unit))).ToArray();

    public static IReadOnlyList<CatalogSelectOption> ServiceUnitOptions { get; } =
        ServiceUnits.Select(unit => new CatalogSelectOption((int)unit, UnitName(unit))).ToArray();

    public static IReadOnlyList<CatalogSelectOption> ProductAccountingOptions { get; } =
        ProductAccountingTypes.Select(accounting =>
            new CatalogSelectOption((int)accounting, AccountingName(accounting))).ToArray();

    public static IReadOnlyList<CatalogSelectOption> ServiceAccountingOptions { get; } =
        [new((int)CatalogAccountingType.Service, AccountingName(CatalogAccountingType.Service))];

    public static string UnitName(CatalogUnitOfMeasure value) => value switch
    {
        CatalogUnitOfMeasure.Piece => "Τεμάχιο (τεμ.)",
        CatalogUnitOfMeasure.Kilogram => "Κιλό (kg)",
        CatalogUnitOfMeasure.Gram => "Γραμμάριο (g)",
        CatalogUnitOfMeasure.Liter => "Λίτρο (L)",
        CatalogUnitOfMeasure.Milliliter => "Χιλιοστόλιτρο (ml)",
        CatalogUnitOfMeasure.Portion => "Μερίδα",
        CatalogUnitOfMeasure.Hour => "Ώρα",
        CatalogUnitOfMeasure.Service => "Υπηρεσία",
        _ => "Μη έγκυρη μονάδα"
    };

    public static string AccountingName(CatalogAccountingType value) => value switch
    {
        CatalogAccountingType.Merchandise => "Εμπόρευμα",
        CatalogAccountingType.FinishedProduct => "Προϊόν",
        CatalogAccountingType.RawMaterial => "Πρώτη ύλη",
        CatalogAccountingType.Consumable => "Αναλώσιμο",
        CatalogAccountingType.Service => "Υπηρεσία",
        CatalogAccountingType.FixedAsset => "Πάγιο",
        _ => "Μη έγκυρη κατηγορία"
    };
}
