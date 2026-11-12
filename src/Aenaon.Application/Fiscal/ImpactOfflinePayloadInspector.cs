using System.Text.Json;

namespace Aenaon.Application.Fiscal;

/// <summary>Δηλωμένο σενάριο αντισυμβαλλόμενου, όχι συμπέρασμα από τον τύπο παραστατικού.</summary>
public enum ImpactCounterpartyScenario
{
    Unknown = 0,
    Business = 1,
    Retail = 2
}

/// <summary>Σοβαρότητα τεχνικού ελέγχου χωρίς φορολογική έγκριση.</summary>
public enum ImpactOfflineFindingSeverity
{
    Error = 1,
    Review = 2
}

/// <summary>Τεχνική απόκλιση με πηγή και γνωστή JSON διεύθυνση.</summary>
public sealed record ImpactOfflineFinding(
    ImpactOfflineFindingSeverity Severity,
    string Code,
    string JsonPath,
    string SourceSection,
    string Description);

/// <summary>
/// Μόνο διαγνωστικός έλεγχος πραγματικής διάταξης JSON.
/// Η απουσία σφαλμάτων δεν ισοδυναμεί με αποδοχή payload από τον πάροχο.
/// </summary>
public sealed record ImpactOfflinePayloadReport(
    ImpactContractProfile Profile,
    ImpactCounterpartyScenario CounterpartyScenario,
    int LineCount,
    IReadOnlyList<ImpactOfflineFinding> Findings)
{
    public int StructuralErrorCount => Findings.Count(item =>
        item.Severity == ImpactOfflineFindingSeverity.Error);
    public int PendingExpertReviewCount => Findings.Count(item =>
        item.Severity == ImpactOfflineFindingSeverity.Review);
    public bool HasDetectedStructuralErrors => StructuralErrorCount > 0;
    public bool IsActualProviderContractValidated => false;
    public bool IsAadeClassificationApproved => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Απομονωμένος έλεγχος υποσυνόλου της δημοσιευμένης δομής EliseCoreDocument.
/// Απαιτεί πραγματικές πληροφορίες συναλλαγής και έλεγχο IMPACT πριν από κάθε χρήση.
/// </summary>
public sealed class ImpactOfflinePayloadInspector
{
    private const int MaxFindings = 1000;
    private const string SourceSectionBase = "1.3.2.1";
    private const string SourceSectionInvoice = "1.3.2.2";
    private const string SourceSectionB2G = "1.3.2.3";
    private const string SourceSectionConditional = "1.3.2.4";

    public ImpactOfflinePayloadReport Inspect(
        ReadOnlyMemory<byte> jsonBytes,
        ImpactContractProfile profile,
        ImpactCounterpartyScenario scenario,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(profile) || !Enum.IsDefined(scenario) ||
            profile == ImpactContractProfile.InvoiceB2G &&
            scenario == ImpactCounterpartyScenario.Retail)
        {
            throw new ArgumentOutOfRangeException(nameof(profile),
                "Μη επιτρεπτός συνδυασμός προφίλ και σεναρίου.");
        }

        // Το Windows PowerShell 5.1 μπορεί να προσθέσει UTF-8 BOM σε συνθετικό JSON.
        // Το αφαιρούμε μόνο από την αρχή, χωρίς να αλλάζουμε το αρχείο ή να χαλαρώνουμε τον parser.
        ReadOnlyMemory<byte> jsonContent = jsonBytes;
        if (jsonContent.Length >= 3 &&
            jsonContent.Span[0] == 0xEF &&
            jsonContent.Span[1] == 0xBB &&
            jsonContent.Span[2] == 0xBF)
        {
            jsonContent = jsonContent[3..];
        }

        // Οι άγνωστες/διπλές ονομασίες πεδίων δεν μπορούν να περνούν ως αξιόπιστες.
        using JsonDocument document = JsonDocument.Parse(jsonContent, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });

        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Το EliseCoreDocument πρέπει να είναι JSON object.");
        }

        EnsureNoDuplicateProperties(root, cancellationToken);

        var findings = new List<ImpactOfflineFinding>();
        Require(root, "DocumentTypeCode", "$.DocumentTypeCode", JsonValueKind.String,
            SourceSectionBase, findings);
        Require(root, "DateIssued", "$.DateIssued", JsonValueKind.String,
            SourceSectionBase, findings);
        Require(root, "Series", "$.Series", JsonValueKind.String,
            SourceSectionBase, findings);
        Require(root, "Number", "$.Number", JsonValueKind.String,
            SourceSectionBase, findings);
        RequirePath(root, ["Issuer", "Vat"], "$.Issuer.Vat",
            JsonValueKind.String, SourceSectionBase, findings);
        RequirePath(root, ["DistributionDetails", "InternalDocumentId"],
            "$.DistributionDetails.InternalDocumentId", JsonValueKind.String,
            SourceSectionBase, findings);
        RequirePath(root, ["Summaries", "TotalNetAmount"], "$.Summaries.TotalNetAmount",
            JsonValueKind.Number, SourceSectionBase, findings);
        RequirePath(root, ["Summaries", "TotalVATAmount"], "$.Summaries.TotalVATAmount",
            JsonValueKind.Number, SourceSectionBase, findings);
        RequirePath(root, ["Summaries", "TotalGrossValue"], "$.Summaries.TotalGrossValue",
            JsonValueKind.Number, SourceSectionBase, findings);

        int lineCount = 0;
        if (TryGet(root, "Details", out JsonElement details) &&
            details.ValueKind == JsonValueKind.Array &&
            details.GetArrayLength() > 0)
        {
            lineCount = details.GetArrayLength();
            if (lineCount > 10000)
            {
                throw new JsonException("Υπέρβαση ασφαλούς πλήθους γραμμών.");
            }
        }
        else
        {
            Add(findings, ImpactOfflineFindingSeverity.Error, "MissingDetails",
                "$.Details", SourceSectionBase, "Απαιτείται μη κενός πίνακας γραμμών Details.");
        }

        if (profile is ImpactContractProfile.InvoiceIapr or ImpactContractProfile.InvoiceB2G)
        {
            if (TryGet(root, "DocumentTypeCode", out JsonElement code) &&
                code.ValueKind == JsonValueKind.String &&
                !string.Equals(code.GetString(), "INVOICE", StringComparison.OrdinalIgnoreCase))
            {
                Add(findings, ImpactOfflineFindingSeverity.Error, "WrongDocumentType",
                    "$.DocumentTypeCode", SourceSectionInvoice,
                    "Το προφίλ τιμολογίου αφορά DocumentTypeCode INVOICE.");
            }

            Require(root, "CurrencyCode", "$.CurrencyCode", JsonValueKind.String,
                SourceSectionInvoice, findings);
            Require(root, "InvoiceTypeCode", "$.InvoiceTypeCode", JsonValueKind.String,
                SourceSectionInvoice, findings);
            RequirePath(root, ["Issuer", "Address", "CountryCode"],
                "$.Issuer.Address.CountryCode", JsonValueKind.String,
                SourceSectionInvoice, findings);
            RequirePath(root, ["Issuer", "RegisteredName"], "$.Issuer.RegisteredName",
                JsonValueKind.String, SourceSectionInvoice, findings);

            if (scenario == ImpactCounterpartyScenario.Business ||
                profile == ImpactContractProfile.InvoiceB2G)
            {
                RequirePath(root, ["CounterParty", "Address", "CountryCode"],
                    "$.CounterParty.Address.CountryCode", JsonValueKind.String,
                    SourceSectionInvoice, findings);
                RequirePath(root, ["CounterParty", "RegisteredName"],
                    "$.CounterParty.RegisteredName", JsonValueKind.String,
                    SourceSectionInvoice, findings);
                RequirePath(root, ["CounterParty", "Vat"], "$.CounterParty.Vat",
                    JsonValueKind.String, SourceSectionInvoice, findings);
            }
            else if (scenario == ImpactCounterpartyScenario.Unknown)
            {
                Add(findings, ImpactOfflineFindingSeverity.Review, "CounterpartyScenarioUnknown",
                    "$.CounterParty", SourceSectionInvoice,
                    "Δήλωσε ρητά B2B ή λιανική πριν κριθεί η υποχρεωτικότητα αντισυμβαλλόμενου.");
            }

            if (lineCount > 0)
            {
                int index = 0;
                foreach (JsonElement detail in details.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (detail.ValueKind != JsonValueKind.Object)
                    {
                        Add(findings, ImpactOfflineFindingSeverity.Error, "InvalidDetailLine",
                            $"$.Details[{index}]", SourceSectionInvoice,
                            "Κάθε γραμμή πρέπει να είναι JSON object.");
                    }
                    else
                    {
                        CheckInvoiceLine(detail, index, profile, findings);
                    }

                    index++;
                }
            }

            Add(findings, ImpactOfflineFindingSeverity.Review, "ClassificationMatrixPending",
                "$.Details[].IncomeClassification", SourceSectionInvoice,
                "Δεν ελέγχονται οι 1.403 υποψήφιοι συνδυασμοί έναντι εγκεκριμένων κανόνων ΑΑΔΕ.");
            Add(findings, ImpactOfflineFindingSeverity.Review, "ConditionalFieldsPending",
                "$", SourceSectionConditional,
                "Τα 37 υπό όρους πεδία δεν αξιολογούνται χωρίς συνθήκες πραγματικής συναλλαγής.");
            Add(findings, ImpactOfflineFindingSeverity.Review, "VatAndTotalsPending",
                "$.Summaries", SourceSectionInvoice,
                "Δεν πιστοποιούνται συντελεστές, απαλλαγές, αθροίσματα και στρογγυλοποιήσεις.");
            Add(findings, ImpactOfflineFindingSeverity.Review, "CorrelatedInvoicesPending",
                "$.CorrelatedInvoices", SourceSectionInvoice,
                "Η ανάγκη συσχέτισης εξαρτάται από τον εγκεκριμένο τύπο παραστατικού.");

            CheckTaxPlacement(root, details, findings);
        }

        if (profile == ImpactContractProfile.InvoiceB2G)
        {
            RequirePath(root, ["B2GDetails", "ContractingAuthorityCode"],
                "$.B2GDetails.ContractingAuthorityCode", JsonValueKind.String,
                SourceSectionB2G, findings);
            RequireCodePath(root, ["B2GDetails", "ContractReferenceCode"],
                "$.B2GDetails.ContractReferenceCode", SourceSectionB2G, findings);
            RequireCodePath(root, ["B2GDetails", "ContractTypeCode"],
                "$.B2GDetails.ContractTypeCode", SourceSectionB2G, findings);
            Add(findings, ImpactOfflineFindingSeverity.Review, "B2GContractPending",
                "$.B2GDetails", SourceSectionB2G,
                "Απαιτείται επιβεβαίωση πραγματικού ΑΑΗΤ, σύμβασης, CPV και EN16931.");
        }

        Add(findings, ImpactOfflineFindingSeverity.Review, "ProviderAcceptanceUnverified",
            "$", SourceSectionBase,
            "Ο πραγματικός πάροχος IMPACT δεν έχει δεχθεί ή επικυρώσει το JSON.");

        return new ImpactOfflinePayloadReport(profile, scenario, lineCount, findings.AsReadOnly());
    }

    private static void CheckInvoiceLine(JsonElement detail, int index,
        ImpactContractProfile profile, List<ImpactOfflineFinding> findings)
    {
        string prefix = $"$.Details[{index}]";
        Require(detail, "LineNo", $"{prefix}.LineNo", JsonValueKind.Number,
            SourceSectionInvoice, findings);
        Require(detail, "UnitPrice", $"{prefix}.UnitPrice", JsonValueKind.Number,
            SourceSectionInvoice, findings);
        Require(detail, "NetTotal", $"{prefix}.NetTotal", JsonValueKind.Number,
            SourceSectionInvoice, findings);
        Require(detail, "VatCategoryCode", $"{prefix}.VatCategoryCode",
            JsonValueKind.Number, SourceSectionInvoice, findings);
        Require(detail, "VatTotal", $"{prefix}.VATTotal", JsonValueKind.Number,
            SourceSectionInvoice, findings);

        // Η §1.3.2.2 έχει αμφισημία στη διαδρομή Classification.TypeCode.
        // Το επίσημο 1.1 B2B JSON δείχνει απευθείας ClassificationTypeCode.
        RequirePath(detail, ["IncomeClassification", "ClassificationCategoryCode"],
            $"{prefix}.IncomeClassification.ClassificationCategoryCode",
            JsonValueKind.String, SourceSectionInvoice, findings);
        RequirePath(detail, ["IncomeClassification", "ClassificationTypeCode"],
            $"{prefix}.IncomeClassification.ClassificationTypeCode",
            JsonValueKind.String, SourceSectionInvoice, findings);
        RequirePath(detail, ["IncomeClassification", "Amount"],
            $"{prefix}.IncomeClassification.Amount", JsonValueKind.Number,
            SourceSectionInvoice, findings);

        // Διακρίνουμε ρητά το επίπεδο σχήμα των παραδειγμάτων από την ένθετη
        // διαδρομή της λεκτικής λίστας, χωρίς να θεωρούμε εγκεκριμένη καμία μορφή.
        InspectClassificationSourceDifferences(detail, prefix, findings);

        // Η απουσία Amount σε δημοσιευμένο παράδειγμα καταγράφεται ως ανοιχτό contract issue.
        Add(findings, ImpactOfflineFindingSeverity.Review,
            "IncomeClassificationShapeAmbiguous",
            $"{prefix}.IncomeClassification", SourceSectionInvoice,
            "Αναγκαία επιβεβαίωση παραδείγματος B2B και γραπτής λίστας πεδίων από IMPACT.");

        if (profile == ImpactContractProfile.InvoiceB2G)
        {
            Require(detail, "MeasurementUnitCodeEN",
                $"{prefix}.MeasurementUnitCodeEN", JsonValueKind.String,
                SourceSectionB2G, findings);
            Require(detail, "CpvCode", $"{prefix}.CpvCode",
                JsonValueKind.String, SourceSectionB2G, findings);
        }
    }

    private static void InspectClassificationSourceDifferences(
        JsonElement detail, string linePath, List<ImpactOfflineFinding> findings)
    {
        if (!TryGet(detail, "IncomeClassification", out JsonElement classification) ||
            classification.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string prefix = $"{linePath}.IncomeClassification";
        bool hasFlatType = TryGet(classification, "ClassificationTypeCode", out _);
        bool hasNestedClassification = TryGet(
            classification, "Classification", out JsonElement nested);
        bool hasNestedType = hasNestedClassification &&
            TryGet(nested, "TypeCode", out _);

        if (hasNestedType && hasFlatType)
        {
            // Δεν επιλέγουμε σιωπηρά μεταξύ δύο παράλληλων ονομάτων/τιμών.
            Add(findings, ImpactOfflineFindingSeverity.Error,
                "ConflictingClassificationShapes", prefix, SourceSectionInvoice,
                "Υπάρχουν ταυτόχρονα επίπεδο και ένθετο TypeCode· απαιτείται σαφές schema IMPACT.");
        }
        else if (hasNestedType)
        {
            Add(findings, ImpactOfflineFindingSeverity.Review,
                "NestedClassificationTypeCodeUnverified",
                $"{prefix}.Classification.TypeCode", SourceSectionInvoice,
                "Η ένθετη διαδρομή αναφέρεται στη λίστα, αλλά το δημοσιευμένο sample είναι επίπεδο.");
        }
        else if (hasNestedClassification)
        {
            Add(findings, ImpactOfflineFindingSeverity.Review,
                "UnexpectedNestedClassificationObject",
                $"{prefix}.Classification", SourceSectionInvoice,
                "Υπάρχει ένθετο αντικείμενο χωρίς τεκμηριωμένο TypeCode.");
        }

        if (!TryGet(classification, "Amount", out _))
        {
            Add(findings, ImpactOfflineFindingSeverity.Review,
                "ClassificationAmountSampleDiscrepancy",
                $"{prefix}.Amount", SourceSectionInvoice,
                "Το πεδίο είναι required στη §1.3.2.2 αλλά λείπει από δημοσιευμένο B2B sample.");
        }
    }

    private static void CheckTaxPlacement(
        JsonElement root, JsonElement details, List<ImpactOfflineFinding> findings)
    {
        bool taxesAtDocumentLevel = TryGet(root, "Taxes", out JsonElement taxes) &&
            taxes.ValueKind == JsonValueKind.Array && taxes.GetArrayLength() > 0;
        if (!taxesAtDocumentLevel || details.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        string[] lineTaxProperties =
        [
            "WithheldAmount", "StampDutyAmount", "OtherTaxesAmount",
            "DeductionsAmount"
        ];
        int index = 0;
        foreach (JsonElement detail in details.EnumerateArray())
        {
            if (detail.ValueKind == JsonValueKind.Object &&
                lineTaxProperties.Any(property => TryGet(detail, property, out JsonElement element) &&
                    element.ValueKind != JsonValueKind.Null))
            {
                Add(findings, ImpactOfflineFindingSeverity.Error,
                    "PossibleTaxPlacementConflict", $"$.Details[{index}]",
                    SourceSectionConditional,
                    "Υπάρχουν φόροι στο Taxes και σε Details· απαιτείται διόρθωση ή τεκμηρίωση.");
            }

            index++;
        }
    }

    private static void RequireCodePath(JsonElement root, string[] path,
        string jsonPath, string sourceSection, List<ImpactOfflineFinding> findings)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (!TryGet(current, segment, out current))
            {
                Add(findings, ImpactOfflineFindingSeverity.Error, "MissingDocumentedField",
                    jsonPath, sourceSection, "Απουσιάζει υποχρεωτικός κωδικός.");
                return;
            }
        }

        // Τα δημοσιευμένα B2G παραδείγματα αναφέρουν και αριθμητικές ειδικές τιμές (0, 3).
        if (current.ValueKind != JsonValueKind.String &&
            current.ValueKind != JsonValueKind.Number ||
            current.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(current.GetString()))
        {
            Add(findings, ImpactOfflineFindingSeverity.Error, "InvalidDocumentedValue",
                jsonPath, sourceSection,
                "Απαιτείται μη κενός κωδικός κειμένου ή αριθμός. Η σημασία του δεν επικυρώνεται.");
        }
    }

    private static void RequirePath(JsonElement root, string[] path,
        string jsonPath, JsonValueKind expected, string sourceSection,
        List<ImpactOfflineFinding> findings)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (!TryGet(current, segment, out current))
            {
                Add(findings, ImpactOfflineFindingSeverity.Error, "MissingDocumentedField",
                    jsonPath, sourceSection, "Απουσιάζει υποχρεωτικό πεδίο της τεκμηρίωσης.");
                return;
            }
        }

        CheckValue(current, jsonPath, expected, sourceSection, findings);
    }

    private static void Require(JsonElement root, string property, string jsonPath,
        JsonValueKind expected, string sourceSection, List<ImpactOfflineFinding> findings)
    {
        if (!TryGet(root, property, out JsonElement current))
        {
            Add(findings, ImpactOfflineFindingSeverity.Error, "MissingDocumentedField",
                jsonPath, sourceSection, "Απουσιάζει υποχρεωτικό πεδίο της τεκμηρίωσης.");
            return;
        }

        CheckValue(current, jsonPath, expected, sourceSection, findings);
    }

    private static void CheckValue(JsonElement value, string path, JsonValueKind kind,
        string sourceSection, List<ImpactOfflineFinding> findings)
    {
        if (value.ValueKind != kind || kind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(value.GetString()) ||
            kind == JsonValueKind.Number && !value.TryGetDecimal(out _))
        {
            Add(findings, ImpactOfflineFindingSeverity.Error, "InvalidDocumentedValue",
                path, sourceSection, "Μη αποδεκτός βασικός τύπος ή κενή τιμή.");
        }
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            // Ακολουθούμε τα PascalCase παραδείγματα και αναγνωρίζουμε αβλαβείς παραλλαγές case.
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static void EnsureNoDuplicateProperties(
        JsonElement element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    throw new JsonException("Διπλό JSON property name (χωρίς διάκριση πεζών/κεφαλαίων).");
                }

                EnsureNoDuplicateProperties(property.Value, cancellationToken);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(value, cancellationToken);
            }
        }
    }

    private static void Add(List<ImpactOfflineFinding> findings,
        ImpactOfflineFindingSeverity severity, string code, string path,
        string section, string description)
    {
        if (findings.Count >= MaxFindings)
        {
            throw new JsonException("Υπέρβαση ορίου ευρημάτων δομικού ελέγχου.");
        }

        findings.Add(new ImpactOfflineFinding(severity, code, path, section, description));
    }
}
