# Aenaon ERP — Βήμα 9.3C.2: Fiscal Import Staging & Independent Review

## 1. Σκοπός

Αυτό το βήμα προσθέτει **απομονωμένη, μη φορολογικά ενεργή αποθήκευση** υποψήφιων καταλόγων αναφοράς ΑΑΔΕ/IMPACT, δύο διαφορετικά άτομα για υποβολή και έλεγχο τεκμηρίου, καθώς και διατηρήσιμο ιστορικό μίας τελικής απόφασης. Δεν γίνεται import στο παραγωγικό `FiscalReferenceCodes`.

{% hint style="danger" %}
Το `SourceReviewed` **δεν σημαίνει ότι οι κωδικοί ή οι συνδυασμοί είναι φορολογικά εγκεκριμένοι**. Δεν υπολογίζεται ΦΠΑ, δεν ενεργοποιείται φορολογικό mapping, δεν δημιουργείται παραστατικό και δεν αποστέλλεται τίποτα στην ΑΑΔΕ ή στην IMPACT.
{% endhint %}

## 2. Πηγές — 10/11/2026

- [ΑΑΔΕ: εκδόσεις myDATA](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) — v2.0.2, επίσημα XSD, τεκμηρίωση ERP και παρόχων, ξεχωριστό XLS συνδυασμών χαρακτηρισμών.
- [IMPACT VAT/TAX](https://developers.s1ecos.com/vat-tax-codes-869826m0) — ΦΠΑ και αιτίες απαλλαγής.
- [IMPACT Classification](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) — κατηγορίες/τύποι χαρακτηρισμών.
- [IMPACT Units](https://developers.s1ecos.com/measurement-unit-codes-iapr-en16931-869829m0) — myDATA/EN16931 μονάδες.
- [IMPACT Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) — όρια ανά είδος εγγράφου, ειδικά B2G.

Τα τεχνικά λεξικά, οι ημερομηνίες και η φορολογική νομοθεσία πρέπει να επανελέγχονται πριν από ενεργοποίηση. Η ύπαρξη κωδικού στο staging δεν τεκμηριώνει επιτρεπόμενο συνδυασμό.

## 3. Πώς λειτουργεί

1. **Επιθεώρηση τεκμηρίου (9.3C.1):** `FiscalImportInspectionService` ελέγχει manifest JSON, τοπικό πρωτότυπο αρχείο, SHA-256, επιτρεπτό HTTPS domain, namespace, έκδοση και περιόδους.
2. **Υποβολή:** `FiscalStagingService.StageAsync` απαιτεί επιτρεπόμενο χρήστη από `IFiscalStagingAuthorizationPolicy`. Αποθηκεύει batch και γραμμές, με `ContentSha256` του ακριβούς κανονικοποιημένου περιεχομένου.
3. **Ανεξάρτητη επιθεώρηση:** `ReviewAsync` απαιτεί άλλο εξουσιοδοτημένο χρήστη, αιτιολογία και έγκυρο UTC χρόνο. Ελέγχει ξανά το SHA-256 των αποθηκευμένων γραμμών.
4. **Τελική απόφαση:** `SourceReviewed` ή `Rejected`. Η ίδια συναλλαγή αποθηκεύει την απόφαση και μία audit εγγραφή `FiscalImportReviews`.
5. **Αποκλεισμός ενεργοποίησης:** το source review δεν εισάγει εγγραφές στο `FiscalReferenceCodes` ούτε εγκρίνει λογιστικούς χαρακτηρισμούς.

## 4. Νέοι πίνακες

| Πίνακας | Σκοπός |
| --- | --- |
| `dbo.FiscalImportBatches` | Υποβολέας, metadata προέλευσης, hashes, κατάσταση, reviewer, rowversion για προστασία concurrency. |
| `dbo.FiscalImportBatchRows` | Ακριβώς οι υποψήφιες γραμμές ανά batch, μοναδικό `BatchId, Code, EffectiveFrom`. |
| `dbo.FiscalImportReviews` | Μία τελική καταγραφή ανά batch, με reviewer, timestamp, αιτιολογία και content hash. |

Η migration `20261110170000_FiscalImportStagingReview` είναι η **πέμπτη** EF Core migration. Οι υπάρχοντες πίνακες παραμένουν ανέπαφοι. Δεν γίνεται seed πραγματικών κωδικών.

{% hint style="warning" %}
Η SQL αποτρέπει παραβίαση FK, αυτοέγκριση, μη έγκυρες καταστάσεις και διπλές καταχωρίσεις review. Δεν αποτελεί **κρυπτογραφικά υπογεγραμμένο ή αμετάβλητο audit απέναντι σε DBA με πλήρη δικαιώματα**. Η αποθήκευση του πρωτοτύπου τεκμηρίου και ξεχωριστή έγκριση fiscal rules παραμένουν εκτός αυτού του βήματος.
{% endhint %}

## 5. Πρόσβαση και ασφάλεια

Η default πολιτική `DenyAllFiscalStagingAuthorizationPolicy` απορρίπτει **όλες** τις αιτήσεις stage/review. Η υπηρεσία είναι εσωτερική: δεν υπάρχει HTTP endpoint, Back Office GUI ή CLI που να κάνει SQL writes. Δεν αρκεί να είναι κάποιος `Tenant Owner` για να εγκρίνει global fiscal source data. Η ενεργοποίηση πραγματικού platform-level RBAC θα απαιτήσει ξεχωριστό PR, Identity authorization tests και audit review.

Τα logs καταγράφουν μόνο IDs υποβολής, τύπο κωδικού, πλήθος γραμμών και απόφαση, όχι credentials ή πρωτότυπα φορολογικά δεδομένα. Δεν συνδέονται με IMPACT credentials ή myDATA channels.

## 6. Προαπαιτούμενα

- Συνδεδεμένο repository, `global.json` .NET SDK και SQL Server 2022 / compatibility level 160.
- Υπάρχουσες 4 migrations εφαρμοσμένες στο `AenaonERP_Dev`.
- Ολοκληρωμένο CI Windows/Ubuntu/SQL, πλήρες **backup** της development βάσης.
- Επιβεβαιωμένα αρχεία manifest + evidence σύμφωνα με `docs/22-fiscal-import-evidence-inspection.md`.

## 7. Εγκατάσταση και δοκιμές

Από PowerShell στη ρίζα του repository:

```powershell
git fetch origin
git switch --track origin/feature/9-3c-2-fiscal-staging-review
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Η προεπισκόπηση θα εμφανίζει **4 εφαρμοσμένες και 1 εκκρεμή migration** εφόσον η development βάση είναι ενημερωμένη μέχρι το 9.3B. Δεν χρειάζεται να τρέξουν τα production/debug SQL scripts.

{% hint style="warning" %}
**Μην εφαρμόσεις τη migration μέχρι να ολοκληρωθούν οι έλεγχοι PR και να υπάρχει έγκυρο backup.** Μετά, μόνο στην επιβεβαιωμένη Development βάση:
```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```
{% endhint %}

Μετά την εφαρμογή:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Αναμένεται **5 εφαρμοσμένες / 0 εκκρεμείς**. Οι νέοι πίνακες είναι κενοί. Το κανονικό Development UI και το CLI 9.3C.1 συνεχίζουν να λειτουργούν χωρίς αλλαγές.

## 8. Συνήθη σφάλματα

| Σφάλμα | Αντιμετώπιση |
| --- | --- |
| `fiscal.staging.access.denied` | Η πολιτική είναι κλειστή εξ ορισμού. Δεν παρακάμπτουμε το RBAC. |
| `fiscal.staging.submission.invalid` | Απαιτείται εγκεκριμένη επιθεώρηση εισόδου, έγκυρος χρήστης και UTC χρόνος. |
| `fiscal.staging.not_found` | Δεν υπάρχει batch με το συγκεκριμένο ID. |
| `fiscal.staging.integrity.invalid` | Διαφορά μεταξύ stored `ContentSha256` και γραμμών, απόρριψη review. |
| `fiscal.staging.review.invalid` | Αυτοέγκριση, κενή αιτιολογία ή άκυρος χρόνος. |
| `fiscal.staging.review.completed` | Έχει ήδη ληφθεί τελική απόφαση. |
| SQL 547 | Παραβίαση FK ή CHECK (π.χ. reviewer = submitter). |
| EF concurrency exception | Άλλος reviewer άλλαξε ήδη το rowversion. Ανακτούμε την εγγραφή χωρίς αυτόματο retry. |

## 9. Rollback, maintenance και έλεγχοι

**Πριν** τη migration: Git revert/κλείσιμο PR. **Μετά**: επαναφορά δοκιμασμένου backup, όχι αυθαίρετο `Down` καθώς διαγράφει τα 3 staging tables.

Οι SQL integration tests εκτελούνται μόνο σε ξεχωριστή `AenaonERP_CI` βάση και αποδεικνύουν τη δημιουργία των πινάκων, την εγγραφή submission και audit, τους κανόνες ελέγχου και τη μη τροποποίηση `FiscalReferenceCodes`.

## 10. Checklist

- [ ] Διασταύρωση προδιαγραφών ΑΑΔΕ myDATA και IMPACT.
- [ ] Χωρίς seed, φορολογικούς υπολογισμούς ή αυτόματη απόφαση ΦΠΑ.
- [ ] Απομονωμένο batch/rows/review με μοναδικούς δείκτες, FK και rowversion.
- [ ] Διαφορετικός submitter/reviewer, σαφής αιτιολογία, μια απόφαση ανά batch.
- [ ] Default-deny platform-level authorization.
- [ ] Δεν υπάρχουν HTTP/CLI actions για μεταβολή fiscal data.
- [ ] Windows/Ubuntu format/build/test και SQL Server CI πράσινα.
- [ ] Backup και dry-run πριν την τοπική εφαρμογή.

## 11. Επόμενο στάδιο

**9.3C.3:** verified rule matrix από επίσημο XLS «Συνδυασμοί χαρακτηρισμών», λογιστική έγκριση ανά πραγματικό document type/context, content provenance, ασφαλής προώθηση στο global reference registry και contract tests του IMPACT. Ακόμη και τότε η σύνδεση με έκδοση παραστατικών θα απαιτεί ειδική επιβεβαίωση.
