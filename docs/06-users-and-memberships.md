# Aenaon ERP — Σχήμα χρηστών και συμμετοχών σε επιχειρήσεις

## Σκοπός και σύντομη περιγραφή

Το έκτο βήμα προσθέτει την υποδομή αποθήκευσης λογαριασμών ASP.NET Core Identity και τη σύνδεση κάθε λογαριασμού με τις επιχειρήσεις στις οποίες συμμετέχει. Ο λογαριασμός είναι ξεχωριστός από την επιχείρηση: ο ίδιος χρήστης μπορεί να έχει διαφορετικό ρόλο σε διαφορετικές επιχειρήσεις.

Η νέα migration `20261109110355_IdentityAndTenantMemberships` προσθέτει πέντε πίνακες. Η `Up` περιέχει δημιουργία νέων πινάκων, περιορισμών και indexes, χωρίς μεταβολή/διαγραφή των `Tenants` και `Stores` ή εισαγωγή λογαριασμών. Τα υπάρχοντα επιχειρησιακά IDs και δεδομένα διατηρούνται.

Το βήμα αφορά το σχήμα. Η δημιουργία πρώτου διαχειριστή, η είσοδος/έξοδος, τα sessions και η εφαρμογή εξουσιοδότησης στα αιτήματα θα προστεθούν στα επόμενα βήματα. Η ύπαρξη πινάκων και ρόλων δεν αποτελεί από μόνη της έλεγχο πρόσβασης.

## Προϋποθέσεις και έλεγχοι πριν τη ρύθμιση

- Ολοκληρωμένα τα προηγούμενα βήματα, με επιτυχή σύνδεση SQL.
- Στη συνήθη συνέχεια του έργου έχει εφαρμοστεί η πρώτη migration και υπάρχει η δοκιμαστική επιχείρηση/κατάστημα.
- Σωστό Development `ConnectionStrings:Aenaon`, εκτός repository.
- SQL login με δικαιώματα δημιουργίας tables, foreign keys, check constraints και indexes στο `dbo` της συγκεκριμένης βάσης.
- Σταματημένο API κατά την ενημέρωση/εφαρμογή.
- Πρόσφατο ελεγμένο backup πριν από αλλαγή σχήματος σε βάση με χρήσιμα δεδομένα.

{% hint style="warning" %}
Η εφαρμογή migration μεταβάλλει το σχήμα. Επιθεώρησε την προεπισκόπηση και το SQL πριν από την εκτέλεση και επιβεβαίωσε τη σωστή βάση. Διατήρησε τα υπάρχοντα δεδομένα και το ιστορικό migrations· δεν χρειάζεται νέα δημιουργία επιχείρησης ή επαναφορά της βάσης.
{% endhint %}

## 1. Ενημέρωση του κώδικα

Από τον κεντρικό φάκελο του repository:

```powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet test AenaonERP.slnx --configuration Release --no-restore --filter "TestCategory!=SqlServer"
```

Διατήρησε τυχόν δικές σου αλλαγές πριν από το pull. Τα υπάρχοντα User Secrets και τα δεδομένα της βάσης παραμένουν στον υπολογιστή σου.

## 2. Προεπισκόπηση

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Με ολοκληρωμένα τα προηγούμενα βήματα αναμένεται:

```text
Βάση: AenaonERP_Dev
Εφαρμοσμένες migrations: 1
Εκκρεμείς migrations: 1
  20261109110355_IdentityAndTenantMemberships
Η προεπισκόπηση ολοκληρώθηκε χωρίς αλλαγές στη βάση.
```

Το SQL της αναβάθμισης είναι διαθέσιμο στο [database/002-identity-memberships.sql](../database/002-identity-memberships.sql). Έχει παραχθεί από το επίσημο EF CLI ως idempotent script από την πρώτη στη δεύτερη migration. Η διαδικασία εκτέλεσης παρακάτω χρησιμοποιεί το προστατευμένο CLI του έργου.

Σε εντελώς νέα βάση η προεπισκόπηση εμφανίζει δύο εκκρεμείς migrations. Το `--apply` εφαρμόζει όλες τις εκκρεμείς, με τη σωστή σειρά. Το δεύτερο SQL αρχείο μόνο του προϋποθέτει ότι έχει προηγηθεί η πρώτη migration.

## 3. Εφαρμογή

Αφού επιβεβαιώσεις τον στόχο, το backup όπου απαιτείται και τις εκκρεμείς αλλαγές:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Αναμενόμενο αποτέλεσμα:

```text
Βάση: AenaonERP_Dev
Εφαρμοσμένες migrations: 2
Εκκρεμείς migrations: 0
Οι migrations εφαρμόστηκαν επιτυχώς.
```

Επανέλαβε το `--dry-run` για επιβεβαίωση μηδενικών εκκρεμοτήτων. Το `--confirm-database` απαιτεί ακριβώς το όνομα της ρυθμισμένης βάσης, με ίδια πεζά/κεφαλαία. Η εντολή παραμένει διαθέσιμη μόνο στο Development.

## Πίνακες και βασικά πεδία

| Πίνακας | Περιεχόμενο |
| --- | --- |
| `dbo.AspNetUsers` | Λογαριασμοί με GUID, username, κανονικοποιημένο username/email, password hash, stamps, πεδία επιβεβαίωσης και lockout. |
| `dbo.AspNetUserClaims` | Claims λογαριασμού, με foreign key προς χρήστη. |
| `dbo.AspNetUserLogins` | Υποδομή αντιστοίχισης εξωτερικών logins του Identity. |
| `dbo.AspNetUserTokens` | Υποδομή αποθήκευσης tokens ανά χρήστη/provider/όνομα. |
| `dbo.TenantMemberships` | Σύνδεση `(TenantId, UserId)` με ρόλο στη συγκεκριμένη επιχείρηση. |

Ο συνολικός αριθμός πινάκων του έργου γίνεται οκτώ: οι πέντε νέοι, οι `Tenants`/`Stores` και το `__EFMigrationsHistory`. Μετά την εφαρμογή οι νέοι πίνακες είναι κενοί. Δεν δημιουργείται αυτόματα διαχειριστής, email ή αρχικός κωδικός.

| Πεδίο / κανόνας | Λειτουργία |
| --- | --- |
| `AspNetUsers.Id` | GUID του λογαριασμού, με απόρριψη κενού GUID. |
| `UserName` / `NormalizedUserName` | Υποχρεωτικά, έως 256 χαρακτήρες· unique index στο κανονικοποιημένο username. |
| `Email` / `NormalizedEmail` | Προαιρετικά στο σχήμα· unique filtered index όταν υπάρχει κανονικοποιημένο email. |
| `PasswordHash` | Πεδίο του Identity για hash, όχι αποθήκευση plaintext password. |
| `SecurityStamp` / `ConcurrencyStamp` | Τεχνικά πεδία Identity για ασφάλεια/συνέπεια μελλοντικών ροών λογαριασμών. |
| `TenantMemberships.Role = 1` | `Owner`, μόνο για την επιχείρηση της συγκεκριμένης εγγραφής. |
| `TenantMemberships.Role = 2` | `Member`, η προεπιλογή της factory του Domain. |

Ο σύνθετος primary key αποκλείει διπλή συμμετοχή του ίδιου χρήστη στην ίδια επιχείρηση. Τα foreign keys απορρίπτουν ανύπαρκτο χρήστη/επιχείρηση. Check constraint απορρίπτει άγνωστους ρόλους. Η συμμετοχή είναι ανεξάρτητη ανά επιχείρηση: `Owner` στην Α δεν συνεπάγεται `Owner` στη Β.

Δεν χρησιμοποιούνται global Identity roles για επιχειρησιακά δικαιώματα. Οι ροές που θα προστεθούν οφείλουν να επαληθεύουν τη συμμετοχή και τον ρόλο στη στοχευμένη επιχείρηση, μαζί με την πρόσβαση στο κατάστημα όπου απαιτείται. Τα claims λογαριασμού από μόνα τους δεν απονέμουν tenant-wide δικαιώματα.

## 4. Έλεγχοι μετά την εφαρμογή

Στο SSMS, στη σωστή βάση, εκτέλεσε read-only queries:

```sql
SELECT [MigrationId], [ProductVersion]
FROM dbo.__EFMigrationsHistory
ORDER BY [MigrationId];

SELECT t.Id AS [TenantId], t.Name AS [TenantName],
       s.Id AS [StoreId], s.Code AS [StoreCode], s.Name AS [StoreName]
FROM dbo.Tenants AS t
JOIN dbo.Stores AS s ON s.TenantId = t.Id;

SELECT COUNT_BIG(*) AS [TotalUsers] FROM dbo.AspNetUsers;
SELECT COUNT_BIG(*) AS [TotalMemberships] FROM dbo.TenantMemberships;
```

Αναμένονται δύο γραμμές ιστορικού, ίδια επιχείρηση/κατάστημα με τα προηγούμενα IDs και μηδέν χρήστες/συμμετοχές πριν από το επόμενο βήμα. Μην προσθέτεις χρήστες με χειροκίνητο `INSERT` και μην ορίζεις plaintext τιμές στο `PasswordHash`.

Η γνωστή εκκίνηση API και οι διαδρομές `/health`, `/health/database`, `/api/info` διατηρούν τη συμπεριφορά τους. Το health της βάσης ελέγχει σύνδεση και compatibility level· την έκδοση σχήματος την ελέγχει το migration CLI.

## CLI, ακύρωση και logs

Οι εντολές, τα χρονικά όρια και τα exit codes του οδηγού migrations παραμένουν ίδια: `0` επιτυχία, `1` SQL σφάλμα/timeout, `2` ελλιπείς ή μη αποδεκτές ρυθμίσεις/βάση, `130` ακύρωση. Το `Ctrl+C` ζητά ακύρωση· έλεγξε την τελική κατάσταση με `--dry-run` πριν επαναλάβεις.

Τα logs του migration manager καταγράφουν migration IDs, πλήθη και SQL error numbers. Δεν εκτυπώνουν connection strings, passwords ή hash/token values. Μην εξάγεις ολόκληρες εγγραφές Identity σε tickets ή logs. Για διάγνωση αυτού του βήματος αρκούν το αποτέλεσμα CLI και οι αριθμοί σφαλμάτων.

## Ανάκτηση, διαγραφές και deployment

Η migration εκτελείται μέσω του EF migration manager με transaction και migration lock. Σε σφάλμα έλεγξε το ιστορικό και την κατάσταση πριν επαναλάβεις. Διατήρησε ελεγμένο backup για ανάκτηση από αποτυχία ή λανθασμένο deployment.

Η `Down` της δεύτερης migration αφαιρεί τους νέους πίνακες και όλα τα identity/membership δεδομένα τους. Δεν εκτίθεται από το CLI και δεν πρέπει να χρησιμοποιείται ως αυτόματη επαναφορά. Μετά από επιτυχημένη εφαρμογή προτίμησε ελεγχόμενη migration προς τα εμπρός ή διαδικασία restore από backup. Η επιστροφή σε παλαιότερο binary δεν επαναφέρει το σχήμα και ο έλεγχος ιστορικού απορρίπτει άγνωστες migrations.

Η SQL εμποδίζει διαγραφή χρήστη ή επιχείρησης που έχει memberships. Τα τεχνικά child records του Identity χρησιμοποιούν τις κανονικές cascade σχέσεις του framework. Οι μελλοντικές διαδικασίες απενεργοποίησης, ανάκλησης πρόσβασης και διαγραφής θα απαιτούν ξεχωριστούς ελέγχους εφαρμογής, ειδικά για ιδιοκτήτες.

Το στάδιο δεν περιλαμβάνει production hosting, persistence κλειδιών Data Protection, sessions ή endpoints εγγραφής/σύνδεσης. Δεν εκθέτει νέα επιχειρησιακά δεδομένα μέσω HTTP. Η παραγωγική διάθεση θα απαιτήσει χωριστά identities για migrations και runtime, ασφαλή configuration και ελεγμένη πολιτική πρόσβασης.

## Αντιμετώπιση προβλημάτων

| Πρόβλημα | Έλεγχος / ενέργεια |
| --- | --- |
| Εμφανίζονται μηδέν εκκρεμείς migrations πριν από την αναβάθμιση | Επιβεβαίωσε pull, branch και σωστό project/φάκελο. |
| Εμφανίζονται δύο εκκρεμείς | Η βάση δεν έχει ακόμη την πρώτη migration. Έλεγξε τον στόχο πριν από την εφαρμογή. |
| SQL `2714` | Υπάρχει ήδη αντικείμενο με ίδιο όνομα χωρίς το αναμενόμενο ιστορικό· έλεγξε τη βάση, χωρίς χειροκίνητη διαγραφή. |
| SQL `262` / `2760` | Έλεγξε δικαιώματα DDL στο συγκεκριμένο database/schema. |
| Ασύμβατο ιστορικό | Έλεγξε την έκδοση κώδικα και τον στόχο. Μην αφαιρείς γραμμές ιστορικού για παράκαμψη. |
| Timeout / ακύρωση | Επανέλεγξε με `--dry-run` και έλεγξε blocking/δικαιώματα πριν επαναλάβεις. |
| Διαφορετικά business IDs από πριν | Σταμάτησε και επιβεβαίωσε τον στόχο/backup. Η migration δεν προορίζεται να αλλάζει τις υπάρχουσες εγγραφές. |

## Έλεγχοι ανάπτυξης και συντήρηση

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api
```

Το Domain παραμένει χωρίς εξάρτηση από Identity/EF. Το Infrastructure χρησιμοποιεί το επίσημο `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14, με σταθερές εκδόσεις και ενεργό NuGet audit. Το υπάρχον πρώτο migration/script διατηρείται. Για επιθεώρηση του δεύτερου:

```powershell
dotnet ef migrations script 20261109100218_InitialTenancy 20261109110355_IdentityAndTenantMemberships --idempotent --project src/Aenaon.Infrastructure --startup-project src/Aenaon.Api --output database/002-identity-memberships.sql
```

Τα tests ελέγχουν το Domain, τον συγχρονισμό model/snapshot και κάθε SQL artifact με τη δική του migration. Το SQL CI δημιουργεί απομονωμένη `AenaonERP_Identity_CI`, εφαρμόζει πρώτα το προηγούμενο schema με επιχειρησιακά δεδομένα και κατόπιν τη νέα migration. Ελέγχει διατήρηση IDs/ονόματος/κωδικού, Identity user-store με hash, μοναδικότητα usernames/emails, διαφορετικούς ρόλους του ίδιου χρήστη ανά επιχείρηση, foreign keys και προστασία διαγραφής. Τα πλασματικά accounts παραμένουν αποκλειστικά στο προσωρινό container.

## Checklist ολοκλήρωσης

- [ ] Το locked restore, build και tests ολοκληρώνονται.
- [ ] Η προεπισκόπηση εμφανίζει τη σωστή βάση και τη δεύτερη migration.
- [ ] Το SQL και το διαθέσιμο backup έχουν ελεγχθεί.
- [ ] Η εφαρμογή επιστρέφει exit code `0`.
- [ ] Το ιστορικό έχει δύο εφαρμοσμένες migrations και καμία εκκρεμή.
- [ ] Η επιχείρηση/κατάστημα έχουν ακριβώς τα προηγούμενα IDs και στοιχεία.
- [ ] Οι νέοι πίνακες υπάρχουν και παραμένουν κενοί πριν από δημιουργία διαχειριστή.
- [ ] Credentials, hashes και token values δεν έχουν ανέβει στο repository/logs.

## Επίσημες πηγές

- [ASP.NET Core Identity EF package](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore/10.0.14)
- [Προσαρμογή Identity model](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)
- [Εφαρμογή migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
