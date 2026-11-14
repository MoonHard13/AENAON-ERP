# AENAON ERP — Κατάσταση και χάρτης τεκμηρίωσης

**Αποτύπωση:** 14/11/2026 · **Κατάσταση:** τα PR #52 και #53 έγιναν merge. Ο χειριστής αποδέχτηκε το 9.5C.2 (9/0 migrations, 7/7 έλεγχοι), ενώ επιβεβαίωσε dry-run για την 10η migration του 9.5C.3 (9 applied / 1 pending). Το τοπικό apply και η αποδοχή ιστορικού παραμένουν εκκρεμή. Η βελτίωση φυσικής ταξινόμησης ειδών τιμοκαταλόγου υλοποιείται σε ξεχωριστό PR χωρίς migration. Το 9.5C.1 είναι ιστορικό documentation-only audit.

{% hint style="info" %}
Τα manuals ανά βήμα διατηρούνται **ως ιστορική τεκμηρίωση**. Για την τρέχουσα κατάσταση προτιμήστε το [root README](../README.md), αυτόν τον χάρτη και τον ίδιο τον κώδικα του `main`. Τα παλαιότερα «δεν υπάρχει ακόμη» αφορούν τη συγκεκριμένη έκδοση του βήματος.
{% endhint %}

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
| Ιστορικό/ακεραιότητα τιμών | [9.5C.3 — Pricing Integrity & History](53-pricing-integrity-history.md): durable audit, overlap guard, Blazor/CLI, 10η migration (merged code· local apply pending) |
| Sorting τιμοκαταλόγου | [Διόρθωση ταξινόμησης ειδών](54-price-list-item-sorting.md): φυσική σειρά κωδικών, όλες οι ορατές στήλες, SQL paging, CLI, χωρίς νέα migration |
| Mobile Back Office | [Responsive UX](55-mobile-backoffice-responsive.md): dashboard, κατάλογος, τιμοκατάλογοι, καρτέλες/ιστορικό και native mobile sorting· frontend-only |
| Μελλοντικά | [Περιβαλλοντικά τέλη — design](47-environmental-fees-fiscal-design.md), [Excel/bulk — αναβολή](48-bulk-actions-and-excel-import-plan.md) |

## Database / installation

Ο χειριστής επιβεβαίωσε **9 εφαρμοσμένες / 1 εκκρεμή** migration στην `AenaonERP_Dev`, μέσω πραγματικού dry-run μετά το merged PR #53. Η **10η migration** `20261114163000_PriceListAuditHistory` προσθέτει audit table και SQL append-only trigger. **Δεν έχει εφαρμοστεί ακόμη** στη Development βάση. Χρειάζονται SQL review, επαληθευμένο backup και ρητό apply· η βελτίωση sorting δεν προσθέτει migration. Ελέγξτε με:

```powershell
git switch main
git pull --ff-only origin main
dotnet --list-sdks
dotnet restore AenaonERP.slnx --locked-mode
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Το `global.json` απαιτεί **.NET SDK 10.0.404** (rollForward `latestPatch`). Το preview δεν εφαρμόζει migrations· πριν από οποιοδήποτε apply απαιτούνται backup, έλεγχος SQL script και ρητή επιβεβαίωση της Development βάσης.

## Μη υλοποιημένα / μη εγκεκριμένα

- POS, online/offline παραγγελιοληψία, Android waiter APK και παραγωγικό cloud deployment.
- ΦΠΑ/φορολογικός υπολογισμός, έκδοση/διαβίβαση myDATA/IMPACT, εγκεκριμένοι φορολογικοί χαρακτηρισμοί. Τα 254 fiscal review observations καταγράφηκαν ως **Pending**, όχι ως approved.
- Τιμή δεσμευμένη σε παραγγελία (immutable snapshot), εκπτώσεις και ιστορικό παραστατικών.
- **9.5C.2 — Αποδεκτό από χειριστή**: προαιρετικό barcode και αναλυτική περιγραφή, [Manual 52](52-catalog-item-details.md). Η τοπική 9η migration και το λειτουργικό acceptance ολοκληρώθηκαν.
- **9.5C.3 — Κώδικας merged, τοπική αποδοχή εκκρεμεί:** audit/history, overlap integrity και [Manual 53](53-pricing-integrity-history.md). Έχει επιβεβαιωθεί μόνο το dry-run 9/1, όχι το apply. **Sorting τιμοκαταλόγου:** [Manual 54](54-price-list-item-sorting.md), βελτίωση χωρίς νέα migration. **9.5C.4–9.5C.5:** commercial scopes/immutable snapshot contract και τελικό acceptance, σύμφωνα με το [Manual 51](51-catalog-pricing-gap-analysis.md).
- Το **9.6A — Μενού εστίασης** αρχίζει μόνο μετά την ολοκλήρωση και αποδοχή των παραπάνω.
- Excel imports και μαζικές εργασίες: **αναβλημένα**.
- Οπτική αποδοχή της σελίδας preview σε Light/Dark, mobile και keyboard-only: **εκκρεμής**.

## Επίσημη συμβατότητα

Ελέγχονται πριν από κάθε σχετική υλοποίηση οι [τρέχουσες εκδόσεις myDATA της ΑΑΔΕ](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) (κατά την παρούσα αποτύπωση: v2.0.2), οι ισχύουσες συμβάσεις IMPACT και το [συμβατότητα gate](18-mydata-impact-compatibility.md). Η τεχνική αποδελτίωση δεν είναι φορολογική έγκριση.

## GitBook και τεκμηρίωση

Το **`docs/`** είναι η τεχνική πηγή manuals AENAON. Τα `documentation/`, `home/`, `help-center/`, `changelog/` στο repository περιλαμβάνουν **γενικά starter templates GitBook** και δεν έχουν εγκριθεί ως τρέχουσα τεκμηρίωση AENAON ERP. Πριν μεταφερθούν/αντικατασταθούν, απαιτείται έλεγχος των πραγματικών GitBook spaces και της συσχέτισής τους με το repository. Καμία καταστροφική επέμβαση στα templates στο παρόν PR.

## Διαδικασία ενημέρωσης

Μετά από κάθε merged feature PR ενημερώνουμε το [README](../README.md), αυτόν τον χάρτη και, εφόσον αλλάζει roadmap, το [requirements](requirements.md). Δεν παρουσιάζουμε τοπικά SQL/GUI tests, παραγωγική λειτουργία ή φορολογική πιστοποίηση ως επιβεβαιωμένα χωρίς πραγματικούς ελέγχους.
