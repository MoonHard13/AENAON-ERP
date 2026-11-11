# Aenaon ERP — Βήμα 9.3C.3B.5B: Read-only IMPACT Contract Preflight

## 1. Σκοπός

Η επιτυχής εξαγωγή της λίστας `AADE_Review_Worklist_Pending.csv` **δεν αποδεικνύει συμβατότητα** με το REST API του παρόχου IMPACT. Το συγκεκριμένο βήμα δημιουργεί **ανεξάρτητο, μη παραγωγικό κατάλογο απαιτήσεων** του `EliseCoreDocument` για τις τρεις περιπτώσεις `base`, `invoice`, `b2g`.

{% hint style="danger" %}
**Δεν υλοποιείται πραγματικός payload validator, φορολογική έγκριση ή αποστολή.** Ο κατάλογος είναι checklist ανάγνωσης της τεκμηρίωσης, όχι επιβεβαίωση ότι κάποιο πραγματικό invoice πληροί τους όρους. Τα flags `IsActualPayloadValidated`, `IsAadeComplianceApproved`, `IsImpactContractValidated` και `CanIssueFiscalDocument` παραμένουν **πάντα false**.
{% endhint %}

## 2. Επίσημη τεκμηρίωση — έλεγχος 11/11/2026

| Πηγή | Σκοπός |
| --- | --- |
| [IMPACT — Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Ρητά πεδία `EliseCoreDocument` στις ενότητες **1.3.2.1**, **1.3.2.2**, **1.3.2.3**, **1.3.2.4** |
| [IMPACT — Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Ξεχωριστοί κωδικοί εσόδων/εξόδων, τύποι χαρακτηρισμών και μέθοδοι πληρωμής |
| [IMPACT — VAT & Tax Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Κατηγορίες ΦΠΑ, αιτίες απαλλαγών και φόροι |
| [ΑΑΔΕ — myDATA 2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Επίσημο XSD, κανάλι ERP/Παρόχου, τύποι παραστατικών, κωδικοί και ειδικοί περιορισμοί |

{% hint style="warning" %}
Η τεκμηρίωση IMPACT διαχωρίζει τις **απαιτήσεις παρόχου** από τις **απαιτήσεις διαβίβασης στην ΑΑΔΕ**. Για παράδειγμα, η ύπαρξη `InvoiceTypeCode` ή `Detail.IncomeClassification...` δεν αρκεί για να κριθεί νόμιμος ένας συνδυασμός. Χρειάζεται επαλήθευση έναντι επίσημων κωδικών, XLSX ΑΑΔΕ και πραγματικού invoice context.
{% endhint %}

## 3. Τρία προφίλ

| Προφίλ CLI | Περιεχόμενο |
| --- | --- |
| `base` | 10 βασικές απαιτήσεις §1.3.2.1, όπως `documentTypeCode`, `dateIssued`, `series`, `number`, `issuer.vat`, `distributionDetails.internalDocumentId`, τουλάχιστον μία γραμμή και αθροίσματα |
| `invoice` | Βασικά + πρόσθετες απαιτήσεις §1.3.2.2 για `INVOICE` προς ΑΑΔΕ + ρητές υπό όρους περιπτώσεις §1.3.2.4 |
| `b2g` | `invoice` + πρόσθετες B2G απαιτήσεις §1.3.2.3 (`Detail.MeasurementUnitCodeEN`, `Detail.CpvCode`, τρεις κωδικοί σύμβασης/φορέα) |

Το `CorrelatedInvoices` απαιτείται **όταν το επιβάλλει ο τύπος παραστατικού**. Τα στοιχεία `CounterParty` καταγράφονται ως υποχρεωτικά εκτός λιανικών. Το `Detail.MeasurementUnitCode` έχει εξαίρεση στα παραστατικά υπηρεσιών, ενώ το `Detail.MeasurementUnitCodeEN` γίνεται υποχρεωτικό στο B2G. **Δεν** εφαρμόζουμε αυτόματα αυτές τις συνθήκες σε πραγματικές επιχειρήσεις.

## 4. Προϋποθέσεις

- .NET SDK όπως ορίζει το `global.json` (στην τρέχουσα έκδοση **10.0.404**).
- Development checkout της τρέχουσας έκδοσης του repository.
- Δεν απαιτούνται διαπιστευτήρια SQL Server, API token, πρόσβαση στο δίκτυο, το CSV των 254 παρατηρήσεων ή χρήση παραγωγικής βάσης.
- Για μελλοντική ανθρώπινη αξιολόγηση απαιτείται φοροτεχνικός που γνωρίζει τις πραγματικές περιπτώσεις της επιχείρησης.

## 5. Εγκατάσταση / έλεγχοι

Μετά το merge:

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

## 6. Χρήση CLI

Δες τα υποχρεωτικά πεδία για κάθε περίπτωση:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-contract --profile base
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-contract --profile invoice
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-contract --profile b2g
```

Προαιρετικά, δημιούργησε **νέο JSON checklist**:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-contract --profile b2g --report-json "C:\FiscalEvidence\IMPACT_B2G_Preflight_Pending.json"
```

Δεν αντικαθιστά υπάρχον αρχείο. Το νέο JSON είναι μόνο **κατάλογος δημοσιευμένων απαιτήσεων** — όχι πραγματικό provider payload και όχι αποτέλεσμα API test.

Κωδικοί εξόδου: `0` επιτυχής παραγωγή καταλόγου, `1` I/O, `2` άκυρα ορίσματα, `130` ακύρωση `Ctrl+C`.

## 7. Διάκριση υποχρεωτικών και υπό όρους πεδίων

Η αναφορά περιλαμβάνει:

| Πεδίο | Σημασία |
| --- | --- |
| `Profile` | `Base`, `InvoiceIapr`, `InvoiceB2G` |
| `DocumentationSnapshotDate` | Ημερομηνία τεχνικής επιθεώρησης των δημοσιευμένων πηγών |
| `Requirements[].PublishedField` | Ονομασία πεδίου όπως εμφανίζεται στην πηγή (δεν αποτελεί αυτόματο JSONPath) |
| `Requirements[].Kind` | `Required`, `Conditional` |
| `Requirements[].Applicability` | Η προϋπόθεση από τη δημοσιευμένη τεκμηρίωση |
| `Requirements[].SourceSection` / `SourceUrl` | Ενότητα και επίσημο URL |
| `Requirements[].ReviewQuestion` | Τι πρέπει να επαληθεύσει τεχνικός/φοροτεχνικός |
| `ReviewGates[]` | Εκκρεμείς έλεγχοι κωδικών χαρακτηρισμών, φόρων, αποκλεισμού διπλής τοποθέτησης φόρων, έκδοσης ΑΑΔΕ και B2G |
| `IsActualPayloadValidated` / `CanIssueFiscalDocument` | Πάντα **false** |

### Απαιτήσεις που δεν επιτρέπεται να παραβλεφθούν

1. **Classification:** Τα πεδία `Detail.IncomeClassification.ClassificationCategoryCode` και `Detail.IncomeClassification.Classification.TypeCode` δεν επικυρώνουν από μόνα τους επιτρεπτούς συνδυασμούς. Συγκρίνουμε με επίσημο XLS και πραγματικό invoice type.
2. **ΦΠΑ / φόροι:** Οι κατηγορίες και οι απαλλαγές από το IMPACT VAT & TAX Codes χρειάζονται πραγματικά ποσά, business context και ελέγχους.
3. **Ταυτόχρονοι φόροι:** Η §1.3.2.4 προειδοποιεί ότι φόροι δεν πρέπει να διαβιβάζονται ταυτόχρονα στο `Taxes` και στο `Details` για τον ίδιο τρόπο δήλωσης.
4. **B2G:** CPV και κωδικοί σύμβασης/φορέα επιβάλλουν πρόσθετο έλεγχο EN16931/GSIS.
5. **myDATA 2.0.2:** Το κανάλι ERP δεν είναι ταυτόσημο με το κανάλι πιστοποιημένου Παρόχου. Πρέπει να επιλεγεί η σωστή διαδικασία και οι κατάλληλες XSD/API απαιτήσεις.

## 8. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται:** πριν σχεδιαστούν DTOs/validators του `EliseCoreDocument`, ώστε κάθε πεδίο να έχει συγκεκριμένη πηγή και οι εξαιρέσεις να μην παραλείπονται.

**Δεν χρησιμοποιείται:** για έλεγχο παραγωγικού τιμολογίου, υπογραφή, διαβίβαση, αποστολή, έγκριση τύπου παραστατικού, εφαρμογή ΦΠΑ ή παραγωγική καταχώριση οικονομικών στοιχείων.

## 9. Troubleshooting και ασφάλεια

| Περίπτωση | Αντιμετώπιση |
| --- | --- |
| Άγνωστο profile | Επιτρεπτά μόνο `base`, `invoice`, `b2g` |
| Υπάρχει το report JSON | Χρησιμοποίησε νέο όνομα ώστε να διατηρηθεί η προηγούμενη έκδοση |
| Οι κωδικοί δεν ταυτίζονται με συγκεκριμένο παραστατικό | Πρόκειται για documentation checklist, όχι validator. Μην το αντιμετωπίσεις ως έγκριση |
| Η τεκμηρίωση παρόχου αλλάζει | Επανέλεγξε official docs και υποχρεωτικά fields και άνοιξε ξεχωριστό PR |
| Η εφαρμογή εμφανίζει `CanIssueFiscalDocument=false` | **Σκόπιμο**: απουσία εγκεκριμένου φορολογικού/παροχικού contract |

Η προεπισκόπηση είναι **Development-only** και δεν έχει SQL migrations, αποθήκευση διαπιστευτηρίων, API calls, αλλαγές ρόλων ή φορολογικές εγκρίσεις. Το logging διατηρεί μόνο profile, count πεδίων και count εκκρεμών gates.

## 10. Backup, rollback και συντήρηση

Κράτησε τα τοπικά JSON checklists σε ξεχωριστό φάκελο, εκτός Git, με ελεγχόμενη πρόσβαση. Σε νέα έκδοση IMPACT προγραμμάτισε αναθεώρηση ολόκληρης της snapshot, όχι αυτόματη διατήρηση θεωρούμενων «εγκεκριμένων» πεδίων.

Rollback με Git revert χωρίς εντολές SQL ή migration. Η εφαρμογή παραμένει **λειτουργικά κλειδωμένη για φορολογική έκδοση**.

## 11. Checklist

- [ ] Επίσημα URLs και ενότητες 1.3.2.1–1.3.2.4 επαληθευμένα.
- [ ] Το προφίλ `base` έχει τις 10 δημοσιευμένες βασικές απαιτήσεις.
- [ ] Τα `invoice` και `b2g` περιλαμβάνουν τις κατάλληλες πρόσθετες υποχρεώσεις.
- [ ] Υπό όρους πεδία δεν μετατρέπονται αυτόματα σε καθολικά υποχρεωτικά.
- [ ] CorrelatedInvoices, πεδία CounterParty και ειδικές μονάδες μέτρησης παραμένουν condition-aware.
- [ ] Οι έλεγχοι classification, VAT/tax, ΑΑΔΕ, φόρων και B2G είναι μη εγκεκριμένοι.
- [ ] Δεν πραγματοποιείται καμία πραγματική κλήση IMPACT/ΑΑΔΕ.
- [ ] Windows/Ubuntu CI, SQL Server integration, MSTest και packaging green.
- [ ] Καμία νέα migration ή επίδραση στα 254 Pending items.

## 12. Επόμενο στάδιο

**9.3C.3B.5C — Offline JSON Contract Validator:** μετά από μελέτη των **πραγματικών schemas** του `EliseCoreDocument` και έγκριση του validation scope, κατασκευάζουμε απομονωμένο test-only validator με φανταστικά δεδομένα και αυστηρές συνθήκες B2B/B2C/B2G. Δεν επιτρέπεται να μεταφέρει τίποτα στο IMPACT χωρίς ξεχωριστή έγκριση ασφαλείας.
