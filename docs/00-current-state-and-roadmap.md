# AENAON ERP — Κατάσταση και χάρτης τεκμηρίωσης

**Αποτύπωση:** 16/11/2026 · **Verified main:** `3af56b94d0758f59386f6437be5330ff681b0b53` μετά το merged [PR #60](https://github.com/MoonHard13/AENAON-ERP/pull/60), με επιτυχές [main CI #43836751173](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43836751173) (Ubuntu/Windows και isolated SQL Server 2022). Το 9.5C.5 είναι **OWNER ACCEPTED / USER-ATTESTED E2E+SQL PASS** μετά από επιβεβαίωση του ιδιοκτήτη στις **17/11/2026**· η πραγματική Development εκτέλεση δεν αναπαράχθηκε ανεξάρτητα από τον reviewer και δεν κατατέθηκαν εδώ αναλυτικά artifacts. Οι 10 εφαρμοσμένες / 0 εκκρεμείς migrations της AenaonERP_Dev είχαν αναφερθεί ιστορικά από τον χειριστή, δεν διασταυρώθηκαν ξανά σήμερα.

{% hint style="info" %}
Τα manuals ανά βήμα διατηρούνται **ως ιστορική τεκμηρίωση**. Για την τρέχουσα κατάσταση προτιμήστε το [root README](../README.md), αυτόν τον χάρτη και τον ίδιο τον κώδικα του `main`. Τα παλαιότερα «δεν υπάρχει ακόμη» αφορούν τη συγκεκριμένη έκδοση του βήματος.
{% endhint %}

## Owner sign-off και υπολειπόμενα gates

**Αποδοχή ιδιοκτήτη (16–17/11/2026):** Στις 16/11 ο ιδιοκτήτης δήλωσε **«accept 9.5C.5»** και στις **17/11/2026** δήλωσε ρητά ότι **εκτέλεσε ο ίδιος τα πραγματικά Development E2E/SQL acceptance checks του 9.5C.5 και ήταν όλα εντάξει**. Κατάσταση: **9.5C.5 OWNER ACCEPTED — USER-ATTESTED DEVELOPMENT E2E/SQL PASS**. Η διαπίστωση προέρχεται από τον χειριστή και **δεν έχει επανεκτελεστεί ανεξάρτητα** σε αυτό το περιβάλλον· δεν δόθηκαν εδώ αναλυτικά αποτελέσματα ανά test case, SQL outputs, test logs ή screenshots. Τα ιστορικά `NOT VERIFIED` αφορούν **τον ανεξάρτητο έλεγχο που δεν έγινε εδώ**, όχι άρνηση της επιβεβαίωσης του ιδιοκτήτη. Παραμένει απαραίτητη **ξεχωριστή ρητή έγκριση** για έναρξη 9.6A.1, SQL migration, merge PR #61 ή δημοσίευση GitBook.

## Διαθέσιμες λειτουργίες — Development-only

| Περιοχή | Κατάσταση και εγχειρίδια |
| --- | --- |
| Βασικό API / SQL / ταυτότητα | Tenant/Store, HTTPS Identity sessions, permissions και UI: [01](01-getting-started.md)–[15](15-visual-consistency-polish.md) |
| Fiscal diagnostics | Staging, πηγές ΑΑΔΕ, ανασκόπηση XLSX, offline IMPACT preflight: [18](18-mydata-impact-compatibility.md)–[33](33-impact-conditional-field-evidence.md) |
| Κατάλογος | Domain/SQL [16](16-product-catalog-domain.md)–[17](17-product-catalog-sql.md), προβολή/CRUD/ιεραρχία/sorting [34](34-catalog-read-preview.md)–[41](41-catalog-sorting.md) |
| Εμπορικά πεδία | Μονάδες/λογιστικοί τύποι, dropdowns, compact UI: [42](42-catalog-commercial-profile.md)–[44](44-back-office-layout-refinement.md) |
| Τιμοκατάλογοι | SQL και διαχείριση UI/CLI: [45](45-price-lists-foundation.md)–[46](46-price-list-management.md) |
| Επίλυση τιμής | Read-only service και Blazor preview: [49](49-price-resolution-foundation.md)–[50](50-price-preview-blazor.md) |
| Catalog & Pricing audit | [9.5C.1 — Catalog & Pricing Gap Analysis](51-catalog-pricing-gap-analysis.md): σχεδιασμός και κριτήρια, χωρίς SQL changes |
| Πρόσθετα πεδία καταλόγου | [9.5C.2 — Barcode και αναλυτική περιγραφή](52-catalog-item-details.md): Domain, SQL, Blazor, CLI, tests και 9η migration (Development) | 
| Ιστορικό/ακεραιότητα τιμών | [9.5C.3 — Pricing Integrity & History](53-pricing-integrity-history.md): durable audit, overlap guard, Blazor/CLI, 10η migration (merged code· local schema 10/0 verified by operator) |
| Εμπορικά scopes & snapshot | [9.5C.4 — Commercial Readiness (merged PR #59)](56-pricing-scopes-commercial-snapshot.md): διατήρηση priority, αναβολή customer groups, versioned in-memory snapshot χωρίς SQL migration |
| Final acceptance | [9.5C.5 — Final acceptance](57-catalog-pricing-final-acceptance.md): verified source/CI, SQL sort regression, **owner-accepted πραγματικοί E2E/SQL έλεγχοι (17/11)**, όχι independently reproduced |
| **9.5C.5 evidence** | [Πρωτόκολλο, CI και δήλωση επιτυχημένων πραγματικών Dev checks από ιδιοκτήτη](59-acceptance-evidence-and-test-protocol.md): ανά-case logs/screenshots δεν υποβλήθηκαν εδώ |
| **9.6A.0 Menu Architecture & UX** | [Design-only functional/SQL/Blazor/CLI specification](58-menu-management-architecture.md): owner 9.5C.5 acceptance έχει δηλωθεί· καμία υλοποίηση, migration, POS ή δημοσίευση χωρίς νέα ρητή έγκριση |
| **9.6A.1 Draft Menu Management + Store Default/Auto-Sync (PR #62)** | [Manual 61](61-draft-menu-management.md): Domain/Application/Infrastructure/Blazor/CLI, **αυτόματο default Draft κατά την αρχική δημιουργία καταστήματος, δυναμικό auto-sync νέων ERP κατηγοριών και ειδών, δημιουργία νέου μενού από ενεργές ERP κατηγορίες και είδη**, εισαγωγή σε παλιό κενό μενού, additive SQL migration **owner-applied στη Development AenaonERP_Dev (11/0, 17/11/2026)**, Draft PR **not merged**, CI **PASS**, real human-style browser/SQL acceptance **not yet verified**, **migration #12 OWNER-REPORTED APPLIED to AenaonERP_Dev (12/0, 17/11/2026), subsequent read-only CLI dry-run PASS (12/0); owner-executed default-menu provisioning + SSMS persisted verification PASS (`DEFAULT-MAIN`, 8 categories, 12 items, Revision 1); auto-sync browser E2E NOT VERIFIED** · [Manual 62](62-default-menu-auto-catalog-sync.md) |
| **9.6A.1 UI επεκτάσεις — κατηγορίες/υποκατηγορίες/root items/drag (Draft PR #62)** | Η νέα έκδοση υποστηρίζει parent-child κατηγορίες, προσθήκη υποκατηγορίας, root items, πλακίδια με drag εντός ίδιου επιπέδου και keyboard ↑/↓. **13η migration `20261117170000_MenuBuilderHierarchyAndRootItems` OWNER-APPROVED / OWNER-APPLIED αποκλειστικά στη Development `AenaonERP_Dev` (18/11/2026: apply PASS και ανεξάρτητο read-only dry-run 13/0).** Πριν την εφαρμογή ελέγχθηκαν πραγματικό SSMS preflight (3 menus, 27 categories, 39 items, 6 συνδέσεις parent-child, 0 orphan) και generated SQL. Backup owner-attested, ανεξάρτητο isolated restore NOT VERIFIED. Εκκρεμεί [read-only postmigration review](../scripts/sql/014-menu-builder-postmigration-review.sql) και πραγματικό UI acceptance. [Manual 63](63-menu-builder-tile-redesign.md) · [Manual 64](64-menu-builder-hierarchy-and-drag-drop.md). **Verified code CI #44113980845 PASS** (538 μη-SQL tests ανά OS + 1 απομονωμένο SQL Server 2022 integration test στο Ubuntu), πραγματικό authenticated browser και SQL **Development** acceptance **NOT VERIFIED**, κανένα merge/Prod/POS/Publish. |
| **9.6A.0 Final Design Review** | [Manual 60](60-menu-management-final-design-review.md): **DESIGN APPROVED 10/10** (6 UX/functional + 4 πολιτικές DEC-01..04: permissions, fail-closed pricing, χρεώσιμα modifiers, timezone). Implementation/merge **όχι εγκεκριμένα** |
| Sorting τιμοκαταλόγου | [Διόρθωση ταξινόμησης ειδών](54-price-list-item-sorting.md): φυσική σειρά κωδικών, όλες οι ορατές στήλες, SQL paging, CLI, χωρίς νέα migration |
| Mobile Back Office | [Responsive UX](55-mobile-backoffice-responsive.md): dashboard, κατάλογος, τιμοκατάλογοι, καρτέλες/ιστορικό και native mobile sorting· frontend-only |
| Μελλοντικά | [Περιβαλλοντικά τέλη — design](47-environmental-fees-fiscal-design.md), [Excel/bulk — αναβολή](48-bulk-actions-and-excel-import-plan.md) |

## Database / installation

Ο χειριστής επιβεβαίωσε **10 εφαρμοσμένες / 0 εκκρεμείς** migrations στην `AenaonERP_Dev` μέσω SSMS. Ο πίνακας audit, το ενεργό append-only trigger, οι trusted constraints και το index ελέγχθηκαν. Έξι πραγματικά Web audit events επιβεβαίωσαν τον βασικό κύκλο τιμής. Η 10η migration **δεν απαιτεί επανεκτέλεση**. Κάθε επόμενο περιβάλλον χρειάζεται χωριστή επιθεώρηση SQL/backup/έγκριση πριν από schema update. Ελέγξτε με:

```powershell
git switch main
git pull --ff-only origin main
dotnet --list-sdks
dotnet restore AenaonERP.slnx --locked-mode
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Το `global.json` απαιτεί **.NET SDK 10.0.404** (rollForward `latestPatch`). Το preview δεν εφαρμόζει migrations· πριν από οποιοδήποτε apply απαιτούνται backup, έλεγχος SQL script και ρητή επιβεβαίωση της Development βάσης.

## Εκκρεμότητες, ιστορικές αποδοχές και μη εγκεκριμένες δυνατότητες

- POS, online/offline παραγγελιοληψία, Android waiter APK και παραγωγικό cloud deployment.
- ΦΠΑ/φορολογικός υπολογισμός, έκδοση/διαβίβαση myDATA/IMPACT, εγκεκριμένοι φορολογικοί χαρακτηρισμοί. Τα 254 fiscal review observations καταγράφηκαν ως **Pending**, όχι ως approved.
- Τιμή δεσμευμένη σε παραγγελία (immutable snapshot), εκπτώσεις και ιστορικό παραστατικών.
- **9.5C.2 — Αποδεκτό από χειριστή**: προαιρετικό barcode και αναλυτική περιγραφή, [Manual 52](52-catalog-item-details.md). Η τοπική 9η migration και το λειτουργικό acceptance ολοκληρώθηκαν.
- **9.5C.4 — Merged PR #59, όχι πλήρες human-style acceptance:** versioned in-memory snapshot και διατήρηση υφιστάμενων scopes. Δεν υπάρχει SQL order snapshot, customer groups, φορολογική σύνδεση ή νέα migration.
- **9.5C.3 — Ιστορικά αναφερθέν τοπικό schema και βασικό audit:** audit/history, overlap integrity και [Manual 53](53-pricing-integrity-history.md). Ο χειριστής αναφέρει επιτυχή human-style δοκιμή· λεπτομερής έκθεση/screenshots δεν έχουν επιθεωρηθεί ανεξάρτητα. **Sorting τιμοκαταλόγου:** [Manual 54](54-price-list-item-sorting.md), βελτίωση χωρίς νέα migration. **9.5C.4–9.5C.5:** commercial scopes/immutable snapshot contract και τελικό acceptance, σύμφωνα με το [Manual 51](51-catalog-pricing-gap-analysis.md).
- Το **9.6A.0 — Menu Architecture & UX** είναι **εγκεκριμένο ως σχεδιασμός (10/10 owner decisions)**, χωρίς κώδικα ή migration (Manuals 58/60). **Η υλοποίηση 9.6A.1** απαιτεί πλέον ξεχωριστή ρητή έγκριση και SQL migration review/backup: το 9.5C.5 human-style E2E/SQL έχει αναφερθεί PASS από τον ιδιοκτήτη, χωρίς ανεξάρτητη αναπαραγωγή από εδώ.
- Excel imports και μαζικές εργασίες: **αναβλημένα**.
- Οπτική αποδοχή της σελίδας preview σε Light/Dark, mobile και keyboard-only: **αναφέρεται επιτυχής από τον ιδιοκτήτη (17/11/2026), όχι ανεξάρτητα επανελεγμένη**.

## Επίσημη συμβατότητα

Ελέγχονται πριν από κάθε σχετική υλοποίηση οι [τρέχουσες εκδόσεις myDATA της ΑΑΔΕ](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) (κατά την παρούσα αποτύπωση: v2.0.2), οι ισχύουσες συμβάσεις IMPACT και το [συμβατότητα gate](18-mydata-impact-compatibility.md). Η τεχνική αποδελτίωση δεν είναι φορολογική έγκριση.

## GitBook και τεκμηρίωση

Ο πραγματικός GitBook space **AENAON ERP Documentation** εξετάστηκε σε επίπεδο περιεχομένου στις 16/11/2026. Οι σελίδες «Γνωριμία με το AENAON ERP» και «Επισκόπηση Καταλόγου» εξακολουθούσαν να αναφέρουν παλαιότερα PR #50/#51 και μη διαθέσιμο Barcode/LongDescription/Audit· επομένως χρειάζονται νέα, ανεξάρτητη **GitBook change request και review**, χωρίς σιωπηλή δημοσίευση. Οπτικό responsive QA του live GitBook δεν εκτελέστηκε.

Το **`docs/`** είναι η τεχνική πηγή manuals AENAON. Τα `documentation/`, `home/`, `help-center/`, `changelog/` στο repository περιλαμβάνουν **γενικά starter templates GitBook** και δεν έχουν εγκριθεί ως τρέχουσα τεκμηρίωση AENAON ERP. Πριν μεταφερθούν/αντικατασταθούν, απαιτείται έλεγχος των πραγματικών GitBook spaces και της συσχέτισής τους με το repository. Καμία καταστροφική επέμβαση στα templates στο παρόν PR.

## Διαδικασία ενημέρωσης

Μετά από κάθε merged feature PR ενημερώνουμε το [README](../README.md), αυτόν τον χάρτη και, εφόσον αλλάζει roadmap, το [requirements](requirements.md). Δεν παρουσιάζουμε τοπικά SQL/GUI tests, παραγωγική λειτουργία ή φορολογική πιστοποίηση ως επιβεβαιωμένα χωρίς πραγματικούς ελέγχους.
