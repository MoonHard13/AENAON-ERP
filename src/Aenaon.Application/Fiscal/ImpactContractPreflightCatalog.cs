namespace Aenaon.Application.Fiscal;

/// <summary>Επίπεδο ελέγχου τεκμηρίωσης παρόχου, χωρίς δημιουργία φορολογικού παραστατικού.</summary>
public enum ImpactContractProfile
{
    Base = 1,
    InvoiceIapr = 2,
    InvoiceB2G = 3
}

/// <summary>Δημοσιευμένη απαίτηση ή προϋπόθεση, όχι αυτόματη επικύρωση payload.</summary>
public enum ImpactContractRequirementKind
{
    Required = 1,
    Conditional = 2,
    RequiresExpertReview = 3
}

/// <summary>Αυτούσια αναφορά του εγχειριδίου IMPACT με το εύρος εφαρμογής της.</summary>
public sealed record ImpactContractRequirement(
    string PublishedField,
    ImpactContractRequirementKind Kind,
    string Applicability,
    string SourceSection,
    string SourceUrl,
    string ReviewQuestion)
{
    public bool IsReviewed => false;
    public bool IsPayloadValidated => false;
}

/// <summary>Συμπληρωματική εκκρεμότητα ελέγχου που δεν αντιμετωπίζεται ως μεμονωμένο JSON field.</summary>
public sealed record ImpactContractReviewGate(
    string Name,
    string SourceUrl,
    string ReviewQuestion)
{
    public bool IsReviewed => false;
}

/// <summary>Απομονωμένη αναφορά απαιτήσεων χωρίς δεδομένα πελατών, διαπιστευτήρια ή εγκρίσεις.</summary>
public sealed record ImpactContractPreflightReport(
    ImpactContractProfile Profile,
    string DocumentationSnapshotDate,
    IReadOnlyList<ImpactContractRequirement> Requirements,
    IReadOnlyList<ImpactContractReviewGate> ReviewGates)
{
    public int RequiredCount => Requirements.Count(field =>
        field.Kind == ImpactContractRequirementKind.Required);
    public int ConditionalCount => Requirements.Count(field =>
        field.Kind == ImpactContractRequirementKind.Conditional);
    public int ManualReviewCount => Requirements.Count(field =>
        field.Kind == ImpactContractRequirementKind.RequiresExpertReview) + ReviewGates.Count;
    public bool IsActualPayloadValidated => false;
    public bool IsAadeComplianceApproved => false;
    public bool IsImpactContractValidated => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Συλλογή ρητών πεδίων από τις δημοσιευμένες ενότητες 1.3.2.1–1.3.2.4 IMPACT.
/// Δεν αποφαίνεται για πραγματικά παραστατικά ή φορολογικούς συνδυασμούς.
/// </summary>
public sealed class ImpactContractPreflightCatalog
{
    private const string MinimumFieldsUrl =
        "https://developers.s1ecos.com/minimum-conditional-fields-869822m0";
    private const string ClassificationUrl =
        "https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0";
    private const string VatUrl =
        "https://developers.s1ecos.com/vat-tax-codes-869826m0";
    private const string AadeUrl =
        "https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata";
    private const string SnapshotDate = "2026-11-11";

    private static readonly string[] BaseFields =
    [
        "documentTypeCode",
        "dateIssued",
        "series",
        "number",
        "issuer.vat",
        "distributionDetails.internalDocumentId",
        "Details[at least one detail line]",
        "summaries.totalNetAmount",
        "summaries.totalVatAmount",
        "summaries.totalGrossValue"
    ];

    private static readonly string[] IaprRequiredFields =
    [
        "CurrencyCode",
        "InvoiceTypeCode",
        "Issuer.Address.CountryCode",
        "Issuer.RegisteredName",
        "Detail.LineNo",
        "Detail.UnitPrice",
        "Detail.NetTotal",
        "Detail.VatCategoryCode",
        "Detail.VatTotal",
        "Detail.IncomeClassification.ClassificationCategoryCode",
        "Detail.IncomeClassification.Classification.TypeCode",
        "Detail.IncomeClassification.Amount"
    ];

    private static readonly string[] B2GRequiredFields =
    [
        "Detail.MeasurementUnitCodeEN",
        "Detail.CpvCode",
        "B2GDetails.ContractingAuthorityCode",
        "B2GDetails.ContractReferenceCode",
        "B2GDetails.ContractTypeCode"
    ];

    private static readonly string[] ConditionalFields =
    [
        "CorrelatedInvoices",
        "CounterParty.Address.CountryCode",
        "CounterParty.RegisteredName",
        "CounterParty.Vat",
        "Detail.MeasurementUnitCode",
        "Issuer.TaxRepresentativeName",
        "Issuer.TaxRepresentativeVat",
        "Issuer.TaxRepresentativeCountryCode",
        "Recipient.Address.Street",
        "Recipient.Address.City",
        "Recipient.Address.Postal",
        "Recipient.Address.CountryCode",
        "Detail.MeasurementUnitCodeEN",
        "Detail.TotalNetValueBeforeTotalDiscount",
        "Detail.ItemCodification",
        "Detail.ShipType.ApplicationId",
        "Detail.ShipType.ApplicationDate",
        "Detail.ShipType.Doy",
        "Detail.ShipType.ShipId",
        "Detail.VatExemptionCategoryCode",
        "Detail.WithheldAmount",
        "Detail.WithheldPercentageCategoryCode",
        "Detail.StampDutyAmount",
        "Detail.StampDutyPercentCategoryCode",
        "Detail.OtherTaxesAmount",
        "Detail.OtherTaxesPercentCategoryCode",
        "Detail.DeductionsAmount",
        "Summaries.TotalWithHeldAmount",
        "Summaries.TotalFeesAmount",
        "Summaries.TotalStampDutyAmount",
        "Summaries.TotalOtherTaxesAmount",
        "Summaries.TotalDeductionsAmount",
        "Summaries.TotalAllowances",
        "Summaries.TotalCharges",
        "Summaries.TotalVATAmountInAlternativeCurrency",
        "Taxes.TaxTypeCode",
        "Taxes.TaxAmount"
    ];

    public ImpactContractPreflightReport Build(ImpactContractProfile profile)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }

        var fields = new List<ImpactContractRequirement>();
        foreach (string field in BaseFields)
        {
            fields.Add(Required(field, "1.3.2.1",
                "Απαιτούμενο βασικό πεδίο ή επαρκής πληθικότητα γραμμών."));
        }

        var gates = new List<ImpactContractReviewGate>();
        if (profile is ImpactContractProfile.InvoiceIapr or ImpactContractProfile.InvoiceB2G)
        {
            foreach (string field in IaprRequiredFields)
            {
                fields.Add(Required(field, "1.3.2.2",
                    "Επιβεβαίωση τύπου δεδομένων, σύμβασης και ειδικών εξαιρέσεων."));
            }

            foreach (string field in ConditionalFields)
            {
                if (profile == ImpactContractProfile.InvoiceB2G &&
                    B2GRequiredFields.Contains(field, StringComparer.Ordinal))
                {
                    continue;
                }

                string condition = field switch
                {
                    "CorrelatedInvoices" => "Απαιτείται όταν το προβλέπει ο τύπος παραστατικού.",
                    "Detail.MeasurementUnitCode" => "Απαιτείται εκτός παραστατικών υπηρεσιών.",
                    "CounterParty.Address.CountryCode" or "CounterParty.RegisteredName" or
                        "CounterParty.Vat" => "Απαιτείται εκτός λιανικών παραστατικών.",
                    _ => "Υπό όρους: απαιτείται όταν υφίσταται η αντίστοιχη περίπτωση."
                };
                fields.Add(new ImpactContractRequirement(field,
                    ImpactContractRequirementKind.Conditional, condition,
                    field is "CorrelatedInvoices" or "Detail.MeasurementUnitCode" or
                        "CounterParty.Address.CountryCode" or "CounterParty.RegisteredName" or
                        "CounterParty.Vat" ? "1.3.2.2" : "1.3.2.4",
                    MinimumFieldsUrl, "Έλεγχος εφαρμογής, περιεχομένου και πραγματικής συνθήκης."));
            }

            gates.Add(new ImpactContractReviewGate("ClassificationCategoryTypeCombination",
                ClassificationUrl,
                "Έλεγχος myDATA κατηγορίας/τύπου ανά συγκεκριμένο παραστατικό και επίσημο XLSX."));
            gates.Add(new ImpactContractReviewGate("VatAndTaxMapping", VatUrl,
                "Έλεγχος ΦΠΑ, αιτιών απαλλαγής, παρακρατήσεων και τυχόν ειδικών συνθηκών."));
            gates.Add(new ImpactContractReviewGate("TaxPlacement",
                MinimumFieldsUrl,
                "Οι φόροι δεν πρέπει να δηλώνονται ταυτόχρονα σε Taxes και Details (1.3.2.4)."));
            gates.Add(new ImpactContractReviewGate("AadeV202",
                AadeUrl,
                "Επιβεβαίωση επίσημης ισχύουσας έκδοσης myDATA/XSD, καναλιού Παρόχου και συναφούς νομοθεσίας."));
        }

        if (profile == ImpactContractProfile.InvoiceB2G)
        {
            foreach (string field in B2GRequiredFields)
            {
                fields.Add(Required(field, "1.3.2.3",
                    "Επιπλέον απαίτηση B2G: επιβεβαίωση πραγματικής σύμβασης και φορέα."));
            }

            gates.Add(new ImpactContractReviewGate("B2GEndToEnd",
                MinimumFieldsUrl,
                "Ανεξάρτητος έλεγχος υποχρεωτικών B2G πεδίων, EN16931 και GSIS."));
        }

        if (fields.GroupBy(field => field.PublishedField, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Διπλό τεκμηριωμένο πεδίο στο προφίλ.");
        }

        return new ImpactContractPreflightReport(profile, SnapshotDate,
            fields.AsReadOnly(), gates.AsReadOnly());
    }

    private static ImpactContractRequirement Required(
        string field, string section, string reviewQuestion) =>
        new(field, ImpactContractRequirementKind.Required,
            "Δημοσιευμένο ως υποχρεωτικό στο αντίστοιχο προφίλ.",
            section, MinimumFieldsUrl, reviewQuestion);
}
