# Aenaon ERP — 9.3C.3B.5D.1: Συμφωνία πηγών για το IncomeClassification

## 1. Σκοπός

Μετά την επιτυχή τοπική εκτέλεση του **Offline IMPACT Structural Preflight** (InvoiceIapr / Business, μία γραμμή, **0 δομικά σφάλματα και 6 εκκρεμείς έλεγχοι**) εξετάζουμε συγκεκριμένα δύο αντιφάσεις των δημοσιευμένων οδηγιών IMPACT.

Στόχος είναι η διακριτή καταγραφή των διαφορετικών μορφών JSON και των πεδίων που παραλείπονται από δείγματα. **Δεν εγκρίνουμε νέο σχήμα, δεν διορθώνουμε αυτόματα παραστατικά και δεν υποκαθιστούμε την επιβεβαίωση του παρόχου.**

{% hint style="danger" %}
Η συμφωνία πηγών είναι **τεχνική αναγνώριση αμφισημιών**, όχι πιστοποίηση. Οι ιδιότητες `IsActualProviderContractValidated`, `IsAadeClassificationApproved`, `IsFiscalRuleApproved` και `CanIssueFiscalDocument` παραμένουν **false**. Δεν εκδίδεται και δεν διαβιβάζεται κανένα παραστατικό.
{% endhint %}

## 2. Επίσημες πηγές (12 Νοεμβρίου 2026)

| Πηγή | Ακριβές σχετικό τεκμήριο |
| --- | --- |
| [IMPACT — Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Ενότητα **§1.3.2.2**: `Detail.IncomeClassification.Classification.TypeCode` και `Detail.IncomeClassification.Amount` στη λίστα υποχρεωτικών |
| [IMPACT — EliseCoreDocument Structure](https://developers.s1ecos.com/elisecoredocument-structure-869821m0) | Ενότητα **§1.5.12 Classification**: `CLASSIFICATIONTYPECODE`, `CLASSIFICATIONCATEGORYCODE`, `AMOUNT` στο ίδιο αντικείμενο |
| [IMPACT — 1.1 B2B JSON Sample](https://developers.s1ecos.com/1-1-b2b-1046955m0) | Πρώτο παράδειγμα: επίπεδο `IncomeClassification.ClassificationTypeCode` και **χωρίς** `Amount`. Δεύτερο παράδειγμα (recType 6): περιλαμβάνει `Amount` σε μία γραμμή |
| [IMPACT — Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Περιγράφει `incomeClassification.ClassificationTypeCode`, με παραπομπή στο myDATA |
| [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Έκδοση **2.0.2** και διακριτή τεχνική περιγραφή καναλιού ERP / παρόχων |

### Εκκρεμότητα Α: επίπεδο ή ένθετο TypeCode;

- Η λεκτική λίστα §1.3.2.2 περιγράφει ένθετη διαδρομή.
- Η περιγραφή του αντικειμένου Classification, τα JSON samples και οι κωδικοί χαρακτηρισμού δείχνουν **επίπεδο** `ClassificationTypeCode`.
- Δεν υπάρχει ακόμη ανακτημένη, επίσημη, δεσμευτική διευκρίνιση ή επιβεβαιωμένο provider response για πραγματικό αίτημα.

**Προσωρινή πολιτική:** το offline εργαλείο διατηρεί το επίπεδο path ως απαιτούμενο για τους τοπικούς ελέγχους. Αν βρει μόνο την ένθετη μορφή, αναφέρει το πρόβλημα και δεν περνά δομικά. Αν βρει **και τις δύο μορφές**, απορρίπτει την ασάφεια ακόμη κι αν τα κείμενα είναι ίδια. Αυτό δεν αποδεικνύει ότι το επίπεδο path είναι εγγυημένα συμβατό στο παραγωγικό IMPACT.

### Εκκρεμότητα Β: είναι πάντα υποχρεωτικό το Amount;

- Η §1.3.2.2 το απαριθμεί ως απαιτούμενο.
- Το πρώτο επίσημο B2B 1.1 sample δεν το περιλαμβάνει μέσα στο `IncomeClassification`.
- Άλλο sample της ίδιας σελίδας περιλαμβάνει `Amount` (ακόμη και μηδενικό).

**Προσωρινή πολιτική:** η απουσία του `Amount` εξακολουθεί να επισημαίνεται ως **δομικό σφάλμα** βάσει της ρητής required λίστας, αλλά συνοδεύεται από ανεξάρτητο **Review** με τον κωδικό `ClassificationAmountSampleDiscrepancy`. Δεν συμπληρώνουμε αυτόματα `Amount=NetTotal` και δεν υποθέτουμε ότι το sample κάνει το πεδίο προαιρετικό.

## 3. Συμπεριφορά νέων ελέγχων

| Συνθετικό JSON ανά γραμμή | Εύρημα | Αποτέλεσμα |
| --- | --- | --- |
| Μόνο `ClassificationTypeCode` και `Amount` | Υφιστάμενο `IncomeClassificationShapeAmbiguous` | Δομικός έλεγχος χωρίς νέο σφάλμα· **εκκρεμεί review** |
| Μόνο `Classification.TypeCode` | `NestedClassificationTypeCodeUnverified` και έλλειψη του επίπεδου path | **Error + Review**, δεν επιτρέπεται σιωπηρή χρήση |
| Και οι δύο διαδρομές | `ConflictingClassificationShapes` | **Error**, ακόμη κι αν συμφωνούν οι τιμές |
| Ένθετο `Classification` χωρίς `TypeCode` | `UnexpectedNestedClassificationObject` | **Review**, παραμένουν οι άλλοι δομικοί έλεγχοι |
| Απουσία `Amount` | `ClassificationAmountSampleDiscrepancy` + `MissingDocumentedField` | **Error + Review**, απαιτείται επιβεβαίωση IMPACT |

Οι νέοι έλεγχοι δεν διαβάζουν ή δεν καταγράφουν τις τιμές πελατών, ΑΦΜ, E3, ποσών ή tokens. Εμφανίζουν μόνο **κωδικό**, **severity**, JSON path και ενότητα πηγής.

## 4. Προϋποθέσεις

- Windows Development, .NET SDK όπως ορίζεται στο `global.json`.
- Ενημερωμένο repository Aenaon ERP μετά το merge του PR.
- Συνθετικό και **όχι πραγματικό** JSON, π.χ. `C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json` από το προηγούμενο manual.
- Προαιρετικά, φάκελος προστατευμένων reports `C:\FiscalEvidence\`. Δεν απαιτούνται SQL credentials ή κλειδιά IMPACT.

## 5. Εγκατάσταση / δοκιμές

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

Το CI ελέγχει επίσης SQL Server 2022 integration, publish API και HTTPS health. Οι πρόσθετες δοκιμές χρησιμοποιούν **συνθετικές παραλλαγές**: επίπεδη/ένθετη/διπλή μορφή και `Amount` που λείπει.

## 6. Εκτέλεση CLI

Η εντολή παραμένει ίδια με το προηγούμενο βήμα, χωρίς νέες παραμέτρους:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-impact-payload --profile invoice --scenario business --payload-file "C:\FiscalEvidence\IMPACT_Synthetic_Invoice.json" --report-json "C:\FiscalEvidence\IMPACT_SourceReconciliation_20261112.json"
```

{% hint style="warning" %}
Το report JSON δημιουργείται **μόνο αν δεν υπάρχει ήδη**. Χρησιμοποίησε νέο όνομα, όχι overwrite παλιού αποτελέσματος. Το συνθετικό payload δεν μεταβάλλεται.
{% endhint %}

Για το αρχικό έγκυρο συνθετικό JSON αναμένουμε και πάλι **0 δομικά σφάλματα / 6 εκκρεμείς ελέγχους**. Οι νέες διαγνωστικές εγγραφές εμφανίζονται μόνο όταν στο JSON υπάρχει **ένθετη/μικτή** μορφή ή λείπει το `Amount`.

Κωδικοί εξόδου: `0` χωρίς διαπιστωμένα δομικά σφάλματα (όχι έγκριση), `1` I/O, `2` μη έγκυρα JSON/ορίσματα, `3` δομικά σφάλματα, `130` ακύρωση.

## 7. Τι μένει εκτός scope

- Δεν επικυρώνεται σχήμα από επίσημο OpenAPI/XSD IMPACT, ούτε υπάρχει επιβεβαίωση από provider sandbox.
- Δεν αλλάζουν οι 22 απαιτούμενες και 37 υπό όρους αναφορές του `ImpactContractPreflightCatalog`.
- Οι **1.403 υποψήφιοι χαρακτηρισμοί** και οι **254 εκκρεμείς παρατηρήσεις** ΑΑΔΕ παραμένουν μη εγκεκριμένοι.
- Δεν επιβεβαιώνονται νόμιμα VAT/E3 ποσά, τύποι παραστατικών, B2C/B2G υποχρεώσεις ή συσχετιζόμενα MARK.
- Δεν δημιουργείται μηχανή αυτόματων εγκρίσεων, μεταφορά σε SQL ή έκδοση/αποστολή.

## 8. Troubleshooting, ασφάλεια και συντήρηση

| Σφάλμα/εύρημα | Ενέργεια |
| --- | --- |
| `ConflictingClassificationShapes` | Αφαίρεσε τη μη τεκμηριωμένη διπλή δομή **μόνο από συνθετικό test**, όχι από πραγματικό φορολογικό αρχείο. Ζήτησε δεσμευτική διευκρίνιση |
| `NestedClassificationTypeCodeUnverified` | Εξέτασε το §1.3.2.2 έναντι της §1.5.12 και των επίσημων samples |
| `ClassificationAmountSampleDiscrepancy` | Επιβεβαίωσε εγγράφως πότε επιτρέπεται `Amount` να λείπει, αν ποτέ |
| `IncomeClassificationShapeAmbiguous` | Αναμενόμενο εκκρεμές review ακόμη και για επίπεδη δομή |
| `Μη έγκυρη ή διφορούμενη δομή JSON` | Έλεγξε σύνταξη, BOM, διπλά ονόματα, UTF-8. Ισχύει η ασφαλής διόρθωση προηγούμενου PR |
| Σταθερά `CanIssueFiscalDocument=false` | Αναμενόμενη και υποχρεωτική προστασία |

Το αρχείο εξετάζεται σε Development με όρια μεγέθους/βάθους, cancellation και μηδενικές δικτυακές κλήσεις. Το logging περιορίζεται σε πλήθος λαθών και review gates. Το Git revert του PR επαναφέρει την προηγούμενη συμπεριφορά χωρίς SQL migration.

## 9. Checklist

- [ ] Τεκμηριώθηκαν οι διαφορές γραπτής §1.3.2.2 έναντι δημοσιευμένου B2B 1.1.
- [ ] Εντοπίζονται ένθετη, επίπεδη και διπλή μορφή `ClassificationTypeCode`.
- [ ] Έλλειψη `Amount` δεν «θεραπεύεται» με αυτόματη αναπλήρωση.
- [ ] Δεν εμφανίζονται ευαίσθητες τιμές σε reports/logs.
- [ ] Τα νέα findings φέρουν κωδικούς, paths και section provenance.
- [ ] Οι παλαιότερες λειτουργίες CLI/JSON παραμένουν συμβατές.
- [ ] Τα approval/issuance flags παραμένουν false.
- [ ] Πράσινο CI Windows/Ubuntu και SQL Server 2022 integration.

## 10. Επόμενο στάδιο

**9.3C.3B.5D.2 — Provider schema confirmation / conditional rules:** θα χρειαστεί **επίσημο schema ή απάντηση τεχνικής υποστήριξης IMPACT** για το `IncomeClassification`, καθώς και χωριστός φοροτεχνικός έλεγχος των υπό όρους πεδίων. Χωρίς αυτά δεν πρέπει να εκδοθεί κανένα πραγματικό τιμολόγιο βάσει του offline inspector.
