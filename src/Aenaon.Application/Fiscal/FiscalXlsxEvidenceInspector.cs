using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>Πραγματική τιμή κελιού από πρωτότυπο XLSX, χωρίς αξιολόγηση φορολογικού κανόνα.</summary>
public sealed record FiscalXlsxCellSample(string Address, string Text, bool IsFormula);

/// <summary>Συνοπτική εικόνα πραγματικού φύλλου του επίσημου αρχείου.</summary>
public sealed record FiscalXlsxSheetOverview(string Name, int NonEmptyCellCount,
    IReadOnlyList<FiscalXlsxCellSample> SampleCells);

/// <summary>Ασφαλής δομική επιθεώρηση τεκμηρίου· δεν εγκρίνει συνδυασμούς χαρακτηρισμών.</summary>
public sealed record FiscalXlsxEvidenceInspection(string ArtifactSha256, long FileSizeBytes,
    IReadOnlyList<FiscalXlsxSheetOverview> Sheets)
{
    public bool IsClassificationMatrixVerified => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Διαβάζει αποκλειστικά OOXML φύλλα, κοινές συμβολοσειρές και πραγματικά κελιά.
/// Απορρίπτει μακροεντολές, υπερβολικά ZIP entries και μη ασφαλή XML.
/// </summary>
public sealed class FiscalXlsxEvidenceInspector
{
    private const long MaximumFileBytes = 30L * 1024 * 1024;
    private const long MaximumEntryBytes = 40L * 1024 * 1024;
    private const long MaximumTotalExpandedBytes = 128L * 1024 * 1024;
    private const int MaximumEntries = 600;
    private const int MaximumSheets = 80;
    private const int MaximumSharedStrings = 200000;
    private const int MaximumSamples = 30;
    private const int MaximumCellText = 180;

    private static readonly XNamespace Spreadsheet =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    public async Task<FiscalXlsxEvidenceInspection> InspectAsync(string xlsxPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(xlsxPath) ||
            !string.Equals(Path.GetExtension(xlsxPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Απαιτείται τοπικό αρχείο XLSX χωρίς μακροεντολές.");
        }

        var file = new FileInfo(Path.GetFullPath(xlsxPath));
        if (!file.Exists || file.Length is <= 0 or > MaximumFileBytes)
        {
            throw Invalid("Το πρωτότυπο XLSX δεν υπάρχει ή υπερβαίνει το όριο των 30 MiB.");
        }

        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        string sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        stream.Position = 0;

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            ValidateArchive(archive);

            XDocument workbook = ReadXml(Required(archive, "xl/workbook.xml"), cancellationToken);
            XDocument rels = ReadXml(Required(archive, "xl/_rels/workbook.xml.rels"),
                cancellationToken);
            IReadOnlyList<string> strings = ReadSharedStrings(archive, cancellationToken);

            var sheetLinks = rels.Descendants(PackageRelationships + "Relationship")
                .Where(value => (string?)value.Attribute("Type") is string type &&
                    type.EndsWith("/worksheet", StringComparison.Ordinal))
                .ToDictionary(value => (string?)value.Attribute("Id") ?? string.Empty,
                    value => ResolveWorksheetPath(value), StringComparer.Ordinal);

            XElement[] sheets = workbook.Descendants(Spreadsheet + "sheet").ToArray();
            if (sheets.Length is 0 or > MaximumSheets)
            {
                throw Invalid("Ο αριθμός φύλλων δεν είναι αποδεκτός.");
            }

            var output = new List<FiscalXlsxSheetOverview>(sheets.Length);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = (string?)sheet.Attribute("name") ?? string.Empty;
                string relationshipId = (string?)sheet.Attribute(OfficeRelationships + "id") ??
                    string.Empty;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 31 ||
                    !names.Add(name) || !sheetLinks.TryGetValue(relationshipId, out string? path))
                {
                    throw Invalid("Αδύνατη η αντιστοίχιση πραγματικού φύλλου με ασφαλές XML path.");
                }

                XDocument worksheet = ReadXml(Required(archive, path), cancellationToken);
                var samples = new List<FiscalXlsxCellSample>(MaximumSamples);
                int count = 0;
                foreach (XElement cell in worksheet.Descendants(Spreadsheet + "sheetData")
                    .Elements(Spreadsheet + "row").Elements(Spreadsheet + "c"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string address = (string?)cell.Attribute("r") ?? string.Empty;
                    if (!IsCellAddress(address))
                    {
                        throw Invalid("Βρέθηκε κελί με μη έγκυρη διεύθυνση A1.");
                    }

                    bool formula = cell.Element(Spreadsheet + "f") is not null;
                    string value = ReadCellText(cell, strings, formula);
                    if (value.Length == 0 && !formula)
                    {
                        continue;
                    }

                    count++;
                    if (samples.Count < MaximumSamples)
                    {
                        samples.Add(new FiscalXlsxCellSample(address,
                            value.Length > MaximumCellText ? value[..MaximumCellText] : value,
                            formula));
                    }
                }

                output.Add(new FiscalXlsxSheetOverview(name, count, samples.AsReadOnly()));
            }

            return new FiscalXlsxEvidenceInspection(sha256, stream.Length, output.AsReadOnly());
        }
        catch (Exception error) when (error is InvalidDataException or XmlException or
            System.IO.IOException or System.ArgumentException or
            System.InvalidOperationException or System.OverflowException)
        {
            throw Invalid("Το XLSX δεν έχει έγκυρη/ασφαλή δομή OOXML ή περιέχει μη έγκυρα κελιά.");
        }
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count is 0 or > MaximumEntries)
        {
            throw Invalid("Το ZIP περιέχει υπερβολικό αριθμό καταχωρίσεων.");
        }

        long expanded = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string path = entry.FullName;
            if (path.StartsWith('/') ||
                path.Contains('\\') || path.Contains("../", StringComparison.Ordinal) ||
                path.Contains("/..", StringComparison.Ordinal) ||
                !names.Add(path) || entry.Length > MaximumEntryBytes ||
                entry.Length > 0 && entry.CompressedLength == 0 ||
                entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > 300)
            {
                throw Invalid("Το αρχείο ZIP περιέχει μη ασφαλή καταχώριση.");
            }

            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumTotalExpandedBytes ||
                path.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid("Το XLSX υπερβαίνει τα όρια ή περιλαμβάνει VBA.");
            }
        }
    }

    private static ZipArchiveEntry Required(ZipArchive archive, string path) =>
        archive.GetEntry(path) ?? throw Invalid("Λείπει υποχρεωτικό μέρος του XLSX.");

    private static XDocument ReadXml(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Stream stream = entry.Open();
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumEntryBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        XDocument document = ReadXml(entry, cancellationToken);
        XElement[] strings = document.Descendants(Spreadsheet + "si").ToArray();
        if (strings.Length > MaximumSharedStrings)
        {
            throw Invalid("Υπέρβαση επιτρεπόμενων κοινών συμβολοσειρών.");
        }

        return strings.Select(value => string.Concat(
            value.Descendants(Spreadsheet + "t").Select(text => text.Value))).ToArray();
    }

    private static string ResolveWorksheetPath(XElement relationship)
    {
        if ((string?)relationship.Attribute("TargetMode") is string mode &&
            string.Equals(mode, "External", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Δεν επιτρέπονται εξωτερικοί σύνδεσμοι φύλλων.");
        }

        string target = (string?)relationship.Attribute("Target") ?? string.Empty;
        if (target.Contains('\\') || target.Contains("..", StringComparison.Ordinal) ||
            target.Contains('?') ||
            target.Contains('#'))
        {
            throw Invalid("Μη ασφαλές worksheet relationship.");
        }

        string path = target.StartsWith('/')
            ? target.TrimStart('/')
            : "xl/" + target;

        if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) ||
            !path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Το worksheet relationship δείχνει εκτός του επιτρεπόμενου path.");
        }

        return path;
    }

    private static string ReadCellText(XElement cell, IReadOnlyList<string> strings, bool formula)
    {
        if (formula)
        {
            // Τα cached formula values δεν επιτρέπεται να αντιμετωπιστούν ως πρωτογενή στοιχεία.
            return "[FORMULA: μη επαληθευμένη τιμή]";
        }

        string type = (string?)cell.Attribute("t") ?? string.Empty;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(Spreadsheet + "is")
                .Descendants(Spreadsheet + "t").Select(value => value.Value)).Trim();
        }

        string raw = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
        if (type == "s")
        {
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index < 0 || index >= strings.Count)
            {
                throw Invalid("Άκυρο shared string index σε κελί.");
            }

            return strings[index].Trim();
        }

        return type switch
        {
            "b" => raw == "1" ? "TRUE" : "FALSE",
            "e" => "[EXCEL ERROR]",
            _ => raw.Trim()
        };
    }

    private static bool IsCellAddress(string address)
    {
        if (address.Length is < 2 or > 12)
        {
            return false;
        }

        int index = 0;
        while (index < address.Length && index < 3 && address[index] is >= 'A' and <= 'Z')
        {
            index++;
        }

        if (index == 0 || index == address.Length || address[index] == '0')
        {
            return false;
        }

        return address[index..].All(char.IsAsciiDigit);
    }

    private static DomainRuleException Invalid(string message) =>
        new("fiscal.xlsx.structure.invalid", message);
}
