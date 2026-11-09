# Aenaon ERP — Σύνδεση SQL Server

## Σκοπός

Σύνδεση της εφαρμογής με ξεχωριστή βάση ανάπτυξης και επαλήθευση της πρόσβασης μέσω browser ή CLI. Αυτό το βήμα ελέγχει τη σύνδεση και το compatibility level· τα tables και τα migrations των επιχειρησιακών μοντέλων θα προστεθούν στη συνέχεια.

## Προϋποθέσεις και έλεγχοι πριν τη ρύθμιση

- Εγκατεστημένα .NET SDK και Git, όπως στο πρώτο βήμα.
- SQL Server 2022 με online βάση `AenaonERP_Dev`, compatibility level 160.
- SQL login που έχει πρόσβαση στη συγκεκριμένη βάση.
- Επιβεβαιωμένη σύνδεση στο ίδιο instance από SSMS.
- Σταματημένο API πριν από την ενημέρωση του τοπικού κώδικα.

Το SQL login αφορά τη σύνδεση με τη βάση. Οι λογαριασμοί χρηστών του Aenaon ERP θα δημιουργηθούν σε επόμενο στάδιο.

## Ενημέρωση κώδικα

Στο τερματικό που τρέχει το API πάτησε `Ctrl+C`. Από τον κεντρικό φάκελο του repository:

```powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet test AenaonERP.slnx --configuration Release --no-restore --filter "TestCategory!=SqlServer"
```

Αν έχεις δικές σου αλλαγές, διατήρησέ τες σε commit ή ξεχωριστό branch πριν από το pull.

## Ρύθμιση SQL Authentication

Χρησιμοποίησε το πραγματικό όνομα του δικού σου instance στη θέση του παραδείγματος:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\Set-DevelopmentDatabase.ps1 -Server 'localhost\SQL2022' -Database 'AenaonERP_Dev' -TrustServerCertificate
```

Το script ζητά διαδοχικά:

1. **SQL login:** το όνομα σύνδεσης που χρησιμοποιείς στο SSMS.
2. **Κωδικό SQL login:** εισάγεται ως SecureString τοπικά, με απόκρυψη στην οθόνη.

Το connection string παράγεται με `DbConnectionStringBuilder`, ώστε να διατηρούνται σωστά ειδικοί χαρακτήρες, εισαγωγικά και ελληνικά στον κωδικό. Μεταφέρεται στο επίσημο `dotnet user-secrets set` μέσω stdin ως JSON. Δεν περνά ως argument στη γραμμή εντολών ούτε γράφεται στο project.

Το `RemoteSigned` ισχύει για τη συγκεκριμένη διεργασία PowerShell, χωρίς μόνιμη αλλαγή της πολιτικής του υπολογιστή. Αν εταιρική Group Policy αποκλείει την εκτέλεση, ακολούθησε τη διαδικασία του διαχειριστή σου· το script δεν παρακάμπτει την Group Policy.

{% hint style="info" %}
Τα User Secrets είναι μηχανισμός ανάπτυξης εκτός repository και δεν κρυπτογραφούνται από το .NET. Χρησιμοποίησε λογαριασμό/βάση ανάπτυξης. Τα πραγματικά production secrets θα αποθηκεύονται στον ασφαλή μηχανισμό της πλατφόρμας φιλοξενίας.
{% endhint %}

## Παράμετροι του script

| Παράμετρος | Περιγραφή |
| --- | --- |
| `-Server` | Υποχρεωτικό instance, π.χ. `localhost\SQL2022` ή έγκυρη TCP διεύθυνση. |
| `-Database` | Βάση εφαρμογής, προεπιλογή `AenaonERP_Dev`. |
| `-TrustServerCertificate` | Επιτρέπει το τοπικό πιστοποιητικό SQL χωρίς έλεγχο αλυσίδας στο Development. Η κρυπτογράφηση παραμένει ενεργή. |

Όταν ο SQL Server διαθέτει πιστοποιητικό με έγκυρη αλυσίδα και όνομα server, παράλειψε το `-TrustServerCertificate`. Εκτός περιβάλλοντος `Development`, η εφαρμογή απορρίπτει connection strings που το ενεργοποιούν.

Το κλειδί ρύθμισης είναι `ConnectionStrings:Aenaon`. Για παραγωγική φιλοξενία μπορεί να δοθεί μέσω του αντίστοιχου ασφαλούς configuration provider ή της ρύθμισης περιβάλλοντος `ConnectionStrings__Aenaon`.

## Έλεγχος από CLI

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-database
```

Η εντολή φορτώνει το περιβάλλον Development και τα User Secrets, εκτελεί τον έλεγχο και τερματίζει χωρίς να ανοίγει HTTP server. Για επιτυχημένη σύνδεση σε SQL Server 2022 αναμένεται μήνυμα με `Compatibility level: 160`.

```powershell
$LASTEXITCODE
```

| Exit code | Σημασία |
| --- | --- |
| `0` | Επιτυχής σύνδεση και ανάγνωση metadata. |
| `1` | Αποτυχία ή λήξη χρόνου σύνδεσης. |
| `2` | Ελλιπείς ή μη επιτρεπτές ρυθμίσεις. |
| `130` | Ακύρωση από τον χρήστη με `Ctrl+C`. |

Βοήθεια:

```powershell
dotnet run --project src/Aenaon.Api -- --help
```

## Έλεγχος από browser

Εκκίνησε το API:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https
```

| URL | Έλεγχος |
| --- | --- |
| `https://localhost:7140/health` | Διαθεσιμότητα της διεργασίας API, ανεξάρτητα από SQL. |
| `https://localhost:7140/health/database` | Πραγματική σύνδεση SQL στο Development. |

Ο έλεγχος βάσης επιστρέφει `200 Healthy` σε επιτυχία ή `503 Unhealthy` σε αποτυχία. Το HTTP αποτέλεσμα δεν περιέχει connection string, SQL login, password ή κείμενο SQL exception. Η διαδρομή βάσης επιστρέφει `404` εκτός Development, όσο δεν υπάρχει ακόμη σύστημα εξουσιοδότησης για διαγνωστικές λειτουργίες.

## Τι κάνει ο έλεγχος SQL

Η κοινή υπηρεσία `SqlServerConnectionProbe` χρησιμοποιείται και από CLI και από health check. Ανοίγει σύνδεση στη βάση εφαρμογής και εκτελεί σταθερό ερώτημα ανάγνωσης:

```sql
SELECT CONVERT(int, compatibility_level)
FROM sys.databases
WHERE database_id = DB_ID();
```

Δεν εκτελεί migrations, `CREATE`, `ALTER`, `INSERT`, `UPDATE` ή `DELETE`. Η επιτυχία επιβεβαιώνει πρόσβαση και ανάγνωση metadata, όχι ύπαρξη μελλοντικών πινάκων ή δικαιώματα εγγραφής.

Οι συνδέσεις/commands αποδεσμεύονται με `await using`. Ο χρόνος ανοίγματος περιορίζεται στα 5 δευτερόλεπτα, το command timeout στα 3 και ο συνολικός έλεγχος έχει cancellation budget 8 δευτερολέπτων. Η ακύρωση του καλούντος μεταφέρεται στον driver. Δεν γίνονται αυτόματες επαναλήψεις σύνδεσης από τον δικό μας κώδικα.

## Ασφάλεια και logs

- Υποστηρίζονται SQL Authentication και Windows Integrated Security.
- Το `Encrypt=False` απορρίπτεται. Το `Persist Security Info` εξαναγκάζεται σε `False`.
- Οι βάσεις συστήματος `master`, `model`, `msdb` και `tempdb` απορρίπτονται ως στόχοι του application probe.
- Τα logs περιέχουν αποτέλεσμα, compatibility level ή αριθμό SQL error. Δεν λαμβάνουν το exception message ή τα στοιχεία σύνδεσης.
- Η μεταβατική εξάρτηση `System.Security.Cryptography.Pkcs` ορίζεται ρητά σε συμβατή έκδοση 10.0.14 για να αποφευχθεί η ευάλωτη έκδοση που ζητά ο SQL driver.
- Ο έλεγχος NuGet audit παραμένει ενεργός και τα warnings αντιμετωπίζονται ως errors.

## Δοκιμές και CI

Χωρίς SQL server:

```powershell
dotnet test AenaonERP.slnx --configuration Release --no-restore --filter "TestCategory!=SqlServer"
```

Το CI εκτελεί τους ελέγχους σε Windows και Linux. Σε Linux δημιουργεί επιπλέον προσωρινό SQL Server 2022 container με τυχαίο password, δημοσιευμένο μόνο στη διεύθυνση loopback του runner. Ο μοναδικός live SQL έλεγχος δημιουργεί τη δική του βάση `AenaonERP_CI`, ελέγχει επιτυχημένη σύνδεση/compatibility level και αποτυχία login χωρίς διαρροή password στα logs. Το container αφαιρείται στο τέλος.

Αυτό το test απαιτεί ρητές μεταβλητές CI και δέχεται αποκλειστικά `127.0.0.1,14333` ως server αρχικής σύνδεσης. Δεν εκτελείται στην προσωπική βάση του χρήστη. Αν τρέξεις όλα τα tests χωρίς φίλτρο, ο live έλεγχος θα εμφανιστεί ως skipped/inconclusive εκτός CI.

Σε Windows PowerShell το CI ελέγχει και το script User Secrets, με πλασματικά διαπιστευτήρια που περιέχουν ειδικούς χαρακτήρες και ελληνικά.

## Αντιμετώπιση προβλημάτων

| Πρόβλημα | Έλεγχος / ενέργεια |
| --- | --- |
| `NotConfigured` / exit `2` | Επανέλαβε τη ρύθμιση User Secrets από τον σωστό φάκελο και τρέξε με `--launch-profile https`, ώστε να φορτώνεται το Development. |
| Μη έγκυρη ρύθμιση / exit `2` | Έλεγξε server, όνομα βάσης, υποχρεωτική κρυπτογράφηση και certificate settings. |
| SQL error `18456` | Έλεγξε το SQL login/password και αν επιτρέπεται SQL Authentication. |
| SQL error `4060` | Έλεγξε την ύπαρξη της βάσης και την πρόσβαση του SQL login σε αυτή. |
| Timeout / error `-1` | Επιβεβαίωσε ότι λειτουργεί το SQL instance και ότι ο server/instance είναι σωστός. Για TCP συνδέσεις έλεγξε τις αντίστοιχες ρυθμίσεις δικτύου/port. |
| Πρόβλημα πιστοποιητικού | Για το τοπικό Development επανέλαβε τη ρύθμιση με `-TrustServerCertificate`. Στο production απαιτείται έγκυρο πιστοποιητικό και έλεγχος αλυσίδας/ονόματος. |
| `/health` Healthy, βάση Unhealthy | Το API λειτουργεί αλλά η SQL σύνδεση δεν έχει επαληθευτεί. Τρέξε `--check-database` και έλεγξε τον αριθμό σφάλματος. |
| `/health/database` επιστρέφει `404` | Η διαδρομή διατίθεται μόνο στο Development. Χρησιμοποίησε το προφίλ `https` τοπικά. |
| Το script αποκλείεται από εταιρική πολιτική | Ακολούθησε τη διαδικασία του διαχειριστή για εκτέλεση τοπικών scripts. Μην αλλάζεις την Group Policy. |

Για αλλαγή διαπιστευτηρίων επανεκτέλεσε το script και επανεκκίνησε το API, ώστε να φορτώσει τη νέα σύνδεση. Για αφαίρεση μόνο αυτής της ρύθμισης:

```powershell
dotnet user-secrets remove 'ConnectionStrings:Aenaon' --project src/Aenaon.Api
```

Μην στέλνεις έξοδο από `dotnet user-secrets list`, επειδή περιλαμβάνει τις τιμές των secrets. Για διάγνωση αρκούν το generic μήνυμα CLI, ο SQL error number και το trace ID.

## Έλεγχος ολοκλήρωσης

- [ ] Το pull και το locked restore ολοκληρώνονται.
- [ ] Τα tests χωρίς εξωτερικό SQL περνούν.
- [ ] Τα User Secrets ορίζονται τοπικά χωρίς κωδικό στη γραμμή εντολών.
- [ ] Το CLI επιστρέφει exit code `0` και compatibility level `160`.
- [ ] Το `/health/database` επιστρέφει `Healthy` στο Development.
- [ ] Ο SQL κωδικός δεν υπάρχει σε αρχεία του repository ή σε logs.

## Επίσημες πηγές

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/7.1.1)
- [User Secrets και stdin](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [SQL Server containers](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker)
- [Διορθωμένη κρυπτογραφική εξάρτηση](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.14)
