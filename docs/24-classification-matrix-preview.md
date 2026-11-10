# Aenaon ERP — Βήμα 9.3C.3A: myDATA Classification Matrix Evidence Preview

## 1. Σκοπός

Το πρώτο υποβήμα του 9.3C.3 εισάγει **ασφαλή, read-only επιθεώρηση υποψήφιων συνδυασμών χαρακτηρισμού εσόδων**, με συγκεκριμένο αρχείο τεκμηρίου ΑΑΔΕ myDATA και ενδεικτική χειροκίνητη αποδελτίωση σε JSON.

{% hint style="danger" %}
**Το υποψήφιο JSON δεν έχει αυτόματα διασταυρωθεί με τα πραγματικά κελιά του XLS.** Η συμφωνία SHA-256 αποδεικνύει την τοπική ακεραιότητα αρχείου, όχι την ακρίβεια της αποδελτίωσης ή τη νομιμότητα οποιουδήποτε συνδυασμού. Οι έξοδοι `IsSourceTranscriptionVerified`, `IsImpactContractValidated`, `IsFiscalRuleApproved` και `CanIssueFiscalDocument` παραμένουν **πάντα `false`**.
{% endhint %}

## 2. Επίσημες προδιαγραφές (επιθεώρηση 10 Νοεμβρίου 2026)

| Πηγή | Χρήση |
| --- | --- |
| [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA](https://aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | myDATA **2.0.2**, ξεχωριστή τεκμηρίωση ERP/Παρόχου, XSD και «Συνδυασμοί χαρακτηρισμών (αρχείο xls)». |
| [ΑΑΔΕ — πρωτότυπο XLSX συνδυασμών 2.0.2](https://aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx) | Πρωτεύον τεκμήριο που πρέπει να ληφθεί και να ελεγχθεί από άνθρωπο. |
| [IMPACT — Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Ερμηνεία κατηγορίας και τύπου χαρακτηρισμού, διαφορετική από type παραστατικού. |
| [IMPACT — VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Ανεξάρτητη διάσταση ΦΠΑ/απαλλαγών, **δεν** ελέγχεται σε αυτό το υποβήμα. |
| [IMPACT — Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Υποχρεωτικά πεδία ανά παραστατικό/κανάλι, και ειδικές B2G απαιτήσεις. |

Το myDATA reference set, τα conditional fields του IMPACT και οι φορολογικές ρυθμίσεις μπορούν να μεταβληθούν ανεξάρτητα. Δεν θεωρούμε ότι ένα σωστό code triplet αρκεί για τη δημιουργία τιμολογίου.

## 3. Σταθερές αρχιτεκτονικές αρχές

- **Χωρίς νέα EF migration:** η Development βάση παραμένει στις 5 migrations μετά το 9.3C.2.
- Χωρίς SQL writes, κανένα νέο API endpoint, καμία σύνδεση με λογαριασμό παρόχου.
- Χωρίς seed επιτρεπόμενων συνδυασμών. Τα test values είναι **synthetic** και δεν αποτελούν επιτρεπόμενους κωδικούς ΑΑΔΕ.
- Κάθε υποψήφια τριάδα δηλώνει **ακριβώς** `InvoiceType`, `IncomeCategory` και `IncomeType`. Δεν επιτρέπονται implicit wildcards ή default «24%».
- Όλες οι τριάδες συσχετίζονται με versioned πηγή, πρωτότυπο SHA-256, effective date interval και δηλωμένη αναφορά φύλλου/κελιού.
- Σε επικαλυπτόμενες ίδιες τριάδες, μη έγκυρο manifest ή λάθος hash, η λειτουργία απορρίπτει την προεπισκόπηση (fail closed).
- Η ένδειξη `PresentInCandidateManifest` σημαίνει **μόνο** ότι η τριάδα υπάρχει στο μη εγκεκριμένο manifest. Δεν αποτελεί «ΑΑΔΕ approved».

## 4. Προϋποθέσεις και παραλαβή XLSX

1. Εγκατάστησε το SDK της έκδοσης `global.json` και συγχρόνισε τον κώδικα.
2. Επίλεξε αποκλειστικά ασφαλή τοπικό φάκελο **εκτός Git repository** (π.χ. `C:\FiscalEvidence`).
3. Κατέβασε από την παραπάνω επίσημη σελίδα το πρωτότυπο «Συνδυασμοί χαρακτηρισμών (αρχείο xls)» και αποθήκευσέ το **χωρίς τροποποίηση**.
4. Υπολόγισε το hash με PowerShell:

```powershell
Get-FileHash -LiteralPath "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" -Algorithm SHA256
```

5. Τοποθέτησε το hash στο `ArtifactSha256` του JSON. Μην θεωρείς το hash ψηφιακή υπογραφή της ΑΑΔΕ.

## 5. Παράδειγμα manifest

{% hint style="warning" %}
Το παρακάτω είναι **σχήμα και μόνο**. Η τριάδα και το κελί είναι τεχνητά, ΔΕΝ προέρχονται από επίσημη επιβεβαίωση. Το placeholder SHA-256 **δεν είναι έγκυρη τιμή**. Δημιούργησε πραγματικό manifest μόνο αφού ελέγξεις το επίσημο αρχείο και διασταυρώσεις με αρμόδιο λογιστή/φοροτεχνικό.
{% endhint %}

```json
{
  "SourceVersion": "myDATA-2.0.2-REVIEW-PENDING",
  "SourceUrl": "https://aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx",
  "ArtifactSha256": "REPLACE_WITH_64_HEX_CHARACTERS_FROM_GET_FILE_HASH",
  "VerifiedAtUtc": "2026-11-10T12:00:00+00:00",
  "Rows": [
    {
      "InvoiceType": "1.1",
      "IncomeCategory": "CATEGORY1_1",
      "IncomeType": "E3_SYNTHETIC",
      "SourceSheet": "SYNTHETIC",
      "SourceCell": "B2",
      "EffectiveFrom": "2026-01-01",
      "EffectiveUntil": null
    }
  ]
}
```

| Πεδίο | Περιγραφή |
| --- | --- |
| `SourceVersion` | Ισχυρισμός για έκδοση πηγής που απαιτεί ανθρώπινη τεκμηρίωση. |
| `SourceUrl` | Ακριβές HTTPS URL πρωτότυπου XLS/XLSX στον επίσημο host `aade.gr`. |
| `ArtifactSha256` | Hash του πραγματικού αποθηκευμένου XLS/XLSX. |
| `VerifiedAtUtc` | UTC χρόνος επιθεώρησης, όχι νομική ημερομηνία ισχύος. |
| `Rows` | 1 έως 10.000 υποψήφιες τριάδες χαρακτηρισμού. |
| `InvoiceType` | Ξεχωριστό namespace `MyDataInvoiceType`. |
| `IncomeCategory` | Ξεχωριστό namespace `MyDataIncomeCategory`. |
| `IncomeType` | Ξεχωριστό namespace `MyDataIncomeType`. |
| `SourceSheet`/`SourceCell` | Δείκτης ανθρώπινης επαλήθευσης του XLS. **Δεν ελέγχεται αυτόματα η πραγματική τιμή του κελιού.** |
| `EffectiveFrom`/`EffectiveUntil` | Ισχύς αναφοράς, χωριστή από την ημερομηνία τεχνικής επιθεώρησης. |

## 6. Εκτέλεση και έλεγχοι

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git fetch origin
git switch --track origin/feature/9-3c-3a-classification-matrix-preview
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"

dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-matrix --manifest "C:\FiscalEvidence\matrix-manifest.json" --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx"
```

Προαιρετική αναζήτηση (αποκλειστικά στο **υποψήφιο** manifest):

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-matrix --manifest "C:\FiscalEvidence\matrix-manifest.json" --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --invoice-type "1.1" --income-category "CATEGORY1_1" --income-type "E3_SYNTHETIC" --date "2026-11-10"
```

Η επιστροφή περιλαμβάνει source URL, version, αριθμό υποψηφίων, SHA-256 και ξεκάθαρο μήνυμα **χωρίς SQL, φορολογική έγκριση ή αυτόματο XLS validation**.

## 7. Πότε χρησιμοποιείται και πότε όχι

**Χρησιμοποιείται:** για προετοιμασία μελλοντικής εισαγωγής πραγματικών κανόνων, αποτροπή λαθών/διπλοτύπων στο χειροκίνητο manifest, οργάνωση ελεγχόμενων συνδυασμών ανά ακριβή τύπο παραστατικού και περίοδο.

**Δεν χρησιμοποιείται:** για απόφαση έκδοσης, παραγωγική αποτίμηση ΦΠΑ, πιστοποίηση από πάροχο, έλεγχο διεθνών συναλλαγών, αναγνώριση B2G, αλλαγές στο `FiscalTaxMappings` ή φορολογικό approval.

## 8. Ασφάλεια, logging και troubleshooting

| Σφάλμα | Αιτία / επίλυση |
| --- | --- |
| `fiscal.matrix.manifest.invalid` | Διόρθωσε πεδία, επίσημο host, XLS extension, UTC, μήκη, προέλευση Sheet/Cell και όρια. |
| `fiscal.matrix.hash.mismatch` | Σύγκρινε hash με το **ίδιο πρωτότυπο αρχείο**· μην αλλάζεις αυθαίρετα τα τεκμήρια. |
| `fiscal.matrix.period.overlap` | Διπλό πανομοιότυπο triplet καλύπτει επικαλυπτόμενες ημερομηνίες. |
| `fiscal.reference.*` | Απαιτούνται πραγματικά έγκυρα domain namespace codes και χρονική ισχύς. |
| `fiscal.matrix.query.invalid` | Άκυρη ημερομηνία ή κωδικός αναζήτησης. |
| I/O error | Επιθεώρησε permissions, ελλείποντα/κλειδωμένα αρχεία και path. |

Κωδικοί εξόδου: `0` επιτυχία **preview**, `1` σφάλμα πρόσβασης αρχείου, `2` μη έγκυρο manifest/query, `130` ακύρωση.

Οι ενέργειες logging δεν εκθέτουν πλήρες manifest, passwords, API keys ή φορολογικά δεδομένα. Το CLI απαιτεί Development. Δεν παράγει νέα αρχεία, network requests ή βάσεις.

## 9. Πριν/μετά, rollback και release

- Πριν: έλεγχος official docs, λήψη πρωτότυπου XLS, υπολογισμός SHA-256, review από άνθρωπο και πράσινο CI.
- Μετά: αναμενόμενες **5 migrations χωρίς εκκρεμή**, καμία νέα schema αλλαγή λόγω του βήματος.
- Δεν γίνεται deployment νέων τιμολογιακών δυνατοτήτων. Rollback = Git revert του PR. Δεν υφίσταται SQL migration να αναιρεθεί.

## 10. Acceptance Checklist

- [ ] Ο κώδικας αναγνωρίζει μόνο διακριτά namespaces `InvoiceType`, `IncomeCategory`, `IncomeType`.
- [ ] Καμία τριάδα δεν θεωρείται αυτομάτως επιτρεπόμενη.
- [ ] SHA-256 από πραγματικά bytes τεκμηρίου.
- [ ] Απόρριψη λανθασμένου host, ημερομηνιών, επικαλύψεων και άγνωστων JSON fields.
- [ ] No SQL writes / no tax issuance / no real reference seed.
- [ ] Windows/Ubuntu format, build, tests και υφιστάμενα SQL integration tests πράσινα.
- [ ] Χειροκίνητη αντιπαραβολή με επίσημο XLS και IMPACT παραμένει ρητά **εκκρεμής**.

## 11. Τι απαιτείται στο 9.3C.3B

Αποδεδειγμένα ακριβής **ανάγνωση της πραγματικής διάταξης του XLS** (με golden file tests), σύγκριση όλων των υποψήφιων εγγραφών με τα πραγματικά κελιά, καταγραφή κάλυψης και αλλαγών έκδοσης, χωριστή λογιστική/φορολογική έγκριση, κανόνες VAT/εξαιρέσεων, IMPACT request contract tests και ασφαλής μεταφορά εγκεκριμένων reference codes. Πριν από αυτά, δεν υπάρχει ενεργό rule engine.
