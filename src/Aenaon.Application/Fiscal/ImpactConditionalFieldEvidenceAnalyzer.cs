using System.Text.Json;

namespace Aenaon.Application.Fiscal;

/// <summary>Μη δεσμευτική καταγραφή παρουσίας πεδίου από την επίσημη λίστα IMPACT.</summary>
public sealed record ImpactConditionalFieldEvidence(
    string PublishedField,
    string SourceSection,
    string SourceUrl,
    int LocationsExamined,
    int ObservedNonNull,
    int ObservedNull,
    bool ContainerShapeNeedsReview)
{
    public int NotObserved => Math.Max(0, LocationsExamined - ObservedNonNull - ObservedNull);
    public bool IsApplicabilityDetermined => false;
    public bool IsFiscalRuleApproved => false;
    public bool IsProviderValidated => false;
}

/// <summary>
/// Διαβάζει μόνο την παρουσία των υπό όρους πεδίων, χωρίς να συμπεραίνει ότι απαιτούνται.
/// Καμία τιμή του παραστατικού δεν αποθηκεύεται ή καταγράφεται στην αναφορά.
/// </summary>
public sealed class ImpactConditionalFieldEvidenceAnalyzer
{
    private readonly ImpactContractPreflightCatalog catalog = new();

    public IReadOnlyList<ImpactConditionalFieldEvidence> Analyze(
        JsonElement document,
        ImpactContractProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Η καταγραφή υπό όρους πεδίων απαιτεί JSON object.");
        }

        ImpactContractPreflightReport requirements = catalog.Build(profile);
        var evidence = new List<ImpactConditionalFieldEvidence>();

        foreach (ImpactContractRequirement requirement in requirements.Requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requirement.Kind != ImpactContractRequirementKind.Conditional)
            {
                continue;
            }

            string[] segments = requirement.PublishedField.Split('.');
            bool isDetail = segments[0] == "Detail";
            bool isTaxes = segments[0] == "Taxes";
            var instances = new List<JsonElement>();
            bool invalidContainer = false;

            if (isDetail || isTaxes)
            {
                string containerName = isDetail ? "Details" : "Taxes";
                if (TryGet(document, containerName, out JsonElement container))
                {
                    if (container.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement instance in container.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            instances.Add(instance);
                            if (instance.ValueKind != JsonValueKind.Object)
                            {
                                invalidContainer = true;
                            }
                        }
                    }
                    else
                    {
                        invalidContainer = true;
                    }
                }
            }
            else
            {
                instances.Add(document);
            }

            int nonNull = 0;
            int observedNull = 0;
            foreach (JsonElement instance in instances)
            {
                JsonElement current = instance;
                bool found = true;
                foreach (string segment in segments.Skip(isDetail || isTaxes ? 1 : 0))
                {
                    if (!TryGet(current, segment, out current))
                    {
                        found = false;
                        break;
                    }
                }

                if (!found)
                {
                    continue;
                }

                if (current.ValueKind == JsonValueKind.Null)
                {
                    observedNull++;
                }
                else
                {
                    nonNull++;
                }
            }

            evidence.Add(new ImpactConditionalFieldEvidence(
                requirement.PublishedField,
                requirement.SourceSection,
                requirement.SourceUrl,
                instances.Count,
                nonNull,
                observedNull,
                invalidContainer));
        }

        return evidence.AsReadOnly();
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
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
