# Aenaon ERP — 9.3C.3B.4: Semantic Review Triage (ΑΑΔΕ / IMPACT)

{% hint style="info" %}
**Επόμενο υποβήμα 9.3C.3B.5A:** με `--worklist-csv NEW.csv` το ίδιο CLI παράγει Excel-compatible λίστα των εκκρεμών παρατηρήσεων, με κενά πεδία ανθρώπινων σημειώσεων. Η επιλογή είναι εναλλακτική του `--report-json`. Βλ. [οδηγό λίστας φοροτεχνικού ελέγχου](29-fiscal-review-worklist-csv.md). Δεν υλοποιεί approval.
{% endhint %}

## 1. Σκοπός

Η προηγούμενη διαδικασία απέδειξε την **τεχνική συμφωνία** μεταξύ πρωτότυπου XLSX και πλήρους JSON. Αυτό το βήμα προετοιμάζει **όλες τις πραγματικές εκκρεμότητες για ανθρώπινο φοροτεχνικό έλεγχο**, με σταθερή αναφορά SHA-256, αριθμό φύλλου, κελί, αυτούσιο περιεχόμενο, προτεινόμενο είδος οδηγίας, προτεραιότητα και κοντινά πραγματικά κελιά.

{% hint style="danger" %}
Το αποτέλεσμα είναι αποκλειστικά **φάκελος εκκρεμών παρατηρήσεων**. Η αυτόματη ταξινόμηση δεν συνιστά φορολογική αξιολόγηση, δεν αποτελεί έγκριση κανόνα και δεν μπορεί να ενεργοποιήσει τιμολόγηση. `IsFiscalRuleApproved=false`, `CanIssueFiscalDocument=false`, `IsOriginalAuthenticityVerified=false` και `IsImpactContractValidated=false` παραμένουν **πάντα false**. **Δεν υπάρχει κουμπί/CLI «Approve».**
{% endhint %}

## 2. Επίσημες πηγές — 11/11/2026

| Πηγή | Περιεχόμενο προς διασταύρωση |
| --- | --- |
| [ΑΑΔΕ: Τεχνικές προδιαγραφές και εκδόσεις myDATA](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | myDATA **2.0.2**, XLS συνδυασμών και χωριστές προδιαγραφές ERP, Παρόχου, Ψηφιακής Διακίνησης |
| [ΑΑΔΕ: συγκεκριμένο XLSX 2.0.2](https://www.aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx) | Πρωτότυπο τεκμήριο που συσχετίζεται με το JSON SHA-256 |
| [IMPACT: Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Πραγματικές κατηγορίες/τύποι χαρακτηρισμών, διαφορετικοί από τις οδηγίες φύλλου |
| [IMPACT: VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Ανεξάρτητοι κανόνες ΦΠΑ και απαλλαγών |
| [IMPACT: Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Υποχρεωτικότητα πεδίων και πρόσθετες περιπτώσεις B2G |

Η ΑΑΔΕ αναφέρει, μεταξύ άλλων, νέα στοιχεία διακίνησης στην έκδοση 2.0.2. **Έλεγχος E3 ≠ πλήρης έλεγχος τύπου παραστατικού, ΦΠΑ, διακίνησης ή payload Παρόχου.**

## 3. Προϋποθέσεις

- Windows Development, .NET SDK του `global.json`, συγχρονισμένο repository.
- Το πραγματικό πρωτότυπο `syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx` **χωρίς αλλαγές**.
- Το πλήρες `AADE_myDATA_FullEvidence.json` από προηγούμενη έκδοση του CLI.
- Κοινό και ανεξάρτητα επαληθευμένο SHA-256. Για το αρχείο που εξετάσαμε: `E541A87E9B2F8F320A3996253A8D47EFA0D21A2617FA45767F146A182CAFA9E8`.
- Φάκελος `C:\FiscalEvidence\` εκτός Git repository, δικαιώματα δημιουργίας **νέου** JSON.
- Δεν απαιτείται SQL Server, API key ή οποιοδήποτε credentials IMPACT.

## 4. Εγκατάσταση και δοκιμές

Από Windows PowerShell στη ρίζα του project:

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git fetch origin
git switch --track origin/feature/9-3c-3b4-semantic-review-dossier

dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Μετά από merge χρησιμοποίησε `git switch main` και `git pull --ff-only origin main`.

## 5. Δημιουργία φακέλου ειδικών περιπτώσεων

Εντολή για ολοκληρωμένη **read-only σύγκριση XLSX/JSON** και δημιουργία **καινούργιου** report:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --triage-fiscal-semantics --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --evidence-json "C:\FiscalEvidence\AADE_myDATA_FullEvidence.json" --report-json "C:\FiscalEvidence\AADE_SemanticReview_Pending.json"
```

Για μόνο συνοπτικό έλεγχο χωρίς δημιουργία αρχείου, αφαίρεσε το `--report-json`.

Ο κώδικας **πρώτα** επανελέγχει το πραγματικό XLSX έναντι του πλήρους JSON. Αν δεν συμφωνούν SHA-256, κελιά, merged ranges, υποψήφια ζεύγη και εκκρεμότητες, η διαδικασία σταματά. Έπειτα ξαναδιαβάζει το αρχείο και ελέγχει ότι το πρωτότυπο δεν άλλαξε ενδιάμεσα.

{% hint style="warning" %}
Εάν το `AADE_SemanticReview_Pending.json` υπάρχει ήδη, η εντολή δεν το αντικαθιστά. Δημιούργησε νέο όνομα για νέα έκδοση. Μην αλλοιώνεις το πρωτότυπο, τα IDs ή το περιεχόμενο του report.
{% endhint %}

## 6. Αποτελέσματα του παρασχεθέντος τεκμηρίου

Η εξέταση του ανεβασμένου πλήρους JSON (53 φύλλα / 2.373 κελιά) κατέγραψε τις εξής **254** παρατηρήσεις:

| Προτεινόμενο είδος | Πλήθος | Απαιτούμενος έλεγχος |
| --- | ---: | --- |
| `InheritedByColumnNine` | 86 | Σχέση με προηγούμενους χαρακτηρισμούς και τη στήλη 9 |
| `PreviousFiscalYearTransfer` | 43 | Υπολογισμός προηγούμενης φορολογικής χρήσης |
| `NextFiscalYearTransfer` | 43 | Υπολογισμός επόμενης φορολογικής χρήσης |
| `NoE3Update` | 59 | «Δεν ενημερώνει E3» ή «Δεν γίνεται χαρακτηρισμός E3» |
| `AssociatedDocumentDependency` | 12 | Κανόνες από τύπο/συσχετιζόμενο παραστατικό |
| `DocumentHeader` | 7 | Όρια πίνακα/επικεφαλίδες, όχι φορολογικοί κανόνες |
| `OptionalBlank` | 2 | Πεδίο «ή κενό», απαιτείται contract review |
| `PublicSectorSpecialCase` | 1 | Ειδική αναφορά Δημοσίου, ενδεχόμενη B2G διάσταση |
| `AccountingBookException` | 1 | «Δεν ενημερώνει το Αναλυτικό Βιβλίο ΑΑΔΕ» |
| **Σύνολο** | **254** | **Όλες εκκρεμείς** |

Για την παρούσα αναγνώσιμη έκδοση όλες οι 254 φράσεις κατατάσσονται στις προτεινόμενες ομάδες. Αυτό **δεν** αποδεικνύει ότι η ταξινόμηση είναι η ορθή λογιστική/νομική ερμηνεία. Σε νέα άγνωστη φράση χρησιμοποιείται `Unclassified` (critical priority), όχι αυθαίρετη έγκριση.

## 7. Μορφή αναφοράς και πεδία

| Πεδίο | Ερμηνεία |
| --- | --- |
| `OriginalXlsxSha256` | Σύνδεση report με συγκεκριμένα bytes πρωτότυπου XLSX |
| `SheetCount` / `OriginalCellCount` | Πραγματικά συνολικά πλήθη φύλλων/κελιών |
| `CandidatePairCount` | Μη εγκεκριμένα υποψήφια ζεύγη (1.403 για αυτή την έκδοση) |
| `PendingHumanReviewCount` | Πλήθος εκκρεμών items |
| `UnclassifiedCount` | Μη αναγνωρισμένες περιγραφές, **ποτέ αυτόματη έγκριση** |
| `Items[].EvidenceId` | Σταθερό αποτύπωμα `SHA256:XLSX + sheet + direction + cell + exact text` |
| `Items[].Sheet` / `Address` | Ακριβής προέλευση κελιού στο Excel |
| `Items[].ExactText` / `OriginalReason` | Αυτούσιο περιεχόμενο και αρχική διάγνωση |
| `Items[].SuggestedKind` / `Priority` | Μη δεσμευτική αυτόματη κατηγορία και σειρά προτεραιότητας |
| `Items[].RequiredHumanCheck` | Ερώτημα ελέγχου που πρέπει να εξεταστεί |
| `Items[].NearbyCells` | Κελιά γύρω από το σημείο (ίδια πλευρά εσόδων/εξόδων), **χωρίς συμπεράσματα για επιτρεπτούς συνδυασμούς** |
| `Items[].IsHumanReviewed` | Πάντα `false`, δεν επιτρέπεται εσωτερικό status update |
| `IsFiscalRuleApproved` / `CanIssueFiscalDocument` | Πάντα `false` |

Τα enums σειριοποιούνται σε ακέραιους στο JSON. Για ερμηνεία χρησιμοποιούνται οι ονομασίες από το CLI και τα παραπάνω πεδία. Το report είναι **input για μελλοντική ανεξάρτητη έγκριση**, όχι έγκριση.

## 8. Πώς γίνεται ο ανθρώπινος έλεγχος

1. Ο υπεύθυνος τεχνικός ανοίγει το επίσημο XLSX **με το ίδιο SHA-256** και πλοηγείται στην ακριβή διεύθυνση `Sheet!Address`.
2. Εξετάζει τα `NearbyCells`, καθώς και συγχωνεύσεις, χρώματα, labels και όρια ενότητας **στο πρωτότυπο Excel**.
3. Για εξαρτώμενα παραστατικά 1.6, 2.4, 5.1 ελέγχει πραγματικό συσχετιζόμενο τύπο και την εφαρμοστέα κατηγορία. **Δεν αντιγράφει αυτόματα** όλους τους συνδυασμούς.
4. Για προηγούμενη/επόμενη χρήση διασταυρώνει λογιστικό γεγονός, ημερομηνία, τρόπο μεταφοράς και συμβατότητα myDATA.
5. Για `NoE3Update`, `OptionalBlank`, `PublicSectorSpecialCase` ελέγχει ειδικά τις προϋποθέσεις φορολογικής διαβίβασης και το συμβόλαιο IMPACT.
6. Ο φοροτεχνικός τεκμηριώνει προτεινόμενη απόφαση **εκτός του τρέχοντος συστήματος**. Η δυνατότητα καταχώρησης/έγκρισης εντός ERP απαιτεί ανεξάρτητο PR, δύο ρόλους, SQL audit και σαφή διάκριση source review από tax approval.

{% hint style="danger" %}
Μην επεξεργαστείς το JSON ώστε να γίνουν true οι σταθερές ασφαλείας ή να τροποποιηθούν τα τεκμήρια. Το παρόν στάδιο **δεν υλοποιεί workflow ανθρώπινης τελικής έγκρισης**.
{% endhint %}

## 9. Logging, σφάλματα και ακύρωση

| Ένδειξη | Τι ελέγχουμε |
| --- | --- |
| `fiscal.xlsx.reconciliation.mismatch` | Το πρωτότυπο/JSON άλλαξε ή δεν προέρχονται από την ίδια εξαγωγή |
| `fiscal.semantics.evidence.invalid` | Ατελή στοιχεία XLSX ή αναφορά σε ανύπαρκτο πραγματικό κελί |
| `fiscal.semantics.evidence.duplicate` | Διπλή παρατήρηση ίδιου κελιού και κατεύθυνσης |
| `fiscal.semantics.evidence.changed` | Το XLSX άλλαξε ανάμεσα στον ανεξάρτητο έλεγχο και στην εξαγωγή |
| `fiscal.semantics.report.path.invalid` | Μη έγκυρη διαδρομή νέου JSON |
| I/O error | Λάθος paths, ανεπαρκή δικαιώματα ή report που υπάρχει ήδη |

Κωδικοί CLI: `0` επιτυχής **triage**, `1` σφάλμα πρόσβασης/εξαγωγής, `2` μη έγκυρα ή ασύμφωνα τεκμήρια, `130` ακύρωση με `Ctrl+C`.

Οι δομημένες καταγραφές περιέχουν μόνο πλήθη και κωδικούς απόρριψης. Δεν αποθηκεύονται τα πρωτότυπα κείμενα παρατηρήσεων σε logs ή SQL.

## 10. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται:** για τεχνική προετοιμασία ανεξάρτητου φοροτεχνικού review, ανάδειξη ειδικών οδηγιών και αρχειοθέτηση στοιχείων provenance.

**Δεν χρησιμοποιείται:** ως production rule engine, λογιστική απόφαση, ενεργοποίηση reference mappings, ΦΠΑ, συμβατότητα IMPACT/B2G ή έγκριση έκδοσης/διαβίβασης.

## 11. Backup, rollback, deployment και αναβαθμίσεις

- Κράτησε αρχικό XLSX, πλήρες JSON και report σε προστατευμένο φάκελο εκτός source repository με καταγεγραμμένο SHA-256.
- Στο PR δεν προστίθεται migration: **παραμένουν 5 εφαρμοσμένες / 0 εκκρεμείς**, εφόσον αυτό έχει επαληθευτεί στο Development.
- Rollback με Git revert, χωρίς SQL αλλαγές.
- Στην παραγωγή το CLI είναι απενεργοποιημένο. Η τυχόν μελλοντική ενεργοποίηση UI/SQL role approvals είναι **διακριτή αλλαγή**.
- Κάθε νέα έκδοση αρχείου ΑΑΔΕ απαιτεί νέο XLSX, νέο JSON, νέα ανεξάρτητη συμφωνία και νέο triage.

## 12. Acceptance checklist

- [ ] Πρωτότυπο ΑΑΔΕ και JSON έχουν προηγουμένως τεχνικά συμφωνήσει.
- [ ] Κάθε εκκρεμότητα έχει σταθερό ID, πραγματικό sheet/cell, αυτούσιο κείμενο και context.
- [ ] Όλα τα ειδικά παραστατικά, μεταφορές χρήσης και μη ενημέρωση E3 παραμένουν pending.
- [ ] Καμία μη αναγνωρισμένη φράση δεν εγκρίνεται αυτόματα.
- [ ] Όλες οι σημαίες approval/issuance είναι false.
- [ ] Ασφαλής εξαγωγή νέου JSON χωρίς overwrite.
- [ ] Windows + Ubuntu build, format, unit + SQL Server CI tests πράσινα.
- [ ] Χωρίς SQL, network provider calls ή νέες migrations.

## 13. Επόμενο στάδιο

**9.3C.3B.5 — Independent fiscal reviewer decisions & provider contracts:** τεκμηριωμένο sign-off κάθε ειδικού κανόνα, έλεγχοι για χρήση/συσχετίσεις παραστατικών, business context, VAT/exemptions, επίσημα XSD και IMPACT request payloads. Το source review από μόνο του **δεν** εγκρίνει φορολογικό mapping και το ERP δεν πρέπει να εκδίδει βάσει του review dossier.
