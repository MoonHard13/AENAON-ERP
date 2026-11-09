# Aenaon ERP — Πρώτη εκκίνηση του API

## Σκοπός

Λήψη του κώδικα από το GitHub, έλεγχος build και εκκίνηση της πρώτης βάσης του API στον υπολογιστή ανάπτυξης.

## Προϋποθέσεις

- Git διαθέσιμο από το τερματικό.
- .NET SDK 10.0.403 ή μεταγενέστερο σταθερό patch της σειράς 10.0.4xx.
- VS Code ή άλλο πρόγραμμα επεξεργασίας C#.
- Πρόσβαση στο NuGet.org κατά το πρώτο restore.

```powershell
git --version
dotnet --info
```

Για το αρχικό API δεν απαιτούνται Android ή MAUI workloads. Αυτά θα εξεταστούν μαζί με την εφαρμογή APK.

## Λήψη του έργου

Άνοιξε PowerShell στον φάκελο όπου θέλεις να αποθηκεύσεις τον κώδικα και εκτέλεσε:

```powershell
git clone https://github.com/MoonHard13/AENAON-ERP.git
cd AENAON-ERP
code .
```

Στο VS Code επίλεξε **Terminal → New Terminal**. Ο τρέχων φάκελος πρέπει να περιέχει το `AenaonERP.slnx` και το `global.json`.

## Προετοιμασία HTTPS

```powershell
dotnet dev-certs https --trust
```

Αν εμφανιστεί παράθυρο επιβεβαίωσης των Windows, επιβεβαίωσε την εμπιστοσύνη του τοπικού πιστοποιητικού ανάπτυξης. Το πιστοποιητικό αυτό αφορά το περιβάλλον ανάπτυξης του υπολογιστή σου.

## Restore, build και δοκιμές

Εκτέλεσε διαδοχικά, ελέγχοντας ότι κάθε εντολή ολοκληρώνεται επιτυχώς:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore
```

Το `--locked-mode` χρησιμοποιεί τις καταγεγραμμένες εκδόσεις των `packages.lock.json`. Αλλαγή εξαρτήσεων γίνεται συνειδητά με νέο restore και έλεγχο των διαφορών. Το restore εκτελεί και NuGet security audit· τα warnings αντιμετωπίζονται ως σφάλματα.

Τα integration tests εκκινούν απομονωμένο HTTP test server. Δεν απαιτούν βάση δεδομένων, πιστοποιητικό HTTPS ή υπηρεσίες παραγωγής. Καλύπτουν το health check, την πληροφορία εφαρμογής, άγνωστη διαδρομή, μη επιτρεπτή μέθοδο και ανακατεύθυνση HTTP σε HTTPS. Τα unit tests του Domain ελέγχουν τους κανόνες επιχειρήσεων και καταστημάτων χωρίς εξωτερικές υπηρεσίες.

## Εκκίνηση

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https
```

Αναμενόμενες διευθύνσεις:

| Διεύθυνση | Συμπεριφορά |
| --- | --- |
| `https://localhost:7140/health` | Επιστρέφει `Healthy`. |
| `https://localhost:7140/api/info` | Επιστρέφει το όνομα `Aenaon ERP API` σε JSON. |
| `http://localhost:5180/api/info` | Ανακατευθύνει στο HTTPS endpoint. |

Η βασική διεύθυνση `/` δεν διαθέτει σελίδα χρήστη σε αυτό το στάδιο. Χρησιμοποίησε τις συγκεκριμένες διαδρομές του πίνακα.

Με το API σε λειτουργία, μπορείς να ελέγξεις από δεύτερο τερματικό:

```powershell
Invoke-RestMethod https://localhost:7140/health
Invoke-RestMethod https://localhost:7140/api/info
```

Για διακοπή πάτησε **Ctrl+C** στο τερματικό που εκτελεί το API.

## Ρυθμίσεις και logs

- Το `global.json` επιλέγει το SDK. Δεν επιτρέπονται preview εκδόσεις.
- Το `appsettings.json` ορίζει τα επίπεδα logging.
- Το `Properties/launchSettings.json` ορίζει τις τοπικές θύρες και το περιβάλλον `Development`.
- Τα logs εκτυπώνονται στο τερματικό ως JSON, με ώρα UTC, HTTP status, διάρκεια και trace ID. Τα αρχεία και η διατήρηση logs θα ρυθμιστούν στο επίπεδο φιλοξενίας.
- Στα αιτήματα καταγράφονται μέθοδος, κατάσταση και διάρκεια. Δεν καταγράφονται από τον δικό μας middleware σώματα, headers, query strings ή κωδικοί.

{% hint style="warning" %}
Αυτό είναι περιβάλλον ανάπτυξης και η πρώτη βάση του API. Δεν περιλαμβάνει ακόμη λογαριασμούς, απομόνωση πραγματικών επιχειρησιακών δεδομένων ή φορολογική έκδοση. Μην εισάγεις δεδομένα παραγωγής. Η παραγωγική φιλοξενία και η ρύθμιση reverse proxy θα υλοποιηθούν σε επόμενο στάδιο.
{% endhint %}

## Έλεγχος εξαρτήσεων

```powershell
dotnet list AenaonERP.slnx package --outdated
dotnet list AenaonERP.slnx package --vulnerable --include-transitive
```

Οι άμεσες εξαρτήσεις δοκιμών είναι τα επίσημα `MSTest` και `Microsoft.AspNetCore.Mvc.Testing`. Οι εκδόσεις τους καθορίζονται κεντρικά στο `Directory.Packages.props`. Το API δεν προσθέτει εξωτερικό package παραγωγής.

## Publish για έλεγχο του πακέτου

```powershell
dotnet publish src/Aenaon.Api --configuration Release --no-restore --self-contained false --output artifacts/api
```

Το αποτέλεσμα είναι framework-dependent και χρειάζεται συμβατό ASP.NET Core 10 runtime στον προορισμό. Το publish δεν εγκαθιστά και δεν δημοσιεύει την εφαρμογή σε cloud. Η πραγματική φιλοξενία, το HTTPS παραγωγής και η διαδικασία rollback θα καθοριστούν στο στάδιο deployment.

## Ενημέρωση του τοπικού κώδικα

Σταμάτησε το API με `Ctrl+C` και έλεγξε την κατάσταση:

```powershell
git status
```

Αν δεν υπάρχουν τοπικές αλλαγές που πρέπει να διατηρηθούν:

```powershell
git pull --ff-only
dotnet restore AenaonERP.slnx --locked-mode
dotnet test AenaonERP.slnx --configuration Release
```

Κράτησε τις δικές σου αλλαγές σε commit ή ξεχωριστό branch πριν ενημερώσεις τον κώδικα.

## Αντιμετώπιση προβλημάτων

| Πρόβλημα | Ενέργεια |
| --- | --- |
| Το `git` δεν αναγνωρίζεται | Εγκατάστησε Git και άνοιξε νέο τερματικό. |
| Δεν βρίσκεται το SDK | Έλεγξε `dotnet --list-sdks` και την έκδοση του `global.json`. |
| Προειδοποίηση πιστοποιητικού | Εκτέλεσε `dotnet dev-certs https --trust` και επανεκκίνησε τον browser. |
| Η θύρα 7140 ή 5180 χρησιμοποιείται | Σταμάτησε προηγούμενη εκτέλεση ή άλλαξε τις θύρες στο `launchSettings.json`. |
| Σφάλμα restore | Έλεγξε πρόσβαση στο NuGet.org, proxy και το ακριβές σφάλμα· μην απενεργοποιείς τον έλεγχο πιστοποιητικών. |
| `404` στη διεύθυνση `/` | Χρησιμοποίησε `/health` ή `/api/info`. |
| Το test αναφέρει ότι λείπει build | Εκτέλεσε πρώτα το Release build ή αφαίρεσε το `--no-build`. |
| Ασυμφωνία lock file | Έλεγξε πρώτα τις αλλαγές στα project files και στο `Directory.Packages.props` πριν ανανεώσεις το lock file. |

## Έλεγχος ολοκλήρωσης

- [ ] Το restore ολοκληρώνεται χωρίς σφάλματα.
- [ ] Το Release build ολοκληρώνεται χωρίς warnings ή errors.
- [ ] Τα πέντε integration tests του API και τα unit tests του Domain περνούν.
- [ ] Το HTTPS `/health` επιστρέφει `Healthy`.
- [ ] Το `/api/info` εμφανίζει το όνομα της εφαρμογής.
- [ ] Το API σταματά με `Ctrl+C`.

## Επίσημη τεκμηρίωση

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [Πιστοποιητικά ανάπτυξης](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs)
- [Integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
