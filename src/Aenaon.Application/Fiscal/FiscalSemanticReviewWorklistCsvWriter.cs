using System.Text;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>
/// Εξάγει ασφαλή λίστα εργασίας για ανεξάρτητο ανθρώπινο έλεγχο.
/// Το CSV δεν επανεισάγεται ως φορολογική απόφαση και δεν ενεργοποιεί κανόνες.
/// </summary>
public sealed class FiscalSemanticReviewWorklistCsvWriter
{
    private const char Separator = ';';
    private static readonly UTF8Encoding ExcelUtf8 = new(encoderShouldEmitUTF8Identifier: true);

    private static readonly string[] Header =
    [
        "EvidenceId",
        "OriginalXlsxSha256",
        "Sheet",
        "Address",
        "Direction",
        "SuggestedKind",
        "Priority",
        "ExactText_ExcelSafe",
        "OriginalReason",
        "RequiredHumanCheck",
        "NearbyCells_ExcelSafe",
        "ReviewerStatus",
        "ReviewerNotes",
        "AadeSourceUrlAndSection",
        "ImpactSourceUrlAndSection",
        "ReviewedBy",
        "ReviewedAtUtc"
    ];

    public async Task WriteNewAsync(FiscalSemanticReviewDossier dossier, string csvPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(csvPath) ||
            !string.Equals(Path.GetExtension(csvPath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("fiscal.semantics.worklist.path.invalid",
                "Απαιτείται νέα διαδρομή αρχείου .csv.");
        }

        ValidateDossier(dossier);

        string fullPath = Path.GetFullPath(csvPath);
        bool created = false;
        try
        {
            // Το CreateNew διασφαλίζει ότι δεν αντικαθίσταται προηγούμενο ανθρώπινο review.
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                await using var writer = new StreamWriter(stream, ExcelUtf8, 81920,
                    leaveOpen: true)
                {
                    NewLine = "\r\n"
                };

                // Το sep=; επιτρέπει άνοιγμα UTF-8 CSV στο Excel με ελληνικό διαχωριστικό.
                await writer.WriteLineAsync("sep=;");
                await WriteRowAsync(writer, Header, cancellationToken);

                foreach (FiscalSemanticReviewItem item in dossier.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string nearby = string.Join(" | ", item.NearbyCells.Select(
                        cell => $"{cell.Address}: {cell.Text}"));

                    string[] values =
                    [
                        item.EvidenceId,
                        dossier.OriginalXlsxSha256,
                        item.Sheet,
                        item.Address,
                        item.Direction.ToString(),
                        item.SuggestedKind.ToString(),
                        item.Priority.ToString(),
                        item.ExactText,
                        item.OriginalReason,
                        item.RequiredHumanCheck,
                        nearby,
                        "Pending",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty
                    ];

                    await WriteRowAsync(writer, values, cancellationToken);
                }

                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        catch
        {
            if (created)
            {
                // Διαγράφουμε αποκλειστικά το CSV που δημιούργησε αυτή η αποτυχημένη κλήση.
                try
                {
                    File.Delete(fullPath);
                }
                catch (IOException)
                {
                    // Το αρχικό σφάλμα παραμένει διαθέσιμο.
                }
                catch (UnauthorizedAccessException)
                {
                    // Η αφαίρεση του υπολειπόμενου CSV γίνεται από τον διαχειριστή.
                }
            }

            throw;
        }
    }

    private static void ValidateDossier(FiscalSemanticReviewDossier dossier)
    {
        if (dossier.IsFiscalRuleApproved || dossier.CanIssueFiscalDocument ||
            dossier.IsImpactContractValidated || dossier.IsOriginalAuthenticityVerified ||
            dossier.OriginalXlsxSha256 is not { Length: 64 } ||
            !dossier.OriginalXlsxSha256.All(Uri.IsHexDigit) ||
            dossier.PendingHumanReviewCount < 1 ||
            dossier.Items.Any(item =>
                item is null || item.IsHumanReviewed || item.IsFiscalRuleApproved ||
                string.IsNullOrWhiteSpace(item.EvidenceId) ||
                string.IsNullOrWhiteSpace(item.Address) ||
                item.NearbyCells is null) ||
            dossier.Items.Select(item => item.EvidenceId)
                .Distinct(StringComparer.Ordinal).Count() != dossier.Items.Count ||
            dossier.Groups.Sum(group => group.Count) != dossier.Items.Count)
        {
            throw new DomainRuleException("fiscal.semantics.worklist.invalid",
                "Μόνο πλήρης φάκελος με μοναδικές, μη εγκεκριμένες παρατηρήσεις επιτρέπεται.");
        }
    }

    private static async Task WriteRowAsync(StreamWriter writer,
        IEnumerable<string> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string line = string.Join(Separator,
            values.Select(value => EscapeField(value)));
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    private static string EscapeField(string? value)
    {
        string field = value ?? string.Empty;
        string trimmed = field.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')
        {
            // Το αρχικό JSON διατηρεί αμετάβλητη την τιμή· το CSV αποτρέπει Excel formula injection.
            field = "'" + field;
        }

        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
