# AENAON ERP — Κατάσταση και χάρτης τεκμηρίωσης

**Αποτύπωση:** 14/11/2026 · **Βάση σχεδιασμού:** merged PR #51. **Νεότερο feature στον κώδικα:** 9.5C.2 — barcode/αναλυτική περιγραφή (PR #52, μετά το review/merge στο `main`). Το 9.5C.1 παραμένει documentation-only audit.

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
| Μελλοντικά | [Περιβαλλοντικά τέλη — design](47-environmental-fees-fiscal-design.md), [Excel/bulk — αναβολή](48-bulk-actions-and-excel-import-plan.md) |

## Database / installation

Στον κώδικα μετά το PR #52 υπάρχουν **9 EF Core migrations**, τελευταία `20261114151000_CatalogItemDetails`. Η εφαρμογή τους στην τοπική `AenaonERP_Dev` δεν επιβεβαιώνεται από το GitHub. Η νέα migration είναι additive με δύο nullable στήλες, check και filtered unique index. Πριν από apply απαιτούνται dry-run, SQL review και backup. **Η κατάσταση της τοπικής `AenaonERP_Dev` δεν επαληθεύεται από το GitHub.** Ελέγξτε με:

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
- **9.5C.2 — Υλοποιήθηκε στον κώδικα**: προαιρετικό barcode και αναλυτική περιγραφή, [Manual 52](52-catalog-item-details.md). Τοπικό SQL apply και visual acceptance απαιτούν επιβεβαίωση.
- **9.5C.3–9.5C.5 — Προηγούνται του 9.6A:** price audit/history, overlap integrity, commercial scopes και τελικό acceptance, σύμφωνα με το [Manual 51](51-catalog-pricing-gap-analysis.md).
- Το **9.6A — Μενού εστίασης** αρχίζει μόνο μετά την ολοκλήρωση και αποδοχή των παραπάνω.
- Excel imports και μαζικές εργασίες: **αναβλημένα**.
- Οπτική αποδοχή της σελίδας preview σε Light/Dark, mobile και keyboard-only: **εκκρεμής**.

## Επίσημη συμβατότητα

Ελέγχονται πριν από κάθε σχετική υλοποίηση οι [τρέχουσες εκδόσεις myDATA της ΑΑΔΕ](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) (κατά την παρούσα αποτύπωση: v2.0.2), οι ισχύουσες συμβάσεις IMPACT και το [συμβατότητα gate](18-mydata-impact-compatibility.md). Η τεχνική αποδελτίωση δεν είναι φορολογική έγκριση.

## GitBook και τεκμηρίωση

Το **`docs/`** είναι η τεχνική πηγή manuals AENAON. Τα `documentation/`, `home/`, `help-center/`, `changelog/` στο repository περιλαμβάνουν **γενικά starter templates GitBook** και δεν έχουν εγκριθεί ως τρέχουσα τεκμηρίωση AENAON ERP. Πριν μεταφερθούν/αντικατασταθούν, απαιτείται έλεγχος των πραγματικών GitBook spaces και της συσχέτισής τους με το repository. Καμία καταστροφική επέμβαση στα templates στο παρόν PR.

## Διαδικασία ενημέρωσης

Μετά από κάθε merged feature PR ενημερώνουμε το [README](../README.md), αυτόν τον χάρτη και, εφόσον αλλάζει roadmap, το [requirements](requirements.md). Δεν παρουσιάζουμε τοπικά SQL/GUI tests, παραγωγική λειτουργία ή φορολογική πιστοποίηση ως επιβεβαιωμένα χωρίς πραγματικούς ελέγχους.
