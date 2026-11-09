# Aenaon ERP — Πίνακες επιχειρήσεων και καταστημάτων

## Σκοπός και περιγραφή

Το τέταρτο βήμα αντιστοιχίζει τα υπάρχοντα μοντέλα `Tenant` και `Store` σε SQL Server με EF Core 10.0.14. Η πρώτη migration δημιουργεί δύο επιχειρησιακούς πίνακες και το ιστορικό εκδόσεων του EF Core σε ήδη υπάρχουσα βάση ανάπτυξης.

Η διαχείριση γίνεται από CLI, με χωριστές ενέργειες προεπισκόπησης και εφαρμογής. Η εκκίνηση του HTTP API εξακολουθεί να λειτουργεί ανεξάρτητα από τις migrations. Τα στοιχεία σύνδεσης διαβάζονται από το υπάρχον `ConnectionStrings:Aenaon`.

{% hint style="info" %}
Τα παραδείγματα με μία migration περιγράφουν το αρχικό τέταρτο βήμα. Η τρέχουσα έκδοση περιλαμβάνει και τη δεύτερη migration χρηστών/συμμετοχών. Για τη συνέχεια και την τρέχουσα αναμενόμενη κατάσταση δες τον [οδηγό του έκτου βήματος](06-users-and-memberships.md).
{% endhint %}

## Προϋποθέσεις και έλεγχοι πριν την εκτέλεση

- Ολοκληρωμένο το τρίτο βήμα και επιτυχημένο `--check-database`.
- Ξεχωριστή βάση ανάπτυξης `AenaonERP_Dev`, compatibility level `160`.
- Το SQL login έχει δικαιώματα ανάγνωσης metadata και δημιουργίας πινάκων/constraints στο schema `dbo` της συγκεκριμένης βάσης.
- Η αρχική βάση δεν περιέχει άλλα αντικείμενα χρήστη. Το CLI απορρίπτει αντικείμενα χωρίς συμβατό ιστορικό migrations.
- Το API είναι σταματημένο κατά την ενημέρωση και εφαρμογή του σχήματος.
- Αν η βάση περιέχει χρήσιμα δεδομένα, υπάρχει πρόσφατο, ελεγμένο backup πριν από οποιαδήποτε αλλαγή σχήματος.

{% hint style="warning" %}
Η εφαρμογή migrations μεταβάλλει το σχήμα της βάσης που ορίζει το τοπικό connection string. Έλεγξε πρώτα το αποτέλεσμα του `--dry-run` και επιβεβαίωσε το ακριβές όνομα βάσης. Οι εντολές αυτού του σταδίου προορίζονται αποκλειστικά για Development.
{% endhint %}

## 1. Ενημέρωση κώδικα

Σταμάτησε το API με `Ctrl+C`. Από τον κεντρικό φάκελο του repository:

```powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet test AenaonERP.slnx --configuration Release --no-restore --filter "TestCategory!=SqlServer"
```

Διατήρησε τυχόν δικές σου αλλαγές σε commit/branch πριν από το pull. Τα ήδη ρυθμισμένα User Secrets παραμένουν διαθέσιμα.

## 2. Προεπισκόπηση

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Σε νέα βάση αναμένεται:

```text
Βάση: AenaonERP_Dev
Εφαρμοσμένες migrations: 0
Εκκρεμείς migrations: 1
  20261109100218_InitialTenancy
Η προεπισκόπηση ολοκληρώθηκε χωρίς αλλαγές στη βάση.
```

Η προεπισκόπηση ανοίγει σύνδεση στην υπάρχουσα βάση και διαβάζει compatibility level, αντικείμενα και ιστορικό migrations. Δεν δημιουργεί ούτε τον πίνακα ιστορικού. Τυχόν JSON logs πριν από το αποτέλεσμα είναι η κανονική δομημένη καταγραφή της λειτουργίας.

Το ακριβές SQL της πρώτης migration διατίθεται στο [database/001-initial-tenancy.sql](../database/001-initial-tenancy.sql) για επιθεώρηση. Έχει παραχθεί από το επίσημο EF CLI ως idempotent script. Η παρακάτω διαδικασία χρησιμοποιεί την προστατευμένη εντολή της εφαρμογής για την εκτέλεση.

## 3. Εφαρμογή

Αφού επιβεβαιώσεις τον στόχο και τις εκκρεμείς αλλαγές:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Το όνομα μετά το `--confirm-database` πρέπει να συμφωνεί ακριβώς, με ίδια πεζά/κεφαλαία, με το `Database` του connection string. Δεν επιλέγει άλλη βάση και δεν αλλάζει τα User Secrets.

Σε επιτυχία εμφανίζεται:

```text
Βάση: AenaonERP_Dev
Εφαρμοσμένες migrations: 1
Εκκρεμείς migrations: 0
Οι migrations εφαρμόστηκαν επιτυχώς.
```

Μια επανάληψη στην ήδη ενημερωμένη βάση επιστρέφει επιτυχία χωρίς επανεκτέλεση της migration. Δεν εισάγονται δοκιμαστικές επιχειρήσεις ή καταστήματα από την εντολή.

## Πίνακες και πεδία

| Πίνακας / πεδίο | Τύπος και χρήση |
| --- | --- |
| `dbo.Tenants.Id` | `uniqueidentifier`, primary key· δημιουργείται από το Domain. |
| `dbo.Tenants.Name` | `nvarchar(160)`, υποχρεωτικό όνομα επιχείρησης. |
| `dbo.Stores.Id` | `uniqueidentifier`, primary key καταστήματος. |
| `dbo.Stores.TenantId` | Υποχρεωτικό foreign key προς `dbo.Tenants.Id`. |
| `dbo.Stores.Code` | `nvarchar(30)`, υποχρεωτικός κωδικός με collation `Greek_CI_AS`. |
| `dbo.Stores.Name` | `nvarchar(160)`, υποχρεωτικό όνομα καταστήματος. |
| `dbo.__EFMigrationsHistory` | Τεχνικό ιστορικό εφαρμοσμένων migrations και έκδοσης EF Core. |

Ο unique index `(TenantId, Code)` αποκλείει διπλό κωδικό μέσα στην ίδια επιχείρηση, χωρίς διάκριση πεζών/κεφαλαίων σύμφωνα με το SQL collation. Ο ίδιος κωδικός επιτρέπεται σε διαφορετικές επιχειρήσεις. Το Domain εξακολουθεί να κανονικοποιεί τους κωδικούς με `ToUpperInvariant` και να επικυρώνει τα πεδία πριν από την αποθήκευση.

Τα foreign keys απορρίπτουν κατάστημα με ανύπαρκτη επιχείρηση. Η SQL εμποδίζει τη διαγραφή επιχείρησης που έχει καταστήματα, χωρίς cascade delete. Check constraints απορρίπτουν κενά ονόματα/κωδικούς από απλά spaces και κενά GUIDs. Η πλήρης επικύρωση χαρακτήρων παραμένει στο Domain.

## 4. Έλεγχοι μετά την εφαρμογή

Επανέλαβε το `--dry-run`: αναμένονται μία εφαρμοσμένη και μηδέν εκκρεμείς migrations. Στο SSMS, στη βάση `AenaonERP_Dev`, εκτέλεσε μόνο τα παρακάτω read-only queries:

```sql
SELECT s.name AS [SchemaName], t.name AS [TableName]
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = N'dbo'
  AND t.name IN (N'Tenants', N'Stores', N'__EFMigrationsHistory')
ORDER BY t.name;

SELECT [MigrationId], [ProductVersion]
FROM dbo.__EFMigrationsHistory
ORDER BY [MigrationId];

SELECT COUNT_BIG(*) AS [TotalTenants] FROM dbo.Tenants;
SELECT COUNT_BIG(*) AS [TotalStores] FROM dbo.Stores;
```

Μετά την πρώτη εφαρμογή και πριν από εισαγωγή πραγματικών δεδομένων αναμένονται τρεις πίνακες, μία εγγραφή ιστορικού και μηδέν επιχειρήσεις/καταστήματα.

Μπορείς κατόπιν να επανεκκινήσεις το API με τη γνωστή εντολή. Τα `/health`, `/health/database` και `/api/info` διατηρούν τη συμπεριφορά τους. Ο έλεγχος `/health/database` παραμένει έλεγχος σύνδεσης, όχι πιστοποίηση ότι το σχήμα είναι ενημερωμένο.

## Παράμετροι, ακύρωση και exit codes

| Επιλογή | Λειτουργία |
| --- | --- |
| `--migrate-database --dry-run` | Εμφανίζει εφαρμοσμένες/εκκρεμείς migrations, χωρίς αλλαγές. |
| `--migrate-database --apply --confirm-database NAME` | Εφαρμόζει τις εκκρεμείς migrations προς τα εμπρός. |
| `--help` | Εμφανίζει τη διαθέσιμη βοήθεια CLI. |
| `Ctrl+C` | Ζητά ακύρωση της λειτουργίας. Μετά την ακύρωση επανέλεγξε με `--dry-run`. |

| Exit code | Σημασία |
| --- | --- |
| `0` | Επιτυχής προεπισκόπηση/εφαρμογή ή ήδη ενημερωμένη βάση. |
| `1` | Σφάλμα SQL ή λήξη χρόνου. |
| `2` | Άκυρα ορίσματα, ρυθμίσεις, επιβεβαίωση ή μη συμβατή βάση. |
| `130` | Ακύρωση από τον καλούντα. |

Το συνολικό χρονικό όριο είναι 60 δευτερόλεπτα, με command timeout 30 δευτερολέπτων για τις migrations. Η ολοκλήρωση ακύρωσης/rollback εξαρτάται και από τον SQL Server. Δεν εκτελείται αυτόματη επανάληψη της δικής μας εντολής μετά από αποτυχία.

## Πότε χρησιμοποιείται και όρια

Χρησιμοποιείται για ελεγχόμενη ενημέρωση της ξεχωριστής βάσης ανάπτυξης με την έκδοση του repository. Απορρίπτονται βάσεις συστήματος, ανύπαρκτες βάσεις, διαφορετικό compatibility level, άγνωστο/ασύμβατο ιστορικό και, στην πρώτη migration, βάσεις με άλλα αντικείμενα χρήστη.

Δεν αποτελεί εργαλείο μετατροπής υπάρχουσας βάσης άλλου ERP, δημιουργίας database ή παραγωγικού deployment. Η παραγωγική διάθεση θα αποκτήσει ξεχωριστή διαδικασία, deployment identity και έλεγχο/backup πριν από τις αλλαγές.

Τα νέα tables αποτελούν υποδομή αποθήκευσης. Δεν προστίθενται ακόμη HTTP endpoints διαχείρισης επιχειρήσεων ή πρόσβαση σε επιχειρησιακά δεδομένα. Η ταυτοποίηση χρηστών, η εξουσιοδότηση και η υποχρεωτική επιλογή επιχείρησης θα προστεθούν πριν από αντίστοιχες λειτουργίες. Ένα foreign key από μόνο του δεν παρέχει απομόνωση πρόσβασης χρηστών.

## Logs, ασφάλεια και ανάκτηση

Τα structured logs καταγράφουν πλήθος migrations, migration IDs, έναρξη/ολοκλήρωση και SQL error number. Δεν καταγράφονται connection strings, credentials ή exception messages. Το EF Core χρησιμοποιείται χωρίς sensitive-data logging και χωρίς μεταφορά του SQL command log στο console logger της εφαρμογής.

Το EF Core διαχειρίζεται transactions και το δικό του migration lock. Δεν περικλείουμε το `MigrateAsync` σε επιπλέον transaction. Αν αποτύχει η αρχική migration, οι αλλαγές μέσα στη transaction της επαναφέρονται· ο τεχνικός πίνακας ιστορικού μπορεί ήδη να έχει δημιουργηθεί. Έλεγξε την κατάσταση πριν επαναλάβεις.

Μετά από επιτυχημένη εφαρμογή, η επιστροφή σε προηγούμενο binary δεν επαναφέρει το σχήμα. Η αρχική `Down` migration διαγράφει τους νέους πίνακες και τα δεδομένα τους: δεν εκτίθεται από το CLI. Για ανάκτηση χρησιμοποιείται ελεγμένο backup ή νέα, ελεγμένη migration προς τα εμπρός. Μην τροποποιείς/διαγράφεις χειροκίνητα γραμμές του ιστορικού.

## Αντιμετώπιση προβλημάτων

| Πρόβλημα | Έλεγχος / ενέργεια |
| --- | --- |
| Exit `2`, ελλιπής σύνδεση | Εκτέλεσε `--check-database` και έλεγξε το προφίλ `https`. |
| Exit `2`, λάθος επιβεβαίωση | Αντέγραψε ακριβώς το όνομα βάσης από την προεπισκόπηση στο `--confirm-database`. |
| Exit `2`, άγνωστα αντικείμενα | Σταμάτησε και έλεγξε ότι επέλεξες τη σωστή ξεχωριστή βάση. Δεν υπάρχει `--force`. |
| Ασύμβατο ιστορικό | Έλεγξε τη σωστή έκδοση κώδικα και το ιστορικό. Μην αφαιρείς εγγραφές για παράκαμψη. |
| Compatibility διαφορετικό από `160` | Έλεγξε τον στόχο/ρυθμίσεις με τον διαχειριστή· το CLI δεν μεταβάλλει database options. |
| SQL `262` / `2760` | Έλεγξε δικαιώματα δημιουργίας tables/constraints στο συγκεκριμένο database/schema. |
| SQL `18456` / `4060` | Έλεγξε SQL login/password και πρόσβαση στην υπάρχουσα βάση. |
| Timeout ή ακύρωση | Επανέλεγξε με `--dry-run` πριν επαναλάβεις· έλεγξε blocking/δικαιώματα στον SQL Server. |
| Healthy αλλά εκκρεμείς migrations | Το health endpoint ελέγχει σύνδεση. Χρησιμοποίησε το CLI για την έκδοση σχήματος. |

## Έλεγχοι ανάπτυξης και συντήρηση

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Τα tests ελέγχουν συγχρονισμό model/snapshot/script και τις προστασίες CLI χωρίς προσωπική βάση. Το απομονωμένο SQL Server 2022 test του CI επαληθεύει προεπισκόπηση χωρίς εγγραφές, migrations, επανάληψη, αποθήκευση/ανάγνωση ελληνικών, σχέσεις, μοναδικότητα κωδικών, αποτροπή ορφανών καταστημάτων και cascade διαγραφής, καθώς και απόρριψη άσχετου/μελλοντικού schema. Τα πλασματικά δεδομένα υπάρχουν αποκλειστικά στο προσωρινό container.

Για παραγωγή/επιθεώρηση migrations από προγραμματιστή:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api
dotnet ef migrations script --idempotent --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api --output database/001-initial-tenancy.sql
```

Το `dotnet-ef` είναι τοπικό εργαλείο, σταθερά ορισμένο σε 10.0.14. Η design-time factory χρησιμοποιεί μη προσβάσιμο placeholder και δεν διαβάζει προσωπικά secrets. Προορίζεται για δημιουργία αρχείων, όχι για `dotnet ef database update/drop`. Οι επόμενες αλλαγές προστίθενται ως νέες migrations· δεν ξαναγράφουμε ήδη εφαρμοσμένη migration ή ιστορικό.

## Checklist ολοκλήρωσης

- [ ] Το locked restore και τα tests ολοκληρώνονται.
- [ ] Η προεπισκόπηση εμφανίζει τη σωστή βάση.
- [ ] Το SQL αρχείο έχει επιθεωρηθεί πριν από την εφαρμογή.
- [ ] Υπάρχει κατάλληλο backup όπου υπάρχουν χρήσιμα δεδομένα.
- [ ] Η εφαρμογή ολοκληρώνεται με exit code `0`.
- [ ] Η δεύτερη προεπισκόπηση εμφανίζει μηδέν εκκρεμείς migrations.
- [ ] Το SSMS εμφανίζει τους τρεις αναμενόμενους πίνακες και το σωστό ιστορικό.
- [ ] Τα credentials παραμένουν εκτός repository/logs.

## Επίσημες πηγές

- [EF Core SQL Server 10.0.14](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/10.0.14)
- [Εφαρμογή migrations και scripts](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Design-time DbContext](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation)
