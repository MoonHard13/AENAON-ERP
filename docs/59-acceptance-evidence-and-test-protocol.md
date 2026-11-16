# AENAON ERP — 9.5C.5: Επικαιροποιημένα τεκμήρια και πρωτόκολλο πραγματικής αποδοχής

**Ημερομηνία:** 16 Νοεμβρίου 2026  
**Current main:** 3af56b94d0758f59386f6437be5330ff681b0b53  
**Merged change:** [PR #60](https://github.com/MoonHard13/AENAON-ERP/pull/60) στις 16/11/2026 09:06 UTC.  
**Push main CI:** [run 43836751173](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43836751173), success σε Ubuntu/Windows.  
**Τελική κατάσταση:** IMPLEMENTED — **NOT FULLY ACCEPTED**. Τα πραγματικά Development E2E/UI/SQL gates παραμένουν **NOT VERIFIED**.

{% hint style="warning" %}
Το πράσινο CI και η ύπαρξη unit/integration tests **δεν** αποτελούν ανθρώπινη αποδοχή. Δεν υπήρξε πρόσβαση στον πραγματικό Development browser, στα διαπιστευτήρια Owner/Member ή στην AenaonERP_Dev σε αυτόν τον έλεγχο. Δεν κατασκευάστηκαν screenshots ή SQL evidence και δεν εκτελέστηκε καμία εντολή μεταβολής δεδομένων.
{% endhint %}

## Σκοπός και σχέση με το Manual 57

Η σελίδα συμπληρώνει το [Manual 57](57-catalog-pricing-final-acceptance.md), το οποίο δημιουργήθηκε πριν από το merge του PR #60. Διατηρεί την ιστορική διάκριση αρχικού baseline και PR, αλλά διορθώνει τη **σημερινή** κατάσταση του main. Το [Menu 9.6A.0 Design](58-menu-management-architecture.md) είναι design-only έως να κλείσει η αποδοχή.

## Τι επαληθεύτηκε σήμερα

- GitHub default branch main @ 3af56b94d0758f59386f6437be5330ff681b0b53, ακριβώς merge SHA του PR #60, με 7 changed files: README, roadmap, requirements, Manual 56, Manual 57, SQL price-list read sorting και isolated SQL assertions.
- Το central PriceLists query ταξινομεί numeric-like codes στη SQL **πριν** από Skip/Take, με σταθερό Id tie-breaker και προστασία μεγάλων numeric strings. Το SQL regression ελέγχει 1,2,10, pagination και foreign-tenant filtering.
- Διαβάστηκαν τα code paths CatalogItem/Category, PriceList/PriceListItem, PriceResolutionPolicy, PriceResolutionService, CommercialPriceSnapshot, EF configurations, Blazor/SSR, CLI και CI workflow.
- Τελευταίο main CI: **Ubuntu:** Domain 108/108, Application 137/137, Infrastructure 42/42, API 224/224, επιπλέον isolated SQL Server 2022 test 1/1 που περιέχει επιμέρους assertions, format/build/publish/HTTPS success. **Windows:** Domain 108/108, Application 137/137, Infrastructure 42/42, API 224/224, build/publish success. Το αριθμητικό σύνολο δεν ισοδυναμεί με 1 SQL assertion μόνο.
- Το CI εκτελεί Google Chrome JS smoke scripts με συνθετικό browser fixture και **όχι το πραγματικό Development Back Office με συνδεδεμένο χρήστη και SQL**. Δεν λογίζεται ως πλήρες browser/E2E acceptance.
- Αρχεία GitHub docs/ είναι πρωτογενή τεχνικά manuals. Ο πραγματικός live GitBook space **AENAON ERP Documentation**, ξεχωριστός από τα Git-synced starter templates, είχε στις 16/11/2026 κείμενα που αναφέρονται σε PR #50/#51, χαρακτήριζαν Barcode/LongDescription/Audit μη υλοποιημένα και δεν περιείχαν νέα αντίστοιχα manuals. Δεν τροποποιήθηκε live GitBook ή δεν επιβεβαιώθηκε οπτικά η εμφάνισή του.

## Acceptance matrix — ξεχωριστό evidence ανά τύπο

**PASS (code/CI)** σημαίνει μόνο επιβεβαιωμένο implementation και σχετικό automated check. **NOT VERIFIED (Dev)** σημαίνει ότι δεν υπήρξε πραγματική δοκιμή μετά από authentic login, browser και βάση. **FAIL** χρησιμοποιείται μόνο για αναπαραχθέν ελάττωμα· δεν επινοούνται FAIL σε ανέλεγκτη οθόνη.

| Κριτήριο | Code / CI | Real Dev user / SQL |
| --- | --- | --- |
| Product/Service creation, editing, deactivation και read | PASS | NOT VERIFIED |
| Category creation/edit/deactivation, hierarchy και constraints | PASS | NOT VERIFIED |
| Barcode, LongDescription, commercial fields/validation | PASS | NOT VERIFIED |
| Catalog search/filter, natural code sort και SQL paging | PASS | NOT VERIFIED |
| Price-list create/edit/activate/deactivate/price management | PASS | NOT VERIFIED |
| Price-list code 1, 2, 10 πριν από SQL paging (PR #60) | PASS | NOT VERIFIED |
| Natural sort PriceListItems, column sorting όπου έχει υλοποιηθεί | PASS | NOT VERIFIED |
| Audit, optimistic stale checks, atomic writes | PASS | NOT VERIFIED |
| Tenant/Store references, Owner/Member server authorization | PASS | NOT VERIFIED |
| Price scopes, date boundaries και PriceResolutionPolicy precedence | PASS | NOT VERIFIED |
| Missing/ambiguous/inactive price fail-closed | PASS | NOT VERIFIED |
| CommercialPriceSnapshot v1 resolved-only / immutable in memory | PASS | NOT VERIFIED |
| Διπλό submit/parallel edit με πραγματικό browser και SQL | ΜΕΡΙΚΗ κάλυψη tests | NOT VERIFIED |
| πραγματικό schema/migration status AenaonERP_Dev | source-only schema | NOT VERIFIED |
| 320/375/390/768/1366/1920 πραγματικά viewports | fixture-only smoke | NOT VERIFIED |
| Light/Dark, keyboard, 200% zoom, focus, modal, mobile overflow | source-only | NOT VERIFIED |
| πραγματικά screenshots σε failure και persisted SQL evidence | χωρίς τεκμήρια | NOT VERIFIED |
| φόρτος, ακύρωση, δικτυακή αστοχία, SQL recovery | μη πλήρης κάλυψη | NOT VERIFIED |
| Live GitBook αντιστοίχιση με main και οπτικό QA | FAIL (outdated text τεκμηριώθηκε) | NOT VERIFIED (visual) |

Η τελευταία γραμμή είναι **τεκμηριωμένο documentation defect**, όχι λειτουργικό bug εφαρμογής. Τα μη ελεγμένα UI gates **δεν** μετατρέπονται σε FAIL.

## Υποχρεωτικό πρωτόκολλο για χειριστή — ΜΟΝΟ Development

### Προϋποθέσεις

1. Απομονωμένη Development SQL Server 2022 βάση (π.χ. AenaonERP_Dev), επαληθευμένη σύνδεση, πρόσφατο backup, δύο δοκιμαστικά Tenants, δύο Stores ανά Tenant, Owner και Member test accounts. Κανένα πραγματικό πελατειακό/φορολογικό παραστατικό.
2. Συγχρονισμένο main και .NET SDK της έκδοσης που απαιτεί το global.json (**10.0.404** κατά τον έλεγχο). Δεν απαιτείται migration του PR #60.
3. Ιδιωτικό φύλλο QA με test ID, user/tenant/store, browser, viewport, theme, χρόνο, expected/actual, SQL before/after και screenshot path για FAIL.

### Εντολές επιθεώρησης χωρίς schema/data writes

~~~powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git switch main
git pull --ff-only origin main
git rev-parse HEAD
git status --short
dotnet --info
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
~~~

Αν δεν υπάρχει το απαιτούμενο SDK, σταματήστε και εγκαταστήστε επίσημο supported SDK, **μην τροποποιήσετε global.json για να περάσει τεχνητά το test**. Τα SQL integration tests του CI απαιτούν *disposable* βάση· ποτέ μην τα στρέψετε σε AenaonERP_Dev ή production.

### Πραγματική εκτέλεση χειροκίνητων tests

1. Εκκινήστε με **dotnet run --project src/Aenaon.Api --launch-profile https**, ανοίξτε το πραγματικό https://localhost:7140/login και συνδεθείτε ως Owner Tenant A. Κρατήστε browser DevTools screenshots μόνο από το πραγματικό app.
2. Στον Catalog δημιουργήστε κατηγορία γονέα/παιδί, προϊόν και υπηρεσία, προαιρετικό Barcode και αναλυτική περιγραφή. Δοκιμάστε υποχρεωτικά, διπλό barcode/code, invalid category και inactive restrictions. Refresh και ελέγξτε SQL.
3. Δημιουργήστε test codes 1, 2, 10 και ελέγξτε στη **λίστα τιμοκαταλόγων** την προεπιλεγμένη φυσική σειρά. Προσθέστε >25 εγγραφές για αλλαγή σελίδας και ελέγξτε μη επανάληψη/μη απώλεια στοιχείων. Δοκιμάστε ξεχωριστά sorting ειδών και κατηγοριών· η κεντρική λίστα τιμοκαταλόγων δεν παρέχει sort ανά *κάθε* στήλη.
4. Επεξεργαστείτε price list και item prices (0 και τετραψήφια decimals), activate/deactivate, audit events, stale update από δεύτερο browser και double-submit. Ελέγξτε ότι αποτυχημένο write δεν παράγει audit.
5. Προεπισκόπηση Store+Channel, Store+All, Tenant+Channel, Tenant+All με ενεργά/ανενεργά, επικαλυπτόμενα ισόβαθμα και ημερομηνίες ισχύος. Η Missing/Ambiguous δεν επιτρέπεται να εκδώσει επιλεγμένη τιμή ή snapshot.
6. Επαναλάβετε ως Member, άγνωστος χρήστης, ξένος Tenant B και Store B. Ελέγξτε **και τις POST ενέργειες**, όχι μόνο κρυμμένα UI buttons.
7. Για κάθε προβολή Catalog, PriceLists, PriceListManage, PricePreview, Dashboard, χρησιμοποιήστε widths **320, 375, 390, 768, 1366, 1920**, Light/Dark, portrait/landscape, 200% zoom και keyboard-only Tab/Shift+Tab/Enter/Escape. Ελέγξτε focus, dropdown, dialog, sticky panels, loading/empty/error states, overflow.
8. Δοκιμάστε refresh μετά τη δημιουργία, session expiration, απώλεια DB, cancel/retry (όπου παρέχεται), δύο χρήστες ταυτόχρονα και επιστροφή στο UI χωρίς διπλοεγγραφές.
9. Μετά τα tests, συγκρίνετε επιλεκτικά τον πραγματικό SQL Tenant/Store κύκλο με **read-only** ερωτήματα και καταγράψτε screenshots/διαφορές. Χωρίς ανεξάρτητο SQL evidence, σημειώστε SQL Gate **NOT VERIFIED**.

### Ενδεικτικός read-only SQL έλεγχος (με πραγματικό test TenantId)

~~~sql
USE [AenaonERP_Dev];

SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT Code, Name, IsActive
FROM dbo.CatalogItems
WHERE TenantId = @TenantId
ORDER BY Code;

SELECT Code, Name, IsActive, StoreId, Channel, ValidFrom, ValidUntil
FROM dbo.PriceLists
WHERE TenantId = @TenantId
ORDER BY Code;

SELECT PriceListId, Action, Source, OccurredUtc, CatalogItemId,
       BeforePrice, AfterPrice
FROM dbo.PriceListAuditEntries
WHERE TenantId = @TenantId
ORDER BY OccurredUtc DESC, Id DESC;
~~~

Στο SSMS ορίστε **εσείς** @TenantId ως GUID της απομονωμένης test επιχείρησης πριν από την εκτέλεση (π.χ. DECLARE @TenantId uniqueidentifier = '...'). Η SQL **ORDER BY Code** εδώ είναι απλή διαγνωστική σειρά· η φυσική σειρά 1/2/10 πρέπει να επιβεβαιωθεί μέσω εφαρμογής/EF query, όχι να συναχθεί από αυτή τη διαγνωστική εντολή. Αν λείπει table, μην εφαρμόσετε αυτόματα migration· αναφέρετε blocker και ακολουθήστε ξεχωριστή διαδικασία backup/έγκρισης.

### Evidence record

| ID | Περιβάλλον / browser | User / Tenant / Store | Εκτέλεση | Expected | Actual | Απόφαση | Screenshot / SQL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C5-001 | NOT VERIFIED | — | Catalog CRUD | Valid save, invalid denied | — | NOT VERIFIED | — |
| C5-002 | NOT VERIFIED | — | Price-list 1/2/10 + paging | 1,2,10 | — | NOT VERIFIED | — |
| C5-003 | NOT VERIFIED | — | Pricing + immutable snapshot | Resolved only | — | NOT VERIFIED | — |
| C5-004 | NOT VERIFIED | — | Owner/Member cross-tenant | No leakage | — | NOT VERIFIED | — |
| C5-005 | NOT VERIFIED | — | Concurrency/double submit | Conflict/no duplicates | — | NOT VERIFIED | — |
| C5-006 | NOT VERIFIED | — | Responsive/themes/keyboard | No broken UI | — | NOT VERIFIED | — |

## Blockers και υπολειπόμενα ρίσκα

**B1 — Blocking:** πραγματικός authenticated browser/SQL test σε Dev δεν εκτελέστηκε.  
**B2 — Blocking:** αναλυτικό Owner/Member, cross-tenant, stale edit, double submit, responsive/light/dark/keyboard/zoom evidence λείπει.  
**B3 — Documentation:** Live GitBook content βασίζεται σε PR #50/#51 και χρειάζεται ανεξάρτητη εγκεκριμένη GitBook change request μετά από συγχρονισμό κειμένου.  
**B4 — Risk:** μη ανεξάρτητα επαληθευμένο τοπικό 10 applied / 0 pending· πρόκειται για ιστορική δήλωση χειριστή, όχι τρέχουσα σύνδεση στην DB.  
**B5 — Risk:** καμία μετρημένη απόδοση υπό πολλά concurrent tenant/store queries.  
**B6 — Fiscal:** ανεξάρτητο φορολογικό mapping/approval pending· menu design δεν ενεργοποιεί έκδοση.

**Καμία ανοιχτή επιβεβαιωμένη λειτουργική FAIL στον κώδικα** από τον τρέχοντα έλεγχο. Αυτό δεν αποκλείει μη ελεγμένα λειτουργικά ελαττώματα.

## Κριτήριο εξόδου και προτεινόμενη συνέχεια

- Όλα τα mandatory C5 test IDs πραγματικά **PASS** με γνήσια SQL/browser evidence.
- Καμία σοβαρή/κρίσιμη εκκρεμότητα UI, ασφάλειας, integrity ή ρόλων.
- Ενημερωμένο QA report και ρητή ιδιοκτησιακή αποδοχή Catalog & Pricing.
- Μόνο τότε έγκριση 9.6A.1. Ο σχεδιασμός 9.6A.0 μπορεί να ελεγχθεί τώρα αλλά **δεν επιτρέπει υλοποίηση**.

**Πότε χρησιμοποιείται:** για εκτέλεση/καταγραφή πραγματικών acceptance tests. **Δεν χρησιμοποιείται:** ως δήλωση ότι τα tests εκτελέστηκαν, ως έγκριση migration ή ως production runbook.

{% hint style="info" %}
Βέλτιστη πρακτική: κάθε δοκιμή έχει έναν επώνυμο χειριστή, timestamp, ακριβές main SHA, browser/version, πραγματικό screenshot σε αποτυχία και επαληθεύσιμα δεδομένα Dev. Δεν καταγράφουμε «PASS» χωρίς αποτέλεσμα πραγματικής εκτέλεσης.
{% endhint %}
