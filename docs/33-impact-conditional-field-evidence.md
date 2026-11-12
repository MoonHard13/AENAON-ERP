# Aenaon ERP — Βήμα 9.3C.3B.5D.2: Τεκμήρια παρουσίας υπό όρους πεδίων IMPACT

## Σκοπός

Μετά τον επιτυχή τοπικό έλεγχο του συνθετικού παραστατικού (`InvoiceIapr / Business`, **0 δομικά σφάλματα, 6 εκκρεμείς έλεγχοι**), το παρόν βήμα εμπλουτίζει την offline αναφορά με **αναλυτική καταγραφή παρουσίας** των υπό όρους πεδίων IMPACT.

Πρόκειται για παρατήρηση **όχι** για απόφαση υποχρεωτικότητας. Για παράδειγμα, εάν λείπει `Detail.MeasurementUnitCode`, δεν μπορεί να χαρακτηριστεί αυθαίρετα σφάλμα σε μια υπηρεσία. Αντίστοιχα, η απουσία `CorrelatedInvoices` δεν αποτελεί από μόνη της σφάλμα, χωρίς τεκμηριωμένο τύπο συσχέτισης.

{% hint style="danger" %}
Η ύπαρξη, απουσία ή μη-null τιμή ενός πεδίου **δεν αποδεικνύει** αν είναι νομικά απαιτούμενο ούτε αν είναι αποδεκτό από IMPACT/ΑΑΔΕ. Το εργαλείο δεν αποφασίζει τη φύση της συναλλαγής, δεν εγκρίνει φορολογικούς κανόνες και δεν επιτρέπει έκδοση παραστατικών.
{% endhint %}

## Επίσημες πηγές

- [IMPACT — Minimum & Conditional Fields, §1.3.2.2–1.3.2.4](https://developers.s1ecos.com/minimum-conditional-fields-869822m0): επιπλέον απαιτήσεις INVOICE προς IAPR, εξαιρέσεις για υπηρεσίες/λιανική, πρόσθετες υπό όρους περιπτώσεις. Στη §1.3.2.4 οι φόροι δηλώνονται στα `Taxes` ή στα `Details`, όχι ταυτόχρονα.
- [IMPACT — EliseCoreDocument Structure](https://developers.s1ecos.com/elisecoredocument-structure-869821m0): οργάνωση στοιχείων σε `Details`, `Taxes`, `Summaries` και αντισυμβαλλομένους.
- [IMPACT — Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0): τεχνικοί κωδικοί χαρακτηρισμών/πληρωμής (απαιτούν ξεχωριστή επαλήθευση).
- [ΑΑΔΕ — myDATA 2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata): επίσημη τεχνική περιγραφή και XSDs, διακριτή ροή ERP/Παρόχων.

Η συγκεκριμένη καταγραφή βασίζεται στον ήδη ενσωματωμένο `ImpactContractPreflightCatalog`. Δεν εισάγονται νέα φορολογικά δεδομένα ούτε αναγνωρίζονται ως εγκεκριμένοι οι 1.403 υποψήφιοι συνδυασμοί ή οι 254 εκκρεμείς παρατηρήσεις του XLSX ΑΑΔΕ.

## Προϋποθέσεις

- Windows / PowerShell και .NET SDK σύμφωνα με το `global.json` (στην παρούσα έκδοση `10.0.404`).
- Development περιβάλλον και ενημερωμένο `main` του Aenaon ERP.
- Το **συνθετικό**, μη πραγματικό JSON `C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json` από το προηγούμενο βήμα.
- Χωρίς πραγματικά ΑΦΜ, πελάτες, φορολογικές αποφάσεις, κλειδιά ή access tokens στο test αρχείο.

## Εγκατάσταση / έλεγχος κώδικα

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

Τα διαθέσιμα CI ελέγχουν επίσης το solution σε Windows και Ubuntu και δοκιμές SQL Server 2022 σε απομονωμένο περιβάλλον.

## Εκτέλεση CLI

Δεν απαιτείται αλλαγή στις παραμέτρους της υφιστάμενης εντολής:

```powershell
$report = "C:\FiscalEvidence\IMPACT_ConditionalEvidence_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"

dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-payload --profile invoice --scenario business --payload-file "C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json" --report-json $report
```

Το CLI επαναλαμβάνει τα ήδη υπάρχοντα αποτελέσματα για τα δομικά σφάλματα και τα ανεξάρτητα review gates και προσθέτει:

```text
Υπό όρους πεδία σε τεχνική καταγραφή: 37
Με μη-null παρουσία: ...
Με null παρουσία: ...
Η παρουσία/απουσία δεν επιβεβαιώνει αν το πεδίο απαιτείται σε πραγματική συναλλαγή.
```

Οι αριθμοί παρουσίας εξαρτώνται από το **συνθετικό** JSON. Ο έλεγχος `InvoiceIapr` περιλαμβάνει **37** υπό όρους εγγραφές. Στο `InvoiceB2G` οι εγγραφές είναι **36**, επειδή το `Detail.MeasurementUnitCodeEN` μεταφέρεται στις πρόσθετες υποχρεωτικές απαιτήσεις B2G.

## Περιγραφή πεδίων αναφοράς

Στο εξαγόμενο JSON, κάθε `ConditionalEvidence[]` έχει:

| Πεδίο | Περιγραφή |
| --- | --- |
| `PublishedField` | Ονομασία όπως είναι καταχωρισμένη στον κατάλογο IMPACT |
| `SourceSection` | Επίσημη ενότητα, π.χ. `1.3.2.2` ή `1.3.2.4` |
| `SourceUrl` | Παραπομπή στο επίσημο τεχνικό εγχειρίδιο |
| `LocationsExamined` | Πόσα πραγματικά containers ή line items εξετάστηκαν |
| `ObservedNonNull` | Σε πόσα σημεία εντοπίστηκε το πεδίο με τιμή διαφορετική από `null` |
| `ObservedNull` | Σε πόσα σημεία το πεδίο υπήρχε, αλλά είχε JSON `null` |
| `NotObserved` | Πόσα από τα εξετασμένα σημεία δεν είχαν το πεδίο |
| `ContainerShapeNeedsReview` | Ακατάλληλη μορφή `Details` ή `Taxes` για αξιόπιστη καταγραφή |
| `IsApplicabilityDetermined` | **Πάντα false** |
| `IsFiscalRuleApproved` | **Πάντα false** |
| `IsProviderValidated` | **Πάντα false** |

Για πεδία `Detail.*` αναλύονται οι γραμμές `Details[]`. Για `Taxes.*` αναλύεται το `Taxes[]`. Τα υπόλοιπα πεδία ελέγχονται από το root της συναλλαγής μέσω των δηλωμένων nested paths. Τα ονόματα αναγνωρίζονται case-insensitive, αλλά τα διπλά properties απορρίπτονται όπως στο προηγούμενο βήμα.

{% hint style="warning" %}
Εάν το `Taxes` απουσιάζει, `LocationsExamined=0` και `NotObserved=0`. Αυτό **δεν** σημαίνει ότι οι φόροι είναι άσχετοι ή ότι δεν απαιτούνται. Το εργαλείο δεν έχει ακόμη εγκεκριμένο συμβάν/κανόνα εφαρμογής.
{% endhint %}

## Πότε χρησιμοποιείται

Χρησιμοποιείται για να συγκρίνουμε με επίσημες πηγές τι **όντως περιέχει** ένα συνθετικό payload, χωρίς έκθεση τιμών, και για να προετοιμάσουμε review από τεχνικό/φοροτεχνικό.

**Δεν χρησιμοποιείται** για απόφαση αν μια πραγματική συναλλαγή είναι προϊόν ή υπηρεσία, για τον καθορισμό είδους πιστωτικού/συσχετιζόμενου, για εφαρμογή ΦΠΑ, συμμόρφωση B2G, ενεργοποίηση issuance ή πραγματικό provider schema test.

## Ασφάλεια και περιορισμοί

- Development-only, read-only είσοδος JSON και μόνο προαιρετική δημιουργία **νέας** τοπικής αναφοράς με `FileMode.CreateNew`.
- Δεν χρησιμοποιούνται SQL migrations, SQL writes, HTTP requests ή παραγωγικά δεδομένα.
- Η έξοδος `ConditionalEvidence` αποθηκεύει **μόνο ονόματα, counts και τεχνικές παραπομπές**, όχι ΑΦΜ, ποσά, ονόματα πελατών, κωδικούς, URLs παραστατικών ή πραγματικές τιμές.
- Η υπάρχουσα απόρριψη κατεστραμμένου JSON, διπλών ονομάτων και η ασφαλής υποστήριξη UTF-8 BOM διατηρούνται.
- Αμετάβλητα exit codes: `0` μόνο «δεν εντοπίστηκε δομικό σφάλμα», `1` σφάλμα αρχείων, `2` άκυρο JSON/παράμετροι, `3` δομικά σφάλματα, `130` ακύρωση.
- Καμία από τις σημαίες έγκρισης ή έκδοσης δεν αλλάζει.

## Troubleshooting

| Πρόβλημα | Ενέργεια |
| --- | --- |
| Αναφορά JSON υπάρχει ήδη | Χρησιμοποίησε νέο όνομα στο `--report-json`, μην καταστρέφεις ιστορικό |
| `ObservedNull > 0` | Το πεδίο υπάρχει ως `null`, **όχι** ως έγκυρη επιβεβαιωμένη τιμή |
| `ContainerShapeNeedsReview=true` | Έλεγξε το αντίστοιχο `Details` / `Taxes` container και την επίσημη δομή |
| `NotObserved > 0` | Εξέτασε κατά πόσο η αντίστοιχη περίπτωση ισχύει· μην συμπληρώνεις αυθαίρετα |
| Παραμένουν 6 review gates | Αναμενόμενο: οι παρατηρήσεις παρουσίας δεν επιλύουν τα απαιτούμενα ανεξάρτητα reviews |

## Backup, ενημερώσεις, rollback

Διατήρησε τα συνθετικά JSON, τα reports και τα τεκμήρια της ΑΑΔΕ σε προστατευμένο φάκελο εκτός repository. Δημιούργησε νέο report για κάθε έκδοση. Για rollback του κώδικα μπορεί να χρησιμοποιηθεί Git revert του PR· **δεν υπάρχει νέα migration**.

## Checklist

- [ ] Το υπάρχον συνθετικό InvoiceIapr εξακολουθεί να δίνει 0 δομικά σφάλματα / 6 review gates.
- [ ] Η αναφορά έχει 37 καταγραφές `ConditionalEvidence` στο InvoiceIapr και 36 στο B2G.
- [ ] Η παρουσία, το `null` και η απουσία δεν παράγουν αυτομάτως έγκριση.
- [ ] Γίνεται σωστή καταμέτρηση σε `Details[]` και `Taxes[]`.
- [ ] Καμία πραγματική τιμή συνθετικού ή παραγωγικού payload δεν συμπεριλαμβάνεται στο `ConditionalEvidence`.
- [ ] Οι παλαιότερες 254 εκκρεμείς φορολογικές παρατηρήσεις παραμένουν Pending.
- [ ] Έγινε επιτυχής έλεγχος unit/integration/CI πριν το merge.

## Επόμενο βήμα

**9.3C.3B.5D.3 — Τεκμηριωμένες συνθήκες εφαρμογής / επιβεβαίωση IMPACT.** Για να αξιολογηθούν πραγματικά οι 37 εγγραφές απαιτείται versioned, ανεξάρτητα επιβεβαιωμένο πλαίσιο συναλλαγής (τύπος παραστατικού, αγαθό/υπηρεσία ανά γραμμή, είδος συσχέτισης, φορολογικό καθεστώς, B2B/B2C/B2G) και γραπτές διευκρινίσεις του παρόχου όπου οι επίσημες πηγές διαφωνούν. Δεν κατασκευάζουμε ακόμα αυτόματη φορολογική έγκριση.
