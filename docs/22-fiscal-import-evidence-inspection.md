# Aenaon ERP — Βήμα 9.3C.1: Offline Fiscal Reference Evidence Inspection

## 1. Σκοπός

Το πρώτο, **μη επεμβατικό** μέρος του 9.3C επιθεωρεί τοπικά υποψήφια φορολογικά λεξικά πριν επιτραπεί οποιοδήποτε import. Χρησιμοποιεί πρωτότυπο τεκμήριο (π.χ. επίσημο XLS, PDF ή διατηρημένο HTML), χειροκίνητο manifest JSON και πραγματικό SHA-256 του τεκμηρίου.

{% hint style="danger" %}
**Ο έλεγχος SHA-256 εγγυάται μόνο ότι το τοπικό αρχείο συμφωνεί με το hash που δηλώνει το manifest. Δεν αποδεικνύει αυθεντικότητα, φορολογική ορθότητα, επιτρεπόμενους συνδυασμούς ή έγκριση ΑΑΔΕ/IMPACT.** Η εφαρμογή **δεν γράφει στη SQL**, δεν επιτρέπει έκδοση παραστατικών και δεν επικοινωνεί με εξωτερικά API.
{% endhint %}

## 2. Επίσημες αναφορές — έλεγχος 10/11/2026

| Πηγή | Στοιχείο |
| --- | --- |
| [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Έκδοση v2.0.2, API ERP/Παρόχων, XSD και αρχείο «Συνδυασμοί χαρακτηρισμών». |
| [IMPACT — VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Ανεξάρτητα VAT category και VAT exemption. |
| [IMPACT — Classification Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Κατηγορία/τύπος χαρακτηρισμού. |
| [IMPACT — Measurement Units](https://developers.s1ecos.com/measurement-unit-codes-iapr-en16931-869829m0) | Κωδικοί myDATA και EN16931. |
| [IMPACT — Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Υποχρεωτικά πεδία που εξαρτώνται από τύπο και κανάλι. |

Η έκδοση και ημερομηνία ισχύος κάθε **συγκεκριμένης** εγγραφής πρέπει να ελέγχονται στο πρωτότυπο τεκμήριο. Μην θεωρείς ότι η αναγραφόμενη έκδοση του API είναι ίδια με την περίοδο ισχύος των φορολογικών κωδικών. Επανέλεγχος απαιτείται πριν από κάθε επόμενη ροή έγκρισης.

## 3. Τι υλοποιήθηκε

- `FiscalImportInspectionService`: ασύγχρονο άνοιγμα JSON και τεκμηρίου, όρια 2 MiB για manifest και 25 MiB για πρωτότυπο αρχείο, ελεγχόμενη deserialization.
- Πηγή ως ισχυρό enum (ΑΑΔΕ/IMPACT), ξεχωριστό `FiscalCodeSystem`, πραγματικό SHA-256 και UTC χρονοσήμανση.
- Έλεγχος HTTPS host, περιγραφών, κωδικών, χρονικών διαστημάτων και διπλών/επικαλυπτόμενων εγγραφών μέσα στο manifest.
- Σταθεροί domain error codes και υποστήριξη `CancellationToken`.
- Development-only CLI με κωδικούς εξόδου 0 (επιτυχία preview), 1 (πρόσβαση σε αρχεία), 2 (μη έγκυρη είσοδος), 130 (ακύρωση).
- Αμετάβλητο αποτέλεσμα `FiscalImportInspection` με `CanImportToDatabase = false`, `IsTaxRuleApproved = false`, `CanIssueFiscalDocument = false`.

**Δεν υπάρχει νέα migration στο 9.3C.1:** η `AenaonERP_Dev` παραμένει με τέσσερις migrations, χωρίς ενημέρωση των fiscal πινάκων.

## 4. Προϋποθέσεις

1. .NET SDK από το `global.json`, Git, κώδικας από το PR branch.
2. Το επίσημο πρωτότυπο αρχείο πηγής, αποθηκευμένο σε ασφαλή τοπικό φάκελο.
3. Έλεγχος περιεχομένου από αρμόδιο άνθρωπο με γνώση των φορολογικών κωδικών.
4. Επεξεργαστής UTF-8 JSON, π.χ. VS Code.

Δεν απαιτούνται SQL credentials, API keys, πάροχος, πρόσβαση στην ΑΑΔΕ ή νέα NuGet packages.

## 5. Προετοιμασία τεκμηρίου

Δημιούργησε φάκελο εκτός του repository για τα επίσημα αρχεία. Αποθήκευσε το **ακριβές** τεκμήριο, χωρίς επεξεργασία του πρωτότυπου. Στο PowerShell:

```powershell
Get-FileHash -LiteralPath "C:\FiscalEvidence\official-source.xls" -Algorithm SHA256
```

Αντέγραψε το SHA-256 στο manifest. Σημείωσε την ακριβή πηγή, την έκδοση του εγγράφου και την πραγματική ημερομηνία ισχύος των κωδικών, όταν αυτή τεκμηριώνεται.

## 6. Δομή JSON manifest

Το παρακάτω περιέχει **αποκλειστικά τεχνητό κωδικό** για παρουσίαση της δομής. Αντικατάστησε την έκδοση, το hash, τις περιόδους και κάθε τιμή μόνο μετά από έλεγχο πηγής. Τα JSON property names είναι case-sensitive και δεν υποστηρίζονται άγνωστα πεδία.

```json
{
  "Source": "Impact",
  "CodeSystem": "MyDataVatCategory",
  "SourceVersion": "REVIEWED-SOURCE-VERSION",
  "SourceUrl": "https://developers.s1ecos.com/vat-tax-codes-869826m0",
  "ArtifactSha256": "REPLACE_WITH_64_HEX_CHARACTERS_FROM_GET_FILE_HASH",
  "VerifiedAtUtc": "2026-11-10T10:00:00+00:00",
  "Rows": [
    {
      "Code": "SYNTHETIC-EXAMPLE",
      "Description": "Τεχνητός κωδικός παρουσίασης, όχι φορολογική τιμή",
      "EffectiveFrom": "2026-01-01",
      "EffectiveUntil": null
    }
  ]
}
```

| Πεδίο | Σημασία |
| --- | --- |
| `Source` | `Aade` ή `Impact`. Το άμεσο `En16931` import δεν επιτρέπεται ακόμη. |
| `CodeSystem` | Ακριβές όνομα enum όπως `MyDataVatCategory`, `MyDataIncomeCategory`, `MyDataIncomeType`, `MyDataVatExemption`, `MyDataMeasurementUnit`, `En16931MeasurementUnit`, `ImpactDocumentType`, `MyDataInvoiceType`. |
| `SourceVersion` | Έκδοση της συγκεκριμένης επίσημης πηγής, όχι κατασκευασμένη τιμή. |
| `SourceUrl` | HTTPS URL από `aade.gr` / `www.aade.gr` ή `developers.s1ecos.com` σύμφωνα με το Source. |
| `ArtifactSha256` | Ακριβές SHA-256 των bytes του αποθηκευμένου τεκμηρίου. |
| `VerifiedAtUtc` | UTC χρόνος επιθεώρησης, όχι μελλοντικός και όχι νομική έναρξη ισχύος. |
| `Rows` | Έως 2.000 υποψήφιες εγγραφές ενός code system με περιγραφή και ισχύ `[EffectiveFrom, EffectiveUntil]`. |

{% hint style="warning" %}
Το `VerifiedAtUtc` εισάγεται από τον χρήστη και **δεν αποτελεί ηλεκτρονική υπογραφή ή ανεξάρτητη επιβεβαίωση προέλευσης**. Στο επόμενο στάδιο απαιτούνται έγκριση reviewer, ιστορικό, αποδεικτικά source mapping και φορολογικό rule matrix.
{% endhint %}

## 7. Εκτέλεση στο CLI (χωρίς SQL)

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git fetch origin
git switch --track origin/feature/9-3c-fiscal-import-inspection
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"

dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-import --manifest "C:\FiscalEvidence\manifest.json" --evidence-file "C:\FiscalEvidence\official-source.xls"
```

Αν έχεις ήδη το branch τοπικά, το `git switch --track` δεν χρειάζεται· χρησιμοποίησε `git switch feature/9-3c-fiscal-import-inspection`.

Εάν το manifest είναι σωστό, επιστρέφονται πηγή, κωδικοσύστημα, έκδοση, αριθμός υποψήφιων γραμμών, υπολογισμένο SHA-256 και μήνυμα ότι **δεν πραγματοποιήθηκε εισαγωγή**.

## 8. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται:** κατά την προετοιμασία αλλαγών φορολογικών λεξικών, για ταυτοποίηση πρωτότυπου αρχείου και έγκαιρη απόρριψη ασυνεπών υποψήφιων εγγραφών.

**Δεν χρησιμοποιείται:** για υπολογισμό ΦΠΑ, ενεργοποίηση mapping, πιστοποίηση συνδυασμών, πραγματική έκδοση, ηλεκτρονική υπογραφή ή έγκριση κανόνα. Δεν μπορεί να διαβάσει αυτόματα γραμμές XLS/PDF — ο χειροκίνητος μετασχηματισμός σε JSON πρέπει να ελεγχθεί.

## 9. Troubleshooting

| Σφάλμα | Ενέργεια |
| --- | --- |
| `fiscal.import.manifest.invalid` | Έλεγξε JSON, ακριβή enums, HTTPS host, UTC ημερομηνία, επιτρεπόμενα μεγέθη και σειρές. |
| `fiscal.import.hash.mismatch` | Επανυπολόγισε hash από το **πραγματικό** πρωτότυπο αρχείο· μην επιδιορθώνεις αυθαίρετα το manifest. |
| `fiscal.import.period.overlap` | Έλεγξε διπλά codes και χρονική ισχύ από το επίσημο τεκμήριο. |
| `fiscal.reference.*` | Μη έγκυρος κωδικός, έκδοση ή χρονικό διάστημα σύμφωνα με το Domain 9.3A. |
| Αδυναμία πρόσβασης σε αρχεία | Έλεγξε διαδρομή, permissions, locked files και μέγεθος. |
| `Development` requirement | Η CLI λειτουργία παραμένει κλειστή εκτός Development. |

## 10. Ασφάλεια, logs και αναστροφή

Δεν πραγματοποιούνται δικτυακές κλήσεις, SQL writes, αλλαγές στο filesystem, ή αποθήκευση κωδικών πρόσβασης. Τα logs αναφέρουν την έναρξη, απόρριψη με error code, επιτυχία και ακύρωση, όχι περιεχόμενο αρχείων ή ευαίσθητα στοιχεία. Τα αποτελέσματα δεν αποτελούν production audit trail.

Rollback: αφαίρεση του branch/PR από τον κώδικα, χωρίς SQL rollback. Δεν πειράζεις `AenaonERP_Dev`.

## 11. Checklist

- [ ] Το αρχείο προέρχεται από επίσημη πηγή και διατηρήθηκε αυτούσιο.
- [ ] Το SHA-256 συμφωνεί με το τοπικό τεκμήριο.
- [ ] Το manifest περιγράφει **ένα** code system με σωστή έκδοση και ισχύ.
- [ ] Κανένας τεχνητός κωδικός δεν επιτρέπεται σε πραγματική φορολογική χρήση.
- [ ] Δοκιμές, formatting, build, δημοσιευμένο CI επιτυχή.
- [ ] Το αποτέλεσμα εμφανίζει ρητά `CanImportToDatabase = false`.
- [ ] Δεν έγινε καμία αλλαγή στην SQL, στο IMPACT ή στην ΑΑΔΕ.

## 12. Συνέχεια του 9.3C

Το επόμενο PR θα εισάγει **staging tables και ανθρώπινη έγκριση** με αμετάβλητη καταγραφή hash/πηγής/reviewer, ελεγχόμενο import, σύγκριση με το πρωτότυπο επίσημο αρχείο και ξεχωριστό έλεγχο επιτρεπόμενων συνδυασμών ανά τύπο παραστατικού. Ειδικά η πλήρης λογική ΦΠΑ, απαλλαγών, ταξινομήσεων B2B/B2C/B2G και IMPACT contract tests παραμένει αποκλεισμένη μέχρι να αξιολογηθούν οι πραγματικοί κανόνες.
