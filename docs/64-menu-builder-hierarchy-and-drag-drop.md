# AENAON ERP — Manual 64: Ιεραρχικό Menu Builder, Drag & Drop και είδη στη ρίζα

**Κατάσταση:** Υλοποιείται στο **Draft PR #62** στο branch `feature/step-9-6a-1-menu-drafts`. **Όχι merged / όχι Production / όχι πλήρως αποδεκτό.**

**Ημερομηνία:** 17/11/2026 · **Εξάρτηση:** [Manual 61](61-draft-menu-management.md), [Manual 62](62-default-menu-auto-catalog-sync.md), [Manual 63](63-menu-builder-tile-redesign.md).

{% hint style="warning" %}
Η νέα **13η migration** `20261117170000_MenuBuilderHierarchyAndRootItems` υπάρχει **μόνο σε πηγαίο κώδικα**. **Δεν έχει εγκριθεί ούτε εφαρμοστεί** στην `AenaonERP_Dev`, ούτε σε Production. Η ήδη εφαρμοσμένη migration #12 παραμένει ως έχει. Μην εκκινήσετε το ενημερωμένο branch απέναντι σε βάση με εκκρεμή migration και μην επιλέξετε `--apply` χωρίς χωριστή έγκριση, ανάλυση SQL, νέο backup και ελεγμένο σχέδιο επαναφοράς.
{% endhint %}

## 1. Σκοπός και λειτουργική συμπεριφορά

Το προσχέδιο έχει **απεριόριστα λογικά επίπεδα κατηγοριών** υπό αυστηρό όριο 10.000 κατηγοριών ανά μενού και 10.000 θέσεων ανά sibling group. Στην αρχική οθόνη εμφανίζονται **μόνο οι κατηγορίες ρίζας και τα είδη ρίζας**. Επιλογή μιας κατηγορίας εμφανίζει **μόνο τις άμεσες υποκατηγορίες και τα άμεσα είδη της**, ποτέ τα στοιχεία άλλου επιπέδου. Το breadcrumb εμφανίζει ολόκληρο το μονοπάτι και επιτρέπει επιστροφή σε προηγούμενο επίπεδο.

Κατηγορίες/είδη μετακινούνται με το **⠿** και drag & drop **μέσα στο ίδιο επίπεδο**. Τα πλήκτρα **↑/↓** στο γρανάζι και τα **ArrowUp/ArrowDown** όταν έχει focus το ⠿ παρέχουν ισοδύναμη λειτουργία. Για διαφορετική τοποθέτηση/γονέα χρησιμοποιείται προσθήκη νέας υποκατηγορίας — δεν υποστηρίζεται ακόμη drag για αλλαγή γονικής κατηγορίας ούτε μεταφορά υπάρχοντος είδους μεταξύ κατηγοριών.

Δεν γίνεται Publish, order/POS, πληρωμή, τιμολόγηση, εκτύπωση ή φορολογική επικοινωνία.

## 2. Προϋποθέσεις

- Ενημερωμένη τοπική έκδοση του branch, HTTPS και έγκυρη συνεδρία.
- Tenant και Store που επιτρέπεται να διαβάζει ο χρήστης.
- **Owner** για καταχώριση, ενημέρωση, αλλαγή σειράς και διαγραφή. **Member**: μόνο προβολή.
- SQL Server Development schema με **όλες** τις εγκεκριμένες migrations, όταν και εφόσον εγκριθεί η #13.
- Ενεργά ERP Catalog είδη για προσθήκη.

## 3. Χρήση από browser

### 3.1 Αρχικό μενού

1. Από το sidebar ανοίξτε **Μενού εστίασης** και επιλέξτε το προσχέδιο.
2. Η αρχική οθόνη δείχνει τις κύριες κατηγορίες στο πρώτο πλέγμα και τα είδη χωρίς κατηγορία στο δεύτερο πλέγμα.
3. Για νέα κύρια κατηγορία πατήστε **Νέα κατηγορία**.
4. Για απευθείας είδος επιλέξτε **Από ERP Catalog**, αναζητήστε με κωδικό ή όνομα και πατήστε **+**. Το είδος αποθηκεύεται με `MenuCategoryId=NULL`.
5. Η κατάσταση **Προστέθηκε** ισχύει μόνο για το συγκεκριμένο επίπεδο. Το ίδιο ERP είδος μπορεί να εμφανίζεται σε άλλη κατηγορία χωρίς να αλλάζει ο κύριος κατάλογος.

### 3.2 Υποκατηγορίες

1. Επιλέξτε μία βασική κατηγορία.
2. Στο εσωτερικό της, πατήστε **Νέα υποκατηγορία**, εισαγάγετε όνομα και πατήστε **Προσθήκη**.
3. Η νέα κατηγορία καταχωρίζεται ως παιδί (`ParentCategoryId`) της επιλεγμένης, **όχι** ως πρόσθετη κατηγορία ρίζας.
4. Μπορείτε να μπείτε και σε αυτήν και να προσθέσετε ακόμη μία υποκατηγορία ή είδη.
5. Για επιστροφή χρησιμοποιήστε το breadcrumb **Αρχικό μενού → γονέας → παιδί**.
6. Μια κατηγορία αφαιρείται μόνο όταν **δεν έχει ούτε είδη ούτε υποκατηγορίες**.

### 3.3 Drag & drop και πλήκτρα

1. Σε desktop κρατήστε το **⠿** ενός πλακιδίου.
2. Σύρετε πάνω από πλακίδιο **του ίδιου πλέγματος** (κατηγορία προς κατηγορία / είδος προς είδος).
3. Αφήστε το ⠿. Η αναδιάταξη αποστέλλεται ως προστατευμένη POST με νέο `RequestId`, τρέχον `ExpectedRevision` και νέα `TargetPosition`.
4. Μετά το redirect ελέγξτε τη νέα σειρά και το revision.
5. Σε κινητό ή χωρίς mouse χρησιμοποιήστε το γρανάζι **↑/↓**. Με πληκτρολόγιο εστιάστε το ⠿ και πατήστε **ArrowUp/ArrowDown**, ή χρησιμοποιήστε τα κουμπιά.
6. Σύρσιμο μεταξύ δύο διαφορετικών επιπέδων δεν μετακινεί εγγραφή, για προστασία από κατά λάθος αλλαγή parent/scope.

## 4. Πεδία, έλεγχοι και όρια

| Πεδίο | Σημασία |
| --- | --- |
| `MenuId`, `TenantId` | Ασφαλής ιδιοκτησία συγκεκριμένου μενού/επιχείρησης |
| `ParentCategoryId` | Nullable γονική κατηγορία, στο ίδιο Tenant/Menu· NULL για ρίζα |
| `MenuCategoryId` | Nullable σύνδεση είδους· NULL σημαίνει είδος στο αρχικό μενού |
| `Position` | Σειρά μέσα στο ίδιο επίπεδο και στον ίδιο τύπο πλακιδίων |
| `CatalogCategoryId` | Υφιστάμενος αναγνωριστικός δεσμός με ERP Catalog, όταν υπάρχει |
| `CatalogItemId` | Υφιστάμενο ERP είδος του ίδιου Tenant |
| `ExpectedRevision` | Έλεγχος σύγχρονης τροποποίησης, stale αλλαγή απορρίπτεται |
| `RequestId` | Idempotency· διπλή αποστολή ίδιας εντολής δεν γράφει ξανά |
| `__RequestVerificationToken` | CSRF/antiforgery προστασία HTTPS φόρμας |

Οι σύνθετες SQL FKs αποτρέπουν parent άλλου Tenant/Menu και item από άλλο Tenant. Φιλτραρισμένα μοναδικά indexes αποτρέπουν διπλό ERP είδος στην ίδια κατηγορία ή στη ρίζα. Δεν απαιτούνται εξωτερικές JavaScript βιβλιοθήκες.

## 5. CLI / CMD

Κάθε σημαντική λειτουργία χρησιμοποιεί την κοινή Application service μέσω `--manage-menus`. Οι ακόλουθες είναι **ενδεικτικές ασφαλείς προεπισκοπήσεις** που δεν αλλάζουν SQL:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action details --menu-id "MENU_GUID" --dry-run
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action add-category --menu-id "MENU_GUID" --parent-category-id "PARENT_GUID" --name "Καφέδες" --expected-revision 1 --dry-run
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action add-item --menu-id "MENU_GUID" --catalog-item-id "CATALOG_ITEM_GUID" --expected-revision 1 --dry-run
```

Για `--apply`, password από διαδραστική κονσόλα (ποτέ σε command argument), σωστό `--confirm-database` και προέλεγχο schema απαιτούνται δικαίωμα Owner και **ξεχωριστή άδεια πραγματικής εγγραφής**.

## 6. Migration #13 — SQL review, recovery, compatibility

Η migration προσθέτει `MenuDraftCategories.ParentCategoryId NULL`, μετατρέπει `MenuDraftItems.MenuCategoryId` σε nullable, προσθέτει τοπικό self-FK, προστασία απαγόρευσης self-parent και νέα indexes. **Δεν διαγράφει κανένα μενού ή είδος**.

Για τις **ήδη υπάρχουσες** συνδεδεμένες ERP κατηγορίες: η migration γεμίζει τον γονέα **μόνο** όταν υπάρχει ρητό `CatalogCategoryId` για παιδί και γονέα στο ίδιο Tenant/Menu. Οι μη συνδεδεμένες χειροκίνητες κατηγορίες μένουν στη ρίζα. Διατηρείται η παλιά σειρά εντός κάθε επιπέδου με `ROW_NUMBER`. Εάν υπάρχει παλιό όνομα τύπου `Γονέας / Παιδί`, το UI δείχνει μόνο το τμήμα `Παιδί` στο εσωτερικό του γονέα· η DB ονομασία δεν τροποποιείται.

**Πριν από οποιαδήποτε APPLY:**
1. Ελέγξτε στο CI το τελευταίο commit και τον ακριβή generated incremental SQL. Ελέγξτε FK/index/filtered uniqueness, row normalization και σύγκρουση δεδομένων.
2. Επιβεβαιώστε αποκλειστικά `AenaonERP_Dev` και `12 applied / 1 pending` (αναμενόμενο πριν την #13, εφόσον δεν μεσολάβησαν άλλες αλλαγές).
3. Πάρτε νέο `BACKUP DATABASE`, `RESTORE VERIFYONLY` και ιδανικά πραγματικό restore σε απομονωμένο SQL Server.
4. Προετοιμάστε rollback/restore. Το `Down()` **αρνείται** επαναφορά όταν υπάρχουν root items **ή γονικές σχέσεις υποκατηγοριών**, πριν από οποιοδήποτε DDL, ώστε να μην απολεσθούν τα δεδομένα· απαιτείται εγκεκριμένη χειροκίνητη προετοιμασία/restore.
5. Ζητήστε **ρητή έγκριση για τη συγκεκριμένη Development migration**. Δεν επιτρέπεται αυτόματη εφαρμογή σε καμία βάση.
6. Μετά από approved APPLY κάντε read-only επιβεβαίωση 13/0, SQL persistence, tenant isolation και πλήρες browser E2E.

## 7. Έλεγχοι αποδοχής

- [ ] Κατάσταση migrations μόνο με read-only dry-run, χωρίς μη εγκεκριμένη APPLY.
- [ ] Release build, format, unit/SSR, isolated SQL 2022 integration PASS στη *τελική* έκδοση.
- [ ] Root: εμφανίζει μόνο κύριες κατηγορίες + root items.
- [ ] Μέσα σε κατηγορία: εμφανίζονται μόνο τα άμεσα παιδιά και άμεσα είδη.
- [ ] Δημιουργία child και nested child, refresh, σωστό breadcrumb.
- [ ] Root add item, duplicate submit, remove, reorder, reload.
- [ ] Category/item drag, εναλλακτικό keyboard και mobile.
- [ ] Αποτροπή αφαίρεσης μη άδειου parent και cross-tenant/cross-menu parent.
- [ ] Auto-sync νέας ERP parent/child κατηγορίας και είδους.
- [ ] Owner write / Member read only, stale revision, CSRF, idempotency.
- [ ] Real E2E στις 320/375/390/768/1366/1920 px, Light/Dark, zoom και screenshots.
- [ ] Independent SQL persistence check, owner acceptance, χωριστή έγκριση merge.

**Κατάσταση έως πραγματικό browser/SQL έλεγχο:** **IMPLEMENTED, NOT FULLY ACCEPTED**.

## 8. Troubleshooting και Best Practices

| Συμβάν | Έλεγχος |
| --- | --- |
| Η σελίδα αποτυγχάνει να φορτώσει | Σωστό branch / migration #13 εκκρεμής ή εφαρμοσμένη μόνο μετά από έγκριση |
| Δεν εμφανίζεται υποκατηγορία | Ελέγξτε την γονική κατηγορία και το breadcrumb |
| Δεν αποθηκεύεται σειρά | Μπορεί να έχει αλλάξει η Revision: refresh και retry, όχι force overwrite |
| Δεν λειτουργεί το drag σε κινητό | Χρησιμοποιήστε ↑/↓ από το γρανάζι |
| Δεν διαγράφεται κατηγορία | Υπάρχουν είδη ή παιδιά, αφαιρέστε πρώτα με ασφαλή σειρά |
| ERP παιδί παραμένει στη ρίζα | Ελέγξτε το CatalogCategoryId του γονέα και αν υπάρχει στο συγκεκριμένο menu |
| Το είδος δεν εμφανίζεται | Ελέγξτε επίπεδο πλοήγησης και IsActive του ERP Catalog |
| Member δεν βλέπει διαχείριση | Αναμενόμενο — μόνο Owner μπορεί να γράψει |

**Πότε χρησιμοποιείται:** Οργάνωση προσχεδίων εστίασης σε Development μετά από εγκεκριμένο schema update. **Πότε δεν χρησιμοποιείται:** Δημοσιευμένα μενού, POS, παραγγελίες, τιμές, φορολογικές εκδόσεις ή μη ελεγχόμενο Production.

**Best practices:** μικρές ενέργειες, ρητό refresh μετά από conflict, αποφυγή επαναλήψεων `--apply`, απομονωμένα QA δεδομένα, διατήρηση πλήρους audit, διασταύρωση κάθε UI αλλαγής με persisted SQL.
