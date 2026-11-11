# Aenaon ERP — 9.3C.3B.5C: Offline IMPACT JSON Structural Preflight

## 1. Σκοπός

Το Aenaon ERP διαθέτει ήδη τεκμηριωμένο κατάλογο IMPACT (βήμα 9.3C.3B.5B) με 22 δημοσιευμένα υποχρεωτικά πεδία, 37 υπό όρους πεδία και 4 ανεξάρτητα review gates για το προφίλ InvoiceIapr.

Το παρόν βήμα εισάγει **αποκλειστικά offline διαγνωστικό έλεγχο υποσυνόλου της δομής JSON `EliseCoreDocument`**, με συνθετικά δεδομένα. Ελέγχει ορισμένα ξεκάθαρα απαιτούμενα πεδία, τους τύπους τους, τις γραμμές `Details`, τις πρόσθετες δομικές απαιτήσεις B2G και την πιθανή διπλή δήλωση φόρων.

{% hint style="danger" %}
**Το εργαλείο δεν εγκρίνει παραστατικά.** Επιτυχής δομικός έλεγχος (exit code `0`) σημαίνει μόνο **ότι δεν βρέθηκαν σφάλματα στους συγκεκριμένους τοπικούς ελέγχους**. Δεν αποτελεί απόδειξη myDATA compliance, αποδοχή από IMPACT, έγκριση συνδυασμών χαρακτηρισμών, εξουσιοδότηση έκδοσης ή δυνατότητα αποστολής. Οι ενδείξεις `IsActualProviderContractValidated`, `IsAadeClassificationApproved`, `IsFiscalRuleApproved`, `CanIssueFiscalDocument` είναι **πάντα false**.
{% endhint %}

## 2. Επίσημες πηγές που ελέγχθηκαν στις 11/11/2026

| Επίσημη τεκμηρίωση | Πεδίο χρήσης |
| --- | --- |
| [IMPACT Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Βασικές απαιτήσεις §1.3.2.1, πρόσθετες απαιτήσεις §1.3.2.2, B2G §1.3.2.3, υπό όρους §1.3.2.4 |
| [IMPACT EliseCoreDocument Structure](https://developers.s1ecos.com/elisecoredocument-structure-869821m0) | Δομή JSON, `Details`, `Summaries`, `IncomeClassification`, `Taxes` |
| [IMPACT 1.1 B2B JSON Sample](https://developers.s1ecos.com/1-1-b2b-1046955m0) | Πραγματικό δημοσιευμένο παράδειγμα πεδίων PascalCase και άμεσου `IncomeClassification.ClassificationTypeCode` |
| [IMPACT 11.1 B2C Example](https://developers.s1ecos.com/11-1-b2c-retail-sales-receipt-1046970m0) | Επιβεβαίωση ότι τα παραστατικά λιανικής δεν έχουν καθολικά την ίδια υποχρεωτικότητα αντισυμβαλλόμενου |
| [IMPACT Classification & Payment Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Ξεχωριστή επαλήθευση E3/κατηγοριών/μεθόδων πληρωμής |
| [IMPACT VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Ξεχωριστή επαλήθευση συντελεστών/απαλλαγών/φόρων |
| [ΑΑΔΕ myDATA τεχνικές προδιαγραφές 2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Επίσημη έκδοση ERP/Παρόχου και XSDs |

### Ασυμφωνία επίσημων πηγών — απαιτείται ανεξάρτητο review

Η §1.3.2.2 αναγράφει `Detail.IncomeClassification.Classification.TypeCode`, ενώ το δημοσιευμένο B2B sample έχει `Details[].IncomeClassification.ClassificationTypeCode` (χωρίς αντικείμενο `Classification`). Επιπλέον, το sample παραλείπει μερικές φορές το `IncomeClassification.Amount`, αν και η §1.3.2.2 το απαριθμεί στα required fields.

Η offline υλοποίηση υιοθετεί **τη μορφή πραγματικού δημοσιευμένου sample** για το `ClassificationTypeCode`, αλλά **επισημαίνει** το `Amount` ως απαιτούμενο από τη γραπτή ενότητα. Το report διατηρεί ρητό `IncomeClassificationShapeAmbiguous` για κάθε γραμμή. Αυτή η προσωρινή απόφαση **δεν πιστοποιεί** πραγματικό provider schema.

## 3. Προϋποθέσεις

- Εγκατεστημένο .NET 10 SDK σύμφωνα με το `global.json` του repository, Development περιβάλλον Windows.
- Συγχρονισμένη έκδοση Aenaon ERP.
- Ένα **τοπικό συνθετικό JSON** που δεν περιέχει προσωπικά δεδομένα, πραγματικά ΑΦΜ, πελάτες ή κλειδιά.
- Πρόσβαση στο PowerShell και, προαιρετικά, σε φάκελο `C:\FiscalEvidence\` με δυνατότητα δημιουργίας νέων αρχείων.

Το πρόγραμμα **δεν** χρειάζεται σύνδεση SQL, bearer token ή σύνδεση με τον πάροχο.

## 4. Εγκατάσταση και έλεγχοι

Μετά το Squash and merge:

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git switch main
git pull --ff-only origin main
dotnet --version
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

## 5. Εκτέλεση με συνθετικό JSON

Για τη δημιουργία απλού, **μη πραγματικού** αρχείου δοκιμής:

```powershell
New-Item -ItemType Directory -Force "C:\FiscalEvidence" | Out-Null

@'
{
  "DocumentTypeCode": "INVOICE",
  "DateIssued": "2026-11-11T12:00:00",
  "Series": "TEST",
  "Number": "1",
  "CurrencyCode": "EUR",
  "InvoiceTypeCode": "1.1",
  "Issuer": {
    "Vat": "EL000000000",
    "RegisteredName": "SYNTHETIC ISSUER",
    "Address": { "CountryCode": "GR" }
  },
  "CounterParty": {
    "Vat": "EL000000000",
    "RegisteredName": "SYNTHETIC CUSTOMER",
    "Address": { "CountryCode": "GR" }
  },
  "DistributionDetails": { "InternalDocumentId": "SYNTHETIC-001" },
  "Details": [{
    "LineNo": 1,
    "UnitPrice": 100,
    "NetTotal": 100,
    "VatCategoryCode": 1,
    "VATTotal": 24,
    "IncomeClassification": {
      "ClassificationCategoryCode": "category1_1",
      "ClassificationTypeCode": "E3_561_001",
      "Amount": 100
    }
  }],
  "Summaries": {
    "TotalNetAmount": 100,
    "TotalVATAmount": 24,
    "TotalGrossValue": 124
  }
}
'@ | Set-Content -Path "C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json" -Encoding utf8
```

{% hint style="warning" %}
Οι κωδικοί/ποσά του παραδείγματος είναι **αποκλειστικά δοκιμαστικοί**. Δεν έχουν επαληθευτεί ως επιτρεπτός συνδυασμός για πραγματικό φορολογικό παραστατικό. Μην ανεβάσεις το δείγμα σε IMPACT και μην το χρησιμοποιήσεις σε πραγματική συναλλαγή.
{% endhint %}

Εκτέλεσε:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-payload --profile invoice --scenario business --payload-file "C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json" --report-json "C:\FiscalEvidence\IMPACT_OfflineStructuralReport.json"
```

### Διαθέσιμες παράμετροι

| Παράμετρος | Τιμή | Χρήση |
| --- | --- | --- |
| `--profile` | `base`, `invoice`, `b2g` | Επιλογή συνόλου βασικών απαιτήσεων |
| `--scenario` | `unknown`, `business`, `retail` | **Ρητή** επιλογή τύπου αντισυμβαλλόμενου, χωρίς inference από παραστατικό |
| `--payload-file` | `PATH.json` | Τοπικό JSON έως 4 MiB |
| `--report-json` | `NEW_FILE.json` | Προαιρετική νέα εξαγωγή, χωρίς αντικατάσταση παλιών reports |

**Προσοχή:** `--profile b2g --scenario retail` απορρίπτεται. Αν ο τύπος αντισυμβαλλόμενου δεν είναι τεκμηριωμένος, χρησιμοποίησε `unknown` και διατήρησε εκκρεμή τον αντίστοιχο ανθρώπινο έλεγχο.

## 6. Τι πραγματικά ελέγχεται

- Υπάρχει έγκυρο JSON object, χωρίς σχόλια, trailing commas, διπλές ονομασίες πεδίων χωρίς διάκριση κεφαλαίων/πεζών.
- Οι βασικές απαιτήσεις `DocumentTypeCode`, `DateIssued`, `Series`, `Number`, `Issuer.Vat`, `DistributionDetails.InternalDocumentId`, οι υποχρεωτικές περιλήψεις και τουλάχιστον μία γραμμή `Details`.
- Για `invoice`/`b2g`: τα ρητά `CurrencyCode`, `InvoiceTypeCode`, `Issuer.Address.CountryCode`, `Issuer.RegisteredName`, τα δημοσιευμένα απαιτούμενα πεδία ανά γραμμή και το `INVOICE` ως document type.
- Για `business`: `CounterParty.Address.CountryCode`, `CounterParty.RegisteredName`, `CounterParty.Vat`. Για `retail` δεν τα απαιτεί. Για `unknown` προσθέτει review gate.
- Για `b2g`: `Detail.MeasurementUnitCodeEN`, `Detail.CpvCode`, `B2GDetails.ContractingAuthorityCode`, `ContractReferenceCode` και `ContractTypeCode`.
- Επισημαίνεται πιθανή διπλή τοποθέτηση φόρων μεταξύ `Taxes` και `Details` (§1.3.2.4).

## 7. Τι **δεν** ελέγχεται (πάντα RequiresReview)

| Εκκρεμότητα | Γιατί παραμένει ανοιχτή |
| --- | --- |
| `ClassificationMatrixPending` | Τα **1.403** υποψήφια ζεύγη Ε3 δεν έχουν εγκριθεί ανεξάρτητα· τα 254 ειδικά cases παραμένουν Pending |
| `VatAndTotalsPending` | Δεν ελέγχονται πραγματική νομιμότητα συντελεστών, αιτίες απαλλαγής, στρογγυλοποιήσεις και σύνολα |
| `ConditionalFieldsPending` | Τα **37** υπό όρους πεδία χρειάζονται πραγματικά δεδομένα συναλλαγής και νομικό context |
| `CorrelatedInvoicesPending` | Οι υποχρεώσεις συσχέτισης εξαρτώνται από τύπο παραστατικού και source review |
| `IncomeClassificationShapeAmbiguous` | Διαφωνία γραπτής λίστας πεδίων έναντι B2B δείγματος IMPACT |
| `ProviderAcceptanceUnverified` | Καμία δοκιμή/επιβεβαίωση από πραγματικό API |
| `B2GContractPending` | Απαιτείται ανεξάρτητος έλεγχος CPV, πραγματικής σύμβασης, EN16931 και GSIS |

Το εργαλείο **δεν κάνει** καμία κλήση HTTP και δεν παράγει έτοιμο request για `/Invoice/json`.

## 8. Ερμηνεία αποτελέσματος

Η έξοδος περιέχει:

- `Δομικά σφάλματα: X` — πεδία ή μορφές JSON που λείπουν ή είναι ακατάλληλα βάσει των ενσωματωμένων, περιορισμένων ελέγχων.
- `Ανεξάρτητοι έλεγχοι που εκκρεμούν: Y` — περιπτώσεις που το εργαλείο **δεν επιλύει**.
- Κωδικούς ευρημάτων και JSON paths χωρίς εκτύπωση τιμών ΑΦΜ, ποσών, στοιχείων πελατών ή άλλων πεδίων του payload.
- `IsActualProviderContractValidated=false` και `CanIssueFiscalDocument=false` στο report ακόμη και όταν `X=0`.

Κωδικοί εξόδου:

| Κωδικός | Σημασία |
| --- | --- |
| `0` | Δεν βρέθηκαν τοπικά δομικά σφάλματα· **όχι provider approval** |
| `1` | Σφάλμα file access ή δημιουργίας νέου report |
| `2` | Άκυρα ορίσματα, λάθος profile/scenario ή μη έγκυρο JSON |
| `3` | Βρέθηκαν τοπικά δομικά σφάλματα |
| `130` | Ακύρωση με `Ctrl+C` |

## 9. Troubleshooting

| Πρόβλημα | Ενέργεια |
| --- | --- |
| `MissingDocumentedField` | Έλεγξε ότι υπάρχει το αντίστοιχο πεδίο στην ακριβή διαδρομή· δες το αντίστοιχο § IMPACT |
| `InvalidDocumentedValue` | Το πεδίο έχει λάθος JSON type ή κενή τιμή· μην μετατρέπεις αυθαίρετα κωδικούς |
| `WrongDocumentType` | `invoice`/`b2g` προϋποθέτουν `DocumentTypeCode=INVOICE` |
| `IncomeClassificationShapeAmbiguous` | Δες §1.3.2.2 και το πραγματικό B2B sample· ζήτησε επιβεβαίωση schema από IMPACT |
| `PossibleTaxPlacementConflict` | Υπάρχουν μη κενά φορολογικά πεδία σε `Details` και `Taxes`. Απαιτείται εξέταση και διόρθωση/τεκμηρίωση |
| Η αναφορά υπάρχει ήδη | Δώσε νέο όνομα `--report-json` για να μη χαθεί παλιό audit |
| `unknown` scenario | Αναμενόμενο επιπλέον `CounterpartyScenarioUnknown` |
| Η εφαρμογή γράφει πάντα `CanIssueFiscalDocument=false` | **Σκόπιμη προστασία**, όχι δυσλειτουργία |

## 10. Ασφάλεια / διαχείριση / rollback

- **Μόνο Development.** Εκτέλεση σε παραγωγικό ASP.NET environment απορρίπτεται.
- Χωρίς τρίτα NuGet packages, χωρίς SQL migrations, DB writes, provider requests ή αλλαγές δικαιωμάτων.
- Το JSON διαβάζεται τοπικά μέχρι 4 MiB, με μέγιστο XML/JSON depth εδώ **32** (μόνο JSON), όριο 10.000 γραμμών και 1.000 ευρημάτων.
- Διπλά properties (case-insensitive) απορρίπτονται, ώστε διαφορετικές ερμηνείες JSON parsers να μη δημιουργούν αμφισημία.
- Το προαιρετικό report δημιουργείται με `CreateNew`· διατηρείται πρωτότυπο χωρίς αλλαγές.
- Δεν γράφονται στοιχεία πελατών στα logs, παρά μόνο profile, πλήθος λαθών και εκκρεμείς έλεγχοι.
- Δοκιμές μόνο με συνθετικές τιμές. Για μελλοντική πραγματική χρήση χρειάζονται κανόνες ασφαλούς διαχείρισης PII, access control, καταγραφή ανεξάρτητων εγκρίσεων και συμβατά schema tests.
- **Rollback:** Git revert αυτού του PR. Δεν υφίσταται SQL migration να αναστραφεί.

## 11. Checklist

- [ ] Ελέγχθηκαν οι επίσημες πηγές και η ασυμφωνία IncomeClassification.
- [ ] Απαιτείται ρητή επιλογή `--profile` και `--scenario`.
- [ ] Το τεστ JSON είναι **απολύτως συνθετικό** και εκτελείται μόνο τοπικά.
- [ ] Ελέγχθηκαν βασικά required fields, B2B/B2C διαφορά και B2G απαιτήσεις.
- [ ] Προβληματική επανάληψη φόρων μεταξύ `Taxes`/`Details` επισημαίνεται.
- [ ] Δεν γίνεται αποδοχή malformed JSON, διπλών keys ή μεγάλων uploads.
- [ ] Το report δεν μπορεί να εκδώσει, εγκρίνει, καταχωρίσει ή στείλει παραστατικό.
- [ ] Unit tests, build, formatting και SQL Server 2022 integration CI ελέγχθηκαν.
- [ ] Καμία νέα migration / λειτουργική αλλαγή στην SQL.

## 12. Επόμενο στάδιο

**9.3C.3B.5D — Official Schema Reconciliation & Tested Conditional Rules**: διάγνωση της ασυμφωνίας `IncomeClassification` με επίσημο schema/provider guidance, ελεγχόμενη εφαρμογή των 37 υπό όρους πεδίων βάσει πραγματικού approved scenario, ανεξάρτητη συμφωνία ΑΑΔΕ/IMPACT και golden payload tests. Μέχρι τότε **κανένα payload δεν είναι έτοιμο για αποστολή**.
