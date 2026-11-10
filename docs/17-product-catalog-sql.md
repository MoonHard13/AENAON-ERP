# Aenaon ERP — Βήμα 9.2: SQL Server Product Catalog Persistence

## 1. Σκοπός

Προσθήκη μόνιμης αποθήκευσης για τις κατηγορίες, τα προϊόντα και τις υπηρεσίες του Aenaon ERP στον SQL Server, χωρίς μεταβολή των υφιστάμενων δεδομένων χρηστών, επιχειρήσεων και καταστημάτων.

{% hint style="warning" %}
Η εφαρμογή της migration **αλλάζει πραγματικά το schema της βάσης**. Πριν από κάθε `--apply`, πάρε επιβεβαιωμένο SQL backup, έλεγξε το όνομα βάσης και επιθεώρησε το παραγόμενο SQL. Δεν απαιτείται να εκτελέσεις `--apply` για να ελέγξεις το Pull Request.
{% endhint %}

## 2. Αρχεία και αρχιτεκτονική

| Αρχείο | Ρόλος |
| --- | --- |
| `CatalogCategoryConfiguration.cs` | Mapping, unique index και Tenant FK για κατηγορίες. |
| `CatalogItemConfiguration.cs` | Mapping, composite tenant/category FK, indexes και SQL CHECK. |
| `AenaonDbContext.cs` | DbSets καταλόγου και ενεργοποίηση configurations. |
| `Migrations/20261110123500_ProductCatalogPersistence.cs` | Δημιουργία δύο νέων πινάκων. |
| `Migrations/AenaonDbContextModelSnapshot.cs` | Τρέχον EF Core schema. |
| `SqlServerCatalogAssertions.cs` | Πραγματικές δοκιμές απομόνωσης και constraints σε προσωρινή βάση CI. |

## 3. Σχήμα

### dbo.CatalogCategories

`Id` (GUID PK), `TenantId` (GUID FK), `Code` (nvarchar(30)), `Name` (nvarchar(160)), `IsActive` (bit).

- Μοναδικός κωδικός ανά `TenantId`: unique index `IX_CatalogCategories_TenantId_Code`.
- `Greek_CI_AS` για σύγκριση κωδικών χωρίς διάκριση πεζών/κεφαλαίων.
- Alternate key `(TenantId, Id)` για προστασία των σχέσεων ειδών.
- Το `TenantId` αναφέρεται σε υπαρκτή επιχείρηση. Δεν επιτρέπεται cascade delete.

### dbo.CatalogItems

`Id` (GUID PK), `TenantId` (GUID FK), `CategoryId` (GUID), `Code` (nvarchar(40)), `Name` (nvarchar(200)), `Kind` (int), `TracksStock` (bit), `IsActive` (bit).

- Unique index `IX_CatalogItems_TenantId_Code`, κοινός για προϊόντα και υπηρεσίες.
- Composite FK `(TenantId, CategoryId)` προς `dbo.CatalogCategories(TenantId, Id)`.
- `Kind=1`: προϊόν· `Kind=2`: υπηρεσία.
- Check constraint αποτρέπει `Kind=2 AND TracksStock=1`.
- Τα foreign keys δεν επιτρέπουν αυτόματη διαγραφή κατηγοριών ή επιχειρήσεων.

{% hint style="info" %}
Η μοναδικότητα κωδικών διασφαλίζεται πλέον **ακόμη και σε ταυτόχρονες εγγραφές SQL**. Το Domain εξακολουθεί να εφαρμόζει δικούς του κανόνες για γρήγορη επικύρωση πριν από τη βάση.
{% endhint %}

## 4. Προϋποθέσεις

- Εγκατεστημένο .NET SDK 10, σύμφωνα με `global.json`.
- SQL Server 2022 και development βάση `AenaonERP_Dev` με collation `Greek_CI_AS`.
- Έγκυρο Development User Secrets connection string `ConnectionStrings:Aenaon`, χωρίς αποθήκευση password στο Git.
- Ο λογαριασμός migration να έχει δικαιώματα μεταβολής schema στη συγκεκριμένη development βάση.
- Να έχει γίνει επιτυχές restore/build και να μην υπάρχουν άλυτα σφάλματα GitHub Actions.

## 5. Προέλεγχος (δεν γράφει στη βάση)

Από PowerShell στη ρίζα του repository:

```powershell
git status -sb
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore

dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Εφόσον η βάση έχει τις δύο προηγούμενες migrations, αναμένεται μία εκκρεμής `20261110123500_ProductCatalogPersistence`. Αν διαφέρει η κατάσταση, μην εκτελέσεις εφαρμογή πριν ελέγξεις τη βάση.

### Επιθεώρηση SQL χωρίς σύνδεση

```powershell
dotnet tool restore

dotnet ef migrations script 20261109110355_IdentityAndTenantMemberships 20261110123500_ProductCatalogPersistence --idempotent --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api --output catalog-migration-review.sql
```

Άνοιξε το `catalog-migration-review.sql` στο VS Code και επιβεβαίωσε ότι δημιουργούνται **μόνο** οι δύο πίνακες, τα indexes, τα constraints και η εγγραφή ιστορικού. Το αρχείο είναι τοπικό review artifact και δεν χρειάζεται να προστεθεί στο Git.

## 6. Backup και προεπισκόπηση

{% hint style="danger" %}
Η πραγματική βάση δεν χρησιμοποιείται ως περιβάλλον δοκιμών καταστροφικών αλλαγών. Πριν το `--apply`, αποθήκευσε SQL Server backup και έλεγξε ότι υπάρχει διαδικασία επιτυχούς επαναφοράς.
{% endhint %}

1. Επιβεβαίωσε στο SSMS ότι βρίσκεσαι στη `AenaonERP_Dev`, όχι σε παραγωγική βάση.
2. Έλεγξε το ιστορικό με `SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;`.
3. Πάρε SQL Server **full backup** σε προορισμό προσβάσιμο από τον SQL Server service account.
4. Επιθεώρησε το SQL script της ενότητας 5.
5. Επανέλαβε το `--dry-run`, επιβεβαιώνοντας ότι εμφανίζει μία εκκρεμή migration.

## 7. Εφαρμογή (μόνο μετά από backup και έλεγχο)

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Η εφαρμογή γίνεται με το υπάρχον ασφαλές Development-only CLI. Δεν ενεργοποιούμε αυτόματη migration κατά την εκκίνηση του API.

## 8. Έλεγχοι μετά την εφαρμογή

Στο SSMS, στη `AenaonERP_Dev`:

```sql
SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT name
FROM sys.tables
WHERE name IN (N'CatalogCategories', N'CatalogItems')
ORDER BY name;

SELECT name, parent_object_id
FROM sys.foreign_keys
WHERE parent_object_id IN
(
    OBJECT_ID(N'dbo.CatalogCategories'),
    OBJECT_ID(N'dbo.CatalogItems')
);

SELECT COUNT(*) AS CategoryCount FROM dbo.CatalogCategories;
SELECT COUNT(*) AS ItemCount FROM dbo.CatalogItems;
```

Σε νέα εγκατάσταση αναμένονται δύο νέοι άδειοι πίνακες, εκτός αν έχουν εισαχθεί στη συνέχεια πραγματικά στοιχεία.

## 9. Tests, logging και ασφάλεια

```powershell
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Τα SQL Server integration tests εκτελούνται **μόνο** στο απομονωμένο CI container, με προσωρινές βάσεις. Ελέγχουν ανάκτηση των τιμών `Product`/`Service`, κοινή μοναδικότητα ανά Tenant, αλλότρια category references, stock constraint και περιορισμούς διαγραφής.

Οι εργασίες migration και τα SQL σφάλματα καταγράφονται με structured logging χωρίς διαπιστευτήρια. Δεν προστέθηκαν HTTP endpoints καταλόγου και η υπάρχουσα λειτουργία Login/Dashboard παραμένει ανέπαφη.

## 10. Troubleshooting

| Σύμπτωμα | Ενέργεια |
| --- | --- |
| `UnsafeDatabase` | Μη συνεχίσεις. Έλεγξε ιστορικό migrations και αρχική κατάσταση. |
| `InvalidConfiguration` | Έλεγξε Development, User Secrets και στόχο `AenaonERP_Dev`. |
| Σφάλμα 2601 / 2627 | Διπλός κωδικός είδους ή κατηγορίας στον ίδιο Tenant. |
| Σφάλμα 547 | Ξένη/ανύπαρκτη κατηγορία, tenant, περιορισμός υπηρεσίας ή απαγορευμένη διαγραφή. |
| Timeout | Εκτέλεσε `--dry-run` πριν από νέα προσπάθεια· μην επαναλάβεις τυφλά. |
| Pending migration μετά το `--apply` | Έλεγξε τα logs και το `__EFMigrationsHistory`, χωρίς χειροκίνητο INSERT. |

## 11. Rollback και αποκατάσταση

- Σε περίπτωση αποτυχίας πριν από την εφαρμογή, δεν απαιτείται επαναφορά της βάσης.
- Σε περίπτωση επιτυχούς migration, επιστροφή στο προηγούμενο schema ενδέχεται να διαγράψει **όλες τις κατηγορίες και τα είδη**. Μην εκτελέσεις downgrade χωρίς backup και σχέδιο ανάκτησης.
- Προτιμάται διορθωτική forward migration. Για πλήρη επαναφορά, χρησιμοποίησε ελεγμένο SQL Server backup σε κατάλληλο περιβάλλον.
- Για αλλαγές κώδικα μπορεί να γίνει Git revert του PR, αλλά **το Git revert δεν αναιρεί SQL migration**.

## 12. Checklist

- [ ] Έλεγχος σωστής βάσης και πλήρους backup.
- [ ] Επιβεβαίωση SQL script.
- [ ] `--dry-run` δείχνει ακριβώς μία εκκρεμή migration.
- [ ] GitHub Actions Windows/Ubuntu/SQL πράσινα.
- [ ] Εφαρμογή `--apply` μόνο μετά από επιβεβαίωση.
- [ ] `dbo.CatalogCategories` και `dbo.CatalogItems` υπάρχουν.
- [ ] Οι υπάρχουσες επιχειρήσεις/χρήστες/καταστήματα παρέμειναν αμετάβλητοι.

## 13. Επόμενο βήμα

Το Βήμα 9.3 θα υλοποιήσει ασφαλείς Application υπηρεσίες με Owner/Member authorization και πραγματική Blazor Back Office οθόνη. Η παρούσα migration **δεν παρέχει CRUD UI ή CLI ειδών**.
