# AENAON ERP — Manual 65: Preflight και έλεγχος μετά τη migration #13

**Κατάσταση (18/11/2026):** Η migration #13 **εγκρίθηκε και εφαρμόστηκε από τον ιδιοκτήτη αποκλειστικά στην Development `AenaonERP_Dev`**. Τα αποσταλμένα αποτελέσματα `--apply` και `--dry-run` συμφωνούν: **13 applied / 0 pending**. Backup owner-attested, isolated restore NOT VERIFIED. Το PR #62 παραμένει Draft, **όχι merged / όχι Production**. Η παρούσα preflight διαδικασία είναι πλέον ιστορική για τη συγκεκριμένη βάση.

Σχετικά: [Manual 64](64-menu-builder-hierarchy-and-drag-drop.md) · [SSMS preflight (ιστορικό)](../scripts/sql/013-menu-builder-preflight.sql) · [SSMS postmigration integrity review (επόμενο βήμα)](../scripts/sql/014-menu-builder-postmigration-review.sql) · [migration source](../src/Aenaon.Infrastructure/Persistence/Migrations/20261117170000_MenuBuilderHierarchyAndRootItems.cs).

{% hint style="warning" %}
Η Development εφαρμογή της #13 έχει ήδη ολοκληρωθεί σύμφωνα με τα αποτελέσματα του χειριστή. **Μην επανεκτελέσετε** παραγόμενο SQL, --apply ή dotnet ef database update. Τα read-only preflight βήματα παραμένουν ιστορικό τεκμήριο· η επόμενη ενέργεια είναι η επιθεώρηση postmigration δεδομένων, χωρίς καμία εγγραφή.
{% endhint %}

## 1. Σκοπός και αλλαγές σχήματος

Η migration #13 προσθέτει nullable ParentCategoryId στις MenuDraftCategories και κάνει nullable το MenuCategoryId των MenuDraftItems. Διατηρεί τα IDs και τους υφιστάμενους δεσμούς. Προσθέτει σύνθετα Foreign Keys ίδιου Tenant/Menu και filtered unique indexes για τις συνδέσεις ειδών κατηγορίας και ρίζας.

Η κατηγοριοποίηση υφιστάμενων menu categories συνδέει τον γονέα **μόνο** μέσω του ERP CatalogCategoryId. Δεν κάνει αντιστοίχιση βάσει ονόματος. Η Up εκτελεί πραγματικά UPDATE στο ParentCategoryId και επαναριθμεί τις θέσεις ανά νέο γονέα, χωρίς εντολές διαγραφής επιχειρησιακών εγγραφών. **Δεν είναι αμιγώς schema-only migration.**

Το Down αρνείται rollback εάν υπάρχουν nested κατηγορίες ή root items, πριν από αλλαγές DDL. Η πρακτική ασφαλής επαναφορά είναι restore δοκιμασμένου backup. Δεν βασιζόμαστε σε αυτόματο Down.

## 2. Προϋποθέσεις

- Μόνο SQL Server Development, ακριβής βάση AenaonERP_Dev.
- Branch feature/step-9-6a-1-menu-drafts, χωρίς μη ελεγμένες τοπικές αλλαγές.
- Εγκατεστημένο SDK από global.json και dotnet-ef από το τοπικό tool manifest.
- **Ιστορικά πριν από τη #13:** ο χειριστής επιβεβαίωσε 12 applied / 1 pending με CLI και με πραγματικά SQL αποτελέσματα (3 menus, 3 drafts, 27 categories, 39 items, 6 intended parent links). **Μετά την APPLY (18/11/2026): 13 applied / 0 pending**, owner-provided CLI output.
- CI στον τελευταίο commit με unit, API και απομονωμένο SQL 2022 migration upgrade.

## 3. Read-only έλεγχος CLI

Στο VS Code PowerShell:

~~~powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git status
git fetch origin
git switch feature/step-9-6a-1-menu-drafts
git pull --ff-only origin feature/step-9-6a-1-menu-drafts
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
~~~

**Ιστορικό pre-apply αποτέλεσμα:** 12 applied / 1 pending, migration 20261117170000_MenuBuilderHierarchyAndRootItems. Το αποτέλεσμα **δεν** ισχύει πλέον στη Development μετά την εγκεκριμένη εφαρμογή. Η τρέχουσα κατάσταση είναι 13/0. Αν το git status δείχνει αλλαγές, μη χρησιμοποιήσετε reset --hard ή clean.

## 4. SSMS preflight

1. Συνδεθείτε μόνο στην AenaonERP_Dev και επιλέξτε την ίδια βάση στο dropdown.
2. Ανοίξτε scripts/sql/013-menu-builder-preflight.sql και διαβάστε το.
3. Εκτελέστε το read-only script: μόνο SELECT και fail-closed THROW, χωρίς UPDATE/DDL.
4. Ελέγξτε πλήθος μενού, categories και items, ιστορικό 12 migrations, προτεινόμενους ERP γονείς, χαμένους parent links, ενδεχόμενα duplicate positions και orphan items.
5. **ParentNotInMenu μεγαλύτερο από μηδέν:** το συγκεκριμένο ERP παιδί θα παραμείνει στη ρίζα, απαιτείται ανθρώπινη αξιολόγηση. Τα 0 orphan και μηδενικές υπερβάσεις των 10.000 κατηγοριών ανά μενού είναι υποχρεωτικά.

Το preflight δεν πιστοποιεί μόνο του την ορθότητα των δεδομένων· χρειάζεται και ερμηνεία των αποτελεσμάτων.

## 5. Παραγωγή SQL migration για ανασκόπηση

Το παρακάτω παράγει μόνο αρχείο χωρίς εκτέλεση:

~~~powershell
dotnet tool restore
New-Item -ItemType Directory -Force "artifacts\sql-review" | Out-Null
dotnet ef migrations script 20261117130000_DefaultStoreMenuCatalogSync 20261117170000_MenuBuilderHierarchyAndRootItems --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api --output "artifacts\sql-review\menu-builder-migration-13.sql"
~~~

Ελέγξτε προσεκτικά το SQL αρχείο: ακριβείς τύπους nullable, ενημερώσεις γονέων, ROW_NUMBER, φίλτρα indexes, FKs, constraints και απουσία μη αναμενόμενου DML/DDL. Αν η παραγωγή αποτύχει, κρατήστε το σφάλμα για ανάλυση — όχι χειροκίνητα SQL workarounds.

**Ο generated SQL review στη δική σας Development έκδοση παραμένει NOT VERIFIED μέχρι να διαβαστεί το αρχείο.**

## 6. Ασφαλές backup και recovery (μόνο μετά από χωριστή άδεια)

Πριν από μελλοντική εφαρμογή απαιτείται νέο backup από εξουσιοδοτημένο χειριστή σε πραγματικό, προσβάσιμο από τον SQL Server path, CHECKSUM, RESTORE VERIFYONLY και πραγματικό isolated restore.

Υπόδειγμα, **ΜΗΝ ΤΟ ΕΚΤΕΛΕΣΕΤΕ ΤΩΡΑ**:

~~~sql
BACKUP DATABASE [AenaonERP_Dev]
TO DISK = N'<BACKUP_PATH_ON_SQL_SERVER>\AenaonERP_Dev_before_migration_13.bak'
WITH COPY_ONLY, INIT, COMPRESSION, CHECKSUM, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'<BACKUP_PATH_ON_SQL_SERVER>\AenaonERP_Dev_before_migration_13.bak'
WITH CHECKSUM;
~~~

Το VERIFYONLY **δεν ισοδυναμεί με επιτυχημένο restore**. Κανένα backup ή recovery operation δεν εκτελέστηκε από τον reviewer.

## 7. Postmigration επαλήθευση και checklist

Στο SSMS, επιλέξτε μόνο την `AenaonERP_Dev` και ανοίξτε `scripts/sql/014-menu-builder-postmigration-review.sql`. Εκτελέστε το read-only script και ελέγξτε: **3 menus / 3 drafts / 27 categories / 39 items / 19 ERP-linked categories**, **6 nested categories**, **0 invalid parent links, invalid category items, orphan menu items**, καμία διπλή θέση ανά γονέα ή διπλό ERP είδος ανά επίπεδο, νέα FKs/check constraints ενεργά και trusted (`IsDisabled=0`, `IsNotTrusted=0`). Τυχόν απόκλιση χρειάζεται αξιολόγηση πριν από το browser acceptance, όχι αυτόματη διόρθωση.

Η ιστορική έκδοση (pre-apply) του ελέγχου αφορούσε 12/1· η σημερινή Development έχει 13/0. Αυτή η σύγκριση βασίζεται σε στοιχεία χειριστή, **όχι ανεξάρτητη σύνδεση reviewer στη Development SQL**.


- [ ] GitHub CI SUCCESS στο τελικό HEAD.
- [x] Ιστορικό Development dry-run 12/1 πριν από την εφαρμογή και νέο dry-run 13/0 μετά από αυτήν (owner-provided).
- [ ] Read-only SSMS preflight: σωστή βάση, αμετάβλητα δεδομένα, μηδενικά orphan records.
- [ ] Έλεγχος των πραγματικών parent-child mappings και orphan parent links.
- [ ] Ανάλυση ακριβούς παραγόμενου SQL χωρίς μη αναμενόμενες εντολές.
- [ ] Νέο backup, VERIFYONLY, δοκιμασμένο isolated restore.
- [x] Χωριστή ρητή έγκριση και εφαρμογή #13 αποκλειστικά στην AenaonERP_Dev (owner-provided).
- [ ] Απομένουν: postmigration SQL integrity, πραγματική persistence/tenant isolation, Owner/Member και real browser Desktop/Mobile/Light-Dark/drag E2E.
- [ ] Χωριστή έγκριση για merge, καμία Production/POS/Publish/fiscal αλλαγή.

**Troubleshooting:** αν το preflight σταματά λόγω ονόματος βάσης ή ιστορικού, ελέγξτε το SSMS dropdown και migrations· αν λείπει γονέας, ελέγξτε CatalogCategoryId του ίδιου Tenant/Menu χωρίς χειροκίνητο UPDATE· αν αποτύχει dotnet ef script, κρατήστε το design-time error.

**Πότε χρησιμοποιείται:** μόνο πριν την έγκριση νέας Development migration. **Πότε δεν χρησιμοποιείται:** Production, εφαρμογή SQL, POS, παραγγελίες ή φορολογικές λειτουργίες.

**Κατάσταση:** Development migration applied per owner's logs, postmigration SQL/browser acceptance NOT VERIFIED — NOT FULLY ACCEPTED.
