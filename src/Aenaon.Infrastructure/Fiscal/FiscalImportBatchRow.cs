using Aenaon.Application.Fiscal;

namespace Aenaon.Infrastructure.Fiscal;

/// <summary>Μία απομονωμένη γραμμή staging χωρίς σύνδεση με ενεργές αντιστοιχίσεις.</summary>
public sealed class FiscalImportBatchRow
{
    private FiscalImportBatchRow() { }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveUntil { get; private set; }

    public static FiscalImportBatchRow Create(Guid batchId, FiscalInspectedRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new FiscalImportBatchRow
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            Code = row.Reference.Code,
            Description = row.Description,
            EffectiveFrom = row.Reference.EffectiveFrom,
            EffectiveUntil = row.Reference.EffectiveUntil
        };
    }
}
