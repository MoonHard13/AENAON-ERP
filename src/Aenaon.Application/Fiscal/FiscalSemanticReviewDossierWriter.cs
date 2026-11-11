using System.Text.Json;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>Αποθηκεύει μόνο νέο, μη εγκεκριμένο φάκελο ανθρώπινης επιθεώρησης.</summary>
public sealed class FiscalSemanticReviewDossierWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public async Task WriteNewAsync(FiscalSemanticReviewDossier dossier,
        string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(destinationPath) ||
            !string.Equals(Path.GetExtension(destinationPath), ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("fiscal.semantics.report.path.invalid",
                "Ο φάκελος επιθεώρησης πρέπει να είναι νέο αρχείο .json.");
        }

        string fullPath = Path.GetFullPath(destinationPath);
        bool created = false;
        try
        {
            await using (var stream = new FileStream(fullPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                created = true;
                await JsonSerializer.SerializeAsync(stream, dossier, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        catch
        {
            if (created)
            {
                // Καθαρίζουμε μόνο τη νέα, ενδεχομένως μερική εξαγωγή.
                try
                {
                    File.Delete(fullPath);
                }
                catch (IOException)
                {
                    // Διατηρούμε την αρχική αιτία αποτυχίας.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ο υπεύθυνος του φακέλου μπορεί να ολοκληρώσει τον καθαρισμό.
                }
            }

            throw;
        }
    }
}
