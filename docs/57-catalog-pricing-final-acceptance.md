# AENAON ERP — 9.5C.5: Τελικός έλεγχος αποδοχής Catalog & Pricing

**Κατάσταση (16/11/2026): IMPLEMENTED / PR #60 CI PASSED / REAL HUMAN-STYLE TESTING NOT VERIFIED / NOT FULLY ACCEPTED.**

**Τρέχουσα επαλήθευση (16/11/2026):** Το [PR #60](https://github.com/MoonHard13/AENAON-ERP/pull/60) έχει **συγχωνευθεί** στο `main` commit `3af56b94d0758f59386f6437be5330ff681b0b53` και το [main CI #43836751173](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43836751173) είναι επιτυχές σε Ubuntu/Windows, μαζί με isolated SQL. Το ακόλουθο checklist **συντάχθηκε ιστορικά** επάνω στο προ-merge `main` `8668f4732966f8a5728b68ec1f56a0a882134a7b` και την επιθεώρηση του PR #60, οπότε οι σχετικές αναφορές σε «baseline» διαβάζονται ως τότε, όχι ως τωρινή κατάσταση. Η ολοκληρωμένη αποδοχή εξακολουθεί να απαιτεί πραγματικό browser και SQL evidence. [Σημερινός συγκεντρωτικός επανέλεγχος και οδηγίες](59-acceptance-evidence-and-test-protocol.md).

## Ρητή αποδοχή από ιδιοκτήτη — 16/11/2026

**Απόφαση ιδιοκτήτη (16/11/2026):** Ο ιδιοκτήτης δήλωσε ρητά **«accept 9.5C.5»**. Καταγράφεται ως **OWNER ACCEPTED — WITH UNVERIFIED E2E/SQL GATES**, όχι ως δήλωση ότι εκτελέστηκαν οι ελλείπουσες δοκιμές. Τα Browser/Development SQL/Owner–Member/Mobile/Keyboard/Zoom/Concurrency gates παραμένουν **NOT VERIFIED**· η πλήρης τεχνική αποδοχή και η υλοποίηση 9.6A.1 εξακολουθούν να απαιτούν τεκμήρια και ξεχωριστή ρητή έγκριση. Καμία εξουσιοδότηση για merge, migration ή Menu implementation δεν δόθηκε.

## Σκοπός και όρια

Τεκμηριωμένος έλεγχος του Development Catalog & Pricing πριν από οποιαδήποτε εργασία στο Μενού/POS. Περιλαμβάνονται κατηγορίες, προϊόντα, υπηρεσίες, εμπορικά πεδία, τιμοκατάλογοι, scopes, προεπισκόπηση, ιστορικό, permissions, SQL και responsive UI.

Δεν περιλαμβάνονται έκδοση παραστατικών, φορολογικοί υπολογισμοί, customer groups, Excel/bulk, Android, POS και παραγωγική εγκατάσταση. Αυτά **δεν ενεργοποιούνται** από το παρόν βήμα.

{% hint style="warning" %}
**Μην θεωρήσετε την επιτυχία CI ισοδύναμη με user acceptance.** Η πραγματική εφαρμογή Development, οι αληθινές συνεδρίες Owner/Member, η τοπική SQL βάση και οι πραγματικές οθόνες/θέματα δεν ήταν διαθέσιμα από το περιβάλλον του παρόντος ελέγχου. Δεν δημιουργήθηκαν πραγματικά browser screenshots. Η δοκιμή αυτών των περιπτώσεων παραμένει NOT VERIFIED.
{% endhint %}

## Τι ελέγχθηκε πράγματι

1. Επιθεώρηση πραγματικών αρχείων στο GitHub `main`: Domain, Application services, SQL/EF stores/constraints, Blazor, CLI, CI, tests και documentation.
2. Επιβεβαίωση merged PR #59, αυθεντικού main commit και GitHub Actions jobs (Windows/Ubuntu).
3. Ανάγνωση πραγματικών CI logs: **Windows: Domain 108/108, Application 137/137, Infrastructure 42/42, API 224/224. Ubuntu: ίδιοι αριθμοί και SQL Server 2022 isolated integration 1/1.** Το μοναδικό SQL test εκτελεί μεγάλο σύνολο εσωτερικών SQL assertions.
4. Εντοπίστηκε η χρήση `OrderBy(x => x.Code)` στην κεντρική λίστα τιμοκαταλόγων. Η απλή αλφαβητική σειρά δεν ικανοποιεί αριθμητικούς κωδικούς `1, 2, 10`.
5. Στο branch 9.5C.5 προετοιμάστηκε natural SQL sorting πριν από `Skip/Take`, με regression assertion για κωδικούς `1,2,10`, δύο σελίδες και tenant isolation. **Το CI του PR #60 εκτέλεσε επιτυχώς τη νέα SQL δοκιμή** ([Ubuntu job](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43825052940)).
6. Διορθώθηκαν τα παρωχημένα «feature branch» / «not merged» στοιχεία για το 9.5C.4 σε README, roadmap, requirements και Manual 56.

## Matrix αποδοχής

**Ερμηνεία:** PASS (source/CI) σημαίνει ότι η συγκεκριμένη συμπεριφορά βρέθηκε στον ελεγχόμενο κώδικα και υπάρχει σχετική αυτοματοποιημένη κάλυψη στο baseline, **όχι** ότι πραγματοποιήθηκε E2E δοκιμή πραγματικού χρήστη. NOT VERIFIED απαιτεί ανεξάρτητη εκτέλεση. Η τελευταία στήλη αφορά πάντοτε **πραγματικό browser + Development SQL**.

| Περιοχή / κριτήριο | Κώδικας / baseline CI | Πραγματική δοκιμή |
| --- | --- | --- |
| Προϊόντα/υπηρεσίες: create, edit, deactivate, read | PASS (source/CI) | NOT VERIFIED |
| Επικύρωση πεδίων και διπλότυποι κωδικοί / barcode | PASS (source/CI) | NOT VERIFIED |
| Είδη: αριθμητικό sorting, search, filters, paging | PASS (source/CI) | NOT VERIFIED |
| Είδη: αποθήκευση/refresh/επαναφορά | PASS (persistence tests σε isolated SQL) | NOT VERIFIED |
| Είδη: κατηγορίες, εμπορικοί τύποι, UoM | PASS (source/CI) | NOT VERIFIED |
| Ενεργές/ανενεργές εγγραφές και αναφορές | PASS (source/CI) | NOT VERIFIED |
| Κατηγορίες: create, edit, deactivate, hierarchy | PASS (source/CI) | NOT VERIFIED |
| Κατηγορίες: search, sorting, in-use restrictions | PASS (source/CI) | NOT VERIFIED |
| Τιμοκατάλογοι: create, edit, activate/deactivate | PASS (source/CI) | NOT VERIFIED |
| Τιμοκατάλογοι: αλλαγή/αφαίρεση τιμής και audit | PASS (source/CI) | NOT VERIFIED |
| Ακρίβεια 4 δεκαδικών, αρνητικές τιμές, EUR | PASS (source/CI) | NOT VERIFIED |
| VAT-inclusive / exclusive, Scope Store/Channel | PASS (source/CI) | NOT VERIFIED |
| Ημερομηνίες ισχύος, αρχικά/τελικά όρια | PASS (source/CI) | NOT VERIFIED |
| Φυσική σειρά κωδικών **στη λίστα τιμοκαταλόγων** | FAIL στο baseline· PASS στο PR #60 isolated SQL regression | NOT VERIFIED |
| Sort ειδών μέσα σε τιμοκατάλογο / SQL paging | PASS (source/CI) | NOT VERIFIED |
| Precedence Store+Channel → Store+All → All+Channel → All+All | PASS (source/CI) | NOT VERIFIED |
| Ενεργά/ανενεργά, missing price και ambiguity fail-closed | PASS (source/CI) | NOT VERIFIED |
| Προληπτικός έλεγχος επικαλύψεων ισόβαθμων τιμών | PASS (source/CI) | NOT VERIFIED |
| Audit, stale edits, serialized writes / atomicity | PASS (source/CI) | NOT VERIFIED |
| Snapshot v1 immutable, provenance, resolved-only | PASS (source/CI) | NOT VERIFIED |
| Tenant isolation, cross-tenant FK, Owner/Member | PASS (source/CI) | NOT VERIFIED |
| Server-side validation, parameterization, errors | PASS (source review/CI) | NOT VERIFIED |
| Duplicate submissions / retries | PASS (catalog idempotency source/tests); πλήρης επανέλεγχος εκκρεμεί | NOT VERIFIED |
| Blazor responsiveness 320/375/390/768/1366/1920 | NOT VERIFIED (fixtures δεν αρκούν) | NOT VERIFIED |
| Light/Dark, keyboard/focus/zoom, error recovery | NOT VERIFIED | NOT VERIFIED |
| Πραγματικά screenshots από λειτουργικό GUI | NOT VERIFIED | NOT VERIFIED |
| Runtime απόδοση / load testing πολλών tenants | NOT VERIFIED | NOT VERIFIED |
| GitBook δημοσίευση και οπτική επιβεβαίωση | NOT VERIFIED | NOT VERIFIED |

### Σημαντικές διευκρινίσεις

- `PriceResolutionPolicy.Select` απορρίπτει ισοβαθμία ως `Ambiguous` ακόμα και αν τα ποσά είναι ίδια. Η προτεραιότητα διατηρείται ως `store-channel-v1`.
- `PriceResolutionService.PreviewSnapshotAsync` δημιουργεί in-memory `CommercialPriceSnapshot` μόνο όταν το αποτέλεσμα είναι `Resolved`. Δεν υποκαθιστά ατομικό snapshot γραμμής παραγγελίας.
- `SqlPriceListManagementStore` εφαρμόζει serialized transactions, optimistic expected-value checks, overlap guard και audit στην ίδια συναλλαγή.
- Τα EF configurations έχουν `TenantId`-scoped unique constraints και σύνθετα FK για price list / catalog item / store. Η ύπαρξή τους δεν αποδεικνύει ότι το τοπικό Development schema είναι συγχρονισμένο.
- Η κεντρική λίστα τιμοκαταλόγων δεν προσφέρει UI sorting ανά κάθε στήλη. Η αλλαγή αυτού του PR αφορά αποκλειστικά την ορθή **προεπιλεγμένη αριθμητική σειρά κωδικού**, όχι νέα UI interactions.

## Απομονωμένη επαλήθευση για το PR 9.5C.5

Οι παρακάτω εντολές είναι **οδηγίες εκτέλεσης**, όχι ισχυρισμός ότι εκτελέστηκαν τοπικά:

```powershell
git fetch origin
git switch feature/step-9-5c-5-catalog-pricing-final-audit
dotnet --info
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet publish src/Aenaon.Api --configuration Release --no-restore --self-contained false --output artifacts/api
```

Ο SQL Server integration έλεγχος εκτελείται αποκλειστικά με το υπάρχον απομονωμένο CI Docker container: στο [workflow](../.github/workflows/ci.yml) ενεργοποιούνται `AENAON_RUN_SQL_INTEGRATION=1` και `AENAON_TEST_SQL_CONNECTION`, και χρησιμοποιείται fresh `AenaonERP_CI`. **Μην μεταφέρετε τις μεταβλητές του CI σε `AenaonERP_Dev` ή production.** Δεν υπάρχει νέα EF migration στο 9.5C.5.

## Human-style acceptance — υποχρεωτικές δοκιμές

Χρησιμοποιήστε μόνο εξουσιοδοτημένα, απομονωμένα δεδομένα Development. Δημιουργήστε δύο δοκιμαστικές επιχειρήσεις και λογαριασμούς Owner/Member. Μην χρησιμοποιήσετε πραγματικά παραστατικά. Καταγράψτε για κάθε δοκιμή αποτέλεσμα, SQL evidence, ημερομηνία, browser, viewport και πραγματικό screenshot σε FAIL.

1. **Κατάλογος:** δημιουργήστε γονική/θυγατρική κατηγορία, προϊόν, υπηρεσία. Ελέγξτε κωδικούς `1`, `2`, `10`, αναζήτηση, ταξινόμηση όλων των διαθέσιμων στηλών, υποχρεωτικά πεδία, barcode, UoM, accounting type και validation.
2. **Ακεραιότητα:** επιχειρήστε διπλό κωδικό, μη έγκυρη κατηγορία, αλλαγή σε ανενεργή κατηγορία και απενεργοποίηση κατηγορίας σε χρήση. Επαναφορτώστε τις σελίδες και επιβεβαιώστε τις εγγραφές στην SQL.
3. **Τιμοκατάλογοι:** δημιουργήστε τιμοκαταλόγους `1`, `2`, `10`, επιβεβαιώστε τη φυσική σειρά και τη σωστή σελιδοποίηση. Δημιουργήστε γενικούς και store/channel-specific τιμοκαταλόγους.
4. **Τιμές:** εισαγάγετε `0`, `12.3456`, αρνητική και τιμή με 5 δεκαδικά. Επιβεβαιώστε μόνο τις επιτρεπτές αποθηκεύσεις, μετά από refresh. Επεξεργαστείτε/αφαιρέστε τιμές, ελέγξτε την εγγραφή ιστορικού και τις αλλαγές πολιτικής ΦΠΑ.
5. **Επίλυση:** ελέγξτε τις τέσσερις προτεραιότητες με ίδιο είδος και ημερομηνία. Δοκιμάστε λήξη ημερομηνίας, inactive list, missing price, overlapping scope, unauthorized store και `Ambiguous`. Καμία λανθασμένη τιμή δεν πρέπει να επιλεγεί.
6. **Συγχρονισμός:** σε δύο ανεξάρτητες συνεδρίες ανοίξτε την ίδια τιμή και αλλάξτε τη διαδοχικά. Η παλιότερη φόρμα πρέπει να λάβει conflict, όχι να παρακάμψει το νεότερο ποσό. Ελέγξτε διπλό submit.
7. **Δικαιώματα:** ο Owner γράφει, ο Member διαβάζει χωρίς δυνατότητα μεταβολής. Δοκιμάστε απευθείας URLs, POSTs, άλλον Tenant και ξένο Store με ενεργό/άκυρο session.
8. **Οπτική αποδοχή:** επαναλάβετε τις κρίσιμες ροές σε 320, 375, 390, 768, 1366 και 1920 px, Light/Dark, portrait/landscape, 200% zoom και keyboard-only (Tab/Shift+Tab/Enter/Escape). Ελέγξτε sidebar, πίνακες, κάρτες, φόρμες, dropdowns, focus, διαλόγους και μηνύματα σφάλματος.
9. **Σύγκριση SQL:** διασταυρώστε τις αποθηκευμένες τιμές και audit records εντός του σωστού Tenant πριν/μετά τις ενέργειες. Επιβεβαιώστε ρητά τα όρια Store και την απουσία ανεπιθύμητων εγγραφών.

### Φύλλο τεκμηρίων

| Test ID | Browser / viewport / theme | User/Tenant/Store | Αρχική κατάσταση | Ενέργεια | Αναμενόμενο | Πραγματικό | PASS / FAIL | Screenshot / SQL |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| E2E-01 | NOT VERIFIED | — | — | — | — | — | NOT VERIFIED | — |
| E2E-02 | NOT VERIFIED | — | — | — | — | — | NOT VERIFIED | — |

## Fiscal compatibility gate (16/11/2026)

- [Επίσημη σελίδα AADE myDATA — v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata).
- [IMPACT eInvoicing Core API Swagger](https://einvoiceapi.impact.gr/swagger/Index.html): έκδοση `2.8.12-build.b9a733`, MyData Version 202 κατά τον έλεγχο.

Η ένδειξη `PricesIncludeVat`, το εμπορικό ποσό `decimal(18,4)`, το `CatalogAccountingType` και το `CatalogUnitOfMeasure` **δεν** επαρκούν από μόνα τους για υπολογισμό ΦΠΑ, φορολογική κατηγορία, εξαίρεση/απαλλαγή, νόμιμη στρογγυλοποίηση τελικής γραμμής ή myDATA/IMPACT line mapping. Απαιτούνται ανεξάρτητος εγκεκριμένος tax engine, κανόνες στρογγυλοποίησης/εκπτώσεων/τελών, versioned provider contracts, δοκιμές και φορολογική έγκριση. Η έκδοση/διαβίβαση παραμένει απενεργοποιημένη.

## SQL, ασφάλεια, rollback και κριτήρια αποδοχής

**Αλλαγές 9.5C.5:** φυσική σειρά ανάγνωσης SQL στην κεντρική λίστα και SQL regression test, συν τεκμηρίωση. **0 νέες migrations, 0 database writes κατά τη δημιουργία του PR, 0 νέα packages.** Rollback του κώδικα με revert του PR μετά από review, χωρίς schema rollback.

**Απομένουν:** ανεξάρτητη δοκιμή πραγματικού GUI/Development SQL, κανονική αναφορά με screenshots και έγκριση του ιδιοκτήτη. Το CI #43825052940 για τον κώδικα/SQL regression του PR #60 πέρασε σε Ubuntu και Windows· επιπλέον πέρασε το τελικό [main CI #43836751173](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43836751173) μετά το merge. Ακόμη και αν περάσουν όλα τα αυτοματοποιημένα tests, ο χαρακτηρισμός παραμένει **IMPLEMENTED, NOT FULLY ACCEPTED** έως να κλείσουν τα mandatory E2E/security/UI gates.

**Κριτήριο εξόδου:** όλες οι απαιτούμενες δοκιμές PASS με πραγματικά τεκμήρια, καμία ανοιχτή κρίσιμη/σοβαρή δυσλειτουργία, επιβεβαιωμένα migration/permissions/isolation, συμπληρωμένα manuals και ρητή έγκριση. Μόνο τότε επιτρέπεται να προγραμματιστεί το 9.6A — Μενού εστίασης.
