# Aenaon ERP

Cloud σύστημα εστίασης για πολλές ανεξάρτητες επιχειρήσεις, με POS, Back office και υποστήριξη υπηρεσιών.

## Τρέχον στάδιο

**Βήμα 8.3 — Secure HTTP Sessions (Development).** HTTPS cookie-based login/logout, CSRF, rate limiting, session validation και tenant-aware access check. Τα authentication endpoints είναι σκόπιμα διαθέσιμα μόνο σε Development μέχρι να ολοκληρωθεί η παραγωγική υποδομή.

Κάθε κατάστημα έχει συγκεκριμένο ιδιοκτήτη (`TenantId`). Ο κωδικός καταστήματος είναι μοναδικός μέσα στην ίδια επιχείρηση· διαφορετικές επιχειρήσεις μπορούν να έχουν τον ίδιο κωδικό. Η συλλογή καταστημάτων τροποποιείται μέσα από τους κανόνες της επιχείρησης.

| Διαδρομή | Απάντηση |
| --- | --- |
| `GET /health` | `Healthy`, όταν το API ανταποκρίνεται. |
| `GET /api/info` | `{"name":"Aenaon ERP API"}`. |
| `GET /health/database` | `Healthy` ή `Unhealthy`, αποκλειστικά σε Development, για την πραγματική σύνδεση SQL. |

Το `/health` σε αυτό το στάδιο ελέγχει τη διαθεσιμότητα του API. Δεν ελέγχει βάση δεδομένων, πάροχο ή συγχρονισμό.

Ο έλεγχος `/health/database` ανοίγει τη ρυθμισμένη σύνδεση και διαβάζει το compatibility level. Δεν δημιουργεί ή τροποποιεί πίνακες/δεδομένα και δεν αποτελεί έλεγχο ολοκλήρωσης migrations.

## Γρήγορη εκκίνηση σε Windows / PowerShell

Προϋποθέσεις: Git και .NET SDK **10.0.403** ή νεότερο σταθερό patch της ίδιας σειράς `10.0.4xx`.

```powershell
git clone https://github.com/MoonHard13/AENAON-ERP.git
cd AENAON-ERP
dotnet dev-certs https --trust
dotnet restore AenaonERP.slnx --locked-mode
dotnet run --project src/Aenaon.Api --launch-profile https
```

Άνοιξε το [https://localhost:7140/health](https://localhost:7140/health). Αναμενόμενη απάντηση: `Healthy`. Η διακοπή του API γίνεται με `Ctrl+C` στο τερματικό.

## Έλεγχοι

Από τον κεντρικό φάκελο του repository:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet list AenaonERP.slnx package --outdated
dotnet list AenaonERP.slnx package --vulnerable --include-transitive
```

Οι ίδιες εντολές build και test εκτελούνται από το GitHub Actions σε Windows και Linux. Η επιτυχία κάθε εκτέλεσης φαίνεται στην καρτέλα **Actions**.

Το CI ελέγχει επιπλέον πραγματικό SQL Server 2022 σε απομονωμένο container Linux και τη ρύθμιση User Secrets μέσω του .NET SDK σε Windows και Linux. Δεν χρησιμοποιεί credentials ή βάσεις χρηστών.

## Ρύθμιση SQL σε περιβάλλον ανάπτυξης

Με υπάρχουσα ξεχωριστή βάση `AenaonERP_Dev`, από PowerShell στον κεντρικό φάκελο:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --configure-database --server 'localhost\SQL2022' --trust-server-certificate
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-database
```

Η πρώτη εντολή ζητά το SQL login και τον κωδικό τοπικά. Ο κωδικός δεν περιλαμβάνεται στη γραμμή εντολών. Το `--trust-server-certificate` επιτρέπεται από την εφαρμογή μόνο σε Development και διατηρεί την κρυπτογράφηση ενεργή.

Πλήρης οδηγός: [Σύνδεση SQL Server](docs/03-sql-server-connection.md).

## Πίνακες επιχειρήσεων και καταστημάτων

Πρώτα έλεγξε τον στόχο και την εκκρεμή migration:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Μετά την επιθεώρηση του SQL και την επιβεβαίωση της βάσης:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Η εφαρμογή προσθέτει `dbo.Tenants`, `dbo.Stores` και το ιστορικό migrations, χωρίς δοκιμαστικά δεδομένα. Το CLI επιτρέπει τη λειτουργία μόνο στο Development και ελέγχει τη βάση πριν από την αρχική migration. Η εκκίνηση του API δεν εφαρμόζει migrations αυτόματα.

Πλήρης οδηγός και ανάκτηση: [Πίνακες επιχειρήσεων και καταστημάτων](docs/04-tenancy-database.md).

## Αρχική δοκιμαστική επιχείρηση

Σε ενημερωμένη βάση χωρίς επιχειρήσεις/καταστήματα:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-business --dry-run --tenant-name "Aenaon Demo" --store-code "MAIN" --store-name "Κεντρικό"
```

Μετά την επιθεώρηση της προεπισκόπησης, αντικατάστησε το `--dry-run` με `--apply --confirm-database AenaonERP_Dev`. Η εντολή καταχωρεί την επιχείρηση και το κατάστημα μαζί και απορρίπτει την επανάληψη όταν υπάρχουν ήδη δεδομένα.

Πλήρης οδηγός, έλεγχοι SQL και ανάκτηση: [Αρχική επιχείρηση](docs/05-initial-business.md).

## Σχήμα χρηστών και συμμετοχών

Με την πρώτη migration ήδη εφαρμοσμένη, έλεγξε τη νέα αναβάθμιση:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Αναμένονται μία εφαρμοσμένη και μία εκκρεμής migration. Η εφαρμογή γίνεται με τη γνωστή εντολή `--migrate-database --apply --confirm-database AenaonERP_Dev`, μετά την επιθεώρηση και τον έλεγχο backup. Η νέα migration προσθέτει πέντε πίνακες, χωρίς λογαριασμούς ή αλλαγή των business IDs.

Πλήρης οδηγός: [Χρήστες και συμμετοχές](docs/06-users-and-memberships.md).

## Πρώτος Administrator (Βήμα 7)

Με μηδέν χρήστες και memberships, στην υπάρχουσα βάση Development:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Αντικατάστησε το email με πραγματική διεύθυνση ανάπτυξης. Έπειτα, μόνο μετά από επιτυχή προεπισκόπηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --apply --confirm-database AenaonERP_Dev --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Ο κωδικός εισάγεται **δύο φορές στο διαδραστικό terminal**, ποτέ ως παράμετρος. Απαιτούνται τουλάχιστον 14 χαρακτήρες και συνδυασμός πεζών/κεφαλαίων/αριθμού/συμβόλου. Το CLI αρνείται επανάληψη, production χρήση ή λάθος επιχείρηση/βάση. Δεν δημιουργείται νέα migration.

Πλήρης οδηγός: [Πρώτος διαχειριστής](docs/07-first-administrator.md).

## Authentication Services (Βήμα 8.1)

Για διαδραστική επαλήθευση του πρώτου Administrator (χωρίς login/session):

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --verify-credentials --username admin --confirm-database AenaonERP_Dev
~~~

Για ελεγχόμενη διόρθωση του αρχικού email **your-email@example.com**, πρώτα προεπισκόπηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --fix-admin-email --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --user-id 76d2c37f-2e26-4311-a129-e6073057c8dd --new-email new-address@your-domain.example
~~~

Μετά από έλεγχο και backup, αντικατάστησε το --dry-run με **--apply --confirm-database AenaonERP_Dev**. Ο υπάρχων κωδικός ζητείται διαδραστικά, όχι από ορίσματα. Η αλλαγή είναι εφάπαξ και δεν επιβεβαιώνει αυτόματα το νέο email.

Πλήρης οδηγός: [Authentication Services και διόρθωση email](docs/08-authentication-services.md).

## Tenant Authorization (Βήμα 8.2)

Πρώτα γίνεται πραγματική επαλήθευση κωδικού, έπειτα έλεγχος δικαιώματος Read για την ήδη δημιουργημένη επιχείρηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --permission Read --confirm-database AenaonERP_Dev
~~~

Για έλεγχο δικαιώματος Manage στο κεντρικό κατάστημα MAIN:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --store-id c697b9c3-e221-4156-b533-f54dee27d979 --permission Manage --confirm-database AenaonERP_Dev
~~~

Το CLI επιτρέπεται μόνο σε Development, ζητά τον κωδικό διαδραστικά και δεν δημιουργεί session ή αλλαγή δεδομένων επιχειρήσεων. Τα memberships αφορούν προς το παρόν **ολόκληρη την επιχείρηση**, όχι διαφορετικά δικαιώματα ανά κατάστημα.

Πλήρης οδηγός: [Tenant Authorization](docs/09-tenant-authorization.md).

## HTTPS Login και Logout (Βήμα 8.3)

Σε Development, ξεκίνα το API με το υπάρχον HTTPS launch profile:

~~~powershell
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Είναι διαθέσιμα μόνο στο Development τα GET /api/auth/csrf, POST /api/auth/login, GET /api/auth/me, GET /api/auth/access και POST /api/auth/logout. Οι ενέργειες POST απαιτούν CSRF token στο header X-Aenaon-CSRF. Το session cookie είναι Secure, HttpOnly, SameSite=Strict, έχει διάρκεια 30 λεπτά και επανελέγχεται απέναντι σε Identity security stamp και membership.

Η ενσωματωμένη πολιτική προστασίας επιτρέπει έως 10 αιτήματα auth ανά λεπτό ανά IP, επιπλέον του Identity lockout. Δεν έχουν προστεθεί business endpoints, νέα migration ή public production login.

**Πλήρης οδηγός και ασφαλές PowerShell παράδειγμα:** [Secure Login Sessions](docs/10-secure-login-sessions.md).

## Δομή

| Διαδρομή | Περιεχόμενο |
| --- | --- |
| `src/Aenaon.Api` | API, controllers, συμβόλαια και κοινή υποδομή HTTP. |
| `src/Aenaon.Application` | Κοινές υπηρεσίες εφαρμογής και συμβόλαια αποθήκευσης. |
| `src/Aenaon.Domain` | Μοντέλα και επιχειρησιακοί κανόνες, χωρίς εξάρτηση από web, βάση ή επιπλέον packages. |
| `src/Aenaon.Infrastructure` | SQL probe, EF Core context/mapping και migrations με περιορισμένο logging. |
| `tests/Aenaon.Api.Tests` | Integration tests του HTTP pipeline με απομονωμένο test server. |
| `tests/Aenaon.Application.Tests` | Έλεγχοι επικύρωσης και συντονισμού πριν από πρόσβαση στη βάση. |
| `tests/Aenaon.Domain.Tests` | Unit tests ιδιοκτησίας, μοναδικότητας κωδικών και επικύρωσης πεδίων. |
| `tests/Aenaon.Infrastructure.Tests` | Έλεγχοι TLS, CLI προστασιών, model/snapshot/SQL και πραγματικής αποθήκευσης σε CI SQL. |
| `docs/01-getting-started.md` | Αναλυτικές οδηγίες εγκατάστασης, CLI, logs και αντιμετώπισης προβλημάτων. |
| `docs/02-tenants-and-stores.md` | Οδηγός του δεύτερου βήματος και παραδείγματα των νέων κανόνων. |
| `docs/03-sql-server-connection.md` | Ρύθμιση User Secrets και διάγνωση πραγματικής σύνδεσης. |
| `docs/04-tenancy-database.md` | Προεπισκόπηση/εφαρμογή migrations, έλεγχοι SSMS και ανάκτηση. |
| `database/001-initial-tenancy.sql` | Παραγόμενο SQL της πρώτης migration για επιθεώρηση. |
| `.config/dotnet-tools.json` | Τοπικό, σταθερό εργαλείο `dotnet-ef`. |
| `docs/05-initial-business.md` | Αρχική καταχώρηση, προεπισκόπηση, προστασίες και έλεγχοι SQL. |
| `docs/06-users-and-memberships.md` | Schema Identity/memberships, αναβάθμιση και διατήρηση δεδομένων. |
| `docs/07-first-administrator.md` | Ασφαλής δημιουργία πρώτου διαχειριστή, έλεγχοι και rollback. |
| `docs/08-authentication-services.md` | Έλεγχος κωδικών, lockout και ασφαλής διόρθωση αρχικού email. |
| `docs/09-tenant-authorization.md` | Κανόνες πρόσβασης Owner/Member, isolation και CLI έλεγχος. |
| `docs/10-secure-login-sessions.md` | HTTPS session cookies, CSRF, login/logout και χειροκίνητοι έλεγχοι. |
| `database/002-identity-memberships.sql` | SQL αναβάθμισης από την πρώτη στη δεύτερη migration. |
| `docs/requirements.md` | Απαιτήσεις και αποφάσεις για τα επόμενα βήματα. |
| `global.json` | Σταθερή επιλογή SDK χωρίς preview εκδόσεις. |
| `Directory.Packages.props` | Κεντρικές εκδόσεις NuGet. |

Το Domain διατηρείται χωρίς εξωτερικά packages. Το Infrastructure χρησιμοποιεί τον επίσημο `Microsoft.Data.SqlClient`, EF Core SQL Server, Identity EF store και logging abstractions. Το EF Core Design είναι ιδιωτική εξάρτηση εργαλείων του API. Η ρητή έκδοση `System.Security.Cryptography.Pkcs` αντικαθιστά ευάλωτη μεταβατική έκδοση του driver. Τα MSTest και MVC Testing αφορούν τις δοκιμές.

## Ασφάλεια και όρια του σταδίου

Το τρέχον API εκθέτει αποκλειστικά πληροφορίες λειτουργίας. Δεν υπάρχουν ακόμη production login endpoints, πρόσβαση σε επιχειρησιακά δεδομένα ή έκδοση παραστατικών. Σε Development δοκιμάζονται αποκλειστικά ασφαλείς συνεδρίες. Η απομόνωση επιχειρήσεων και η εξουσιοδότηση αποτελούν προϋποθέσεις πριν προστεθούν αντίστοιχα endpoints.

Τα διαπιστευτήρια ανάπτυξης θα αποθηκεύονται με .NET User Secrets. Στο production θα χρησιμοποιούνται ασφαλείς ρυθμίσεις του παρόχου φιλοξενίας. Μην ανεβάζεις κωδικούς, πραγματικά δεδομένα πελατών ή κλειδιά παρόχου στο repository.

## Τεκμηρίωση

- [Πρώτη εκκίνηση](docs/01-getting-started.md)
- [Επιχειρήσεις και καταστήματα](docs/02-tenants-and-stores.md)
- [Σύνδεση SQL Server](docs/03-sql-server-connection.md)
- [Πίνακες επιχειρήσεων και καταστημάτων](docs/04-tenancy-database.md)
- [Αρχική επιχείρηση και κατάστημα](docs/05-initial-business.md)
- [Χρήστες και συμμετοχές](docs/06-users-and-memberships.md)
- [Πρώτος διαχειριστής](docs/07-first-administrator.md)
- [Authentication Services](docs/08-authentication-services.md)
- [Tenant Authorization](docs/09-tenant-authorization.md)
- [HTTPS Login Sessions](docs/10-secure-login-sessions.md)
- [Συμφωνημένες απαιτήσεις](docs/requirements.md)
