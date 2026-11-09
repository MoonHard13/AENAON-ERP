# Aenaon ERP

Cloud σύστημα εστίασης για πολλές ανεξάρτητες επιχειρήσεις, με POS, Back office και υποστήριξη υπηρεσιών.

## Τρέχον στάδιο

**Βήμα 3 — Έλεγχος σύνδεσης SQL Server.** Προστίθενται ανεξάρτητο Infrastructure project, έλεγχος σύνδεσης από CLI και διαγνωστικό endpoint στο Development. Τα διαπιστευτήρια ορίζονται τοπικά μέσω User Secrets. Η αποθήκευση των επιχειρησιακών μοντέλων και η διαχείριση από φόρμες θα ακολουθήσουν.

Κάθε κατάστημα έχει συγκεκριμένο ιδιοκτήτη (`TenantId`). Ο κωδικός καταστήματος είναι μοναδικός μέσα στην ίδια επιχείρηση· διαφορετικές επιχειρήσεις μπορούν να έχουν τον ίδιο κωδικό. Η συλλογή καταστημάτων τροποποιείται μέσα από τους κανόνες της επιχείρησης.

| Διαδρομή | Απάντηση |
| --- | --- |
| `GET /health` | `Healthy`, όταν το API ανταποκρίνεται. |
| `GET /api/info` | `{"name":"Aenaon ERP API"}`. |
| `GET /health/database` | `Healthy` ή `Unhealthy`, αποκλειστικά σε Development, για την πραγματική σύνδεση SQL. |

Το `/health` σε αυτό το στάδιο ελέγχει τη διαθεσιμότητα του API. Δεν ελέγχει βάση δεδομένων, πάροχο ή συγχρονισμό.

Ο έλεγχος `/health/database` ανοίγει τη ρυθμισμένη σύνδεση και διαβάζει το compatibility level. Δεν δημιουργεί ή τροποποιεί πίνακες/δεδομένα και δεν αποτελεί έλεγχο ολοκλήρωσης μελλοντικών migrations.

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

## Δομή

| Διαδρομή | Περιεχόμενο |
| --- | --- |
| `src/Aenaon.Api` | API, controllers, συμβόλαια και κοινή υποδομή HTTP. |
| `src/Aenaon.Domain` | Μοντέλα και επιχειρησιακοί κανόνες, χωρίς εξάρτηση από web, βάση ή επιπλέον packages. |
| `src/Aenaon.Infrastructure` | SQL connection probe, πολιτική σύνδεσης και περιορισμένο logging. |
| `tests/Aenaon.Api.Tests` | Integration tests του HTTP pipeline με απομονωμένο test server. |
| `tests/Aenaon.Domain.Tests` | Unit tests ιδιοκτησίας, μοναδικότητας κωδικών και επικύρωσης πεδίων. |
| `tests/Aenaon.Infrastructure.Tests` | Έλεγχοι διαπιστευτηρίων/TLS, ακύρωσης, logging και απομονωμένο SQL integration test. |
| `docs/01-getting-started.md` | Αναλυτικές οδηγίες εγκατάστασης, CLI, logs και αντιμετώπισης προβλημάτων. |
| `docs/02-tenants-and-stores.md` | Οδηγός του δεύτερου βήματος και παραδείγματα των νέων κανόνων. |
| `docs/03-sql-server-connection.md` | Ρύθμιση User Secrets και διάγνωση πραγματικής σύνδεσης. |
| `docs/requirements.md` | Απαιτήσεις και αποφάσεις για τα επόμενα βήματα. |
| `global.json` | Σταθερή επιλογή SDK χωρίς preview εκδόσεις. |
| `Directory.Packages.props` | Κεντρικές εκδόσεις NuGet. |

Το Domain διατηρείται χωρίς εξωτερικά packages. Το Infrastructure χρησιμοποιεί τον επίσημο `Microsoft.Data.SqlClient` και logging abstractions. Η ρητή έκδοση `System.Security.Cryptography.Pkcs` αντικαθιστά ευάλωτη μεταβατική έκδοση του driver. Τα MSTest και MVC Testing αφορούν τις δοκιμές.

## Ασφάλεια και όρια του σταδίου

Το τρέχον API εκθέτει αποκλειστικά πληροφορίες λειτουργίας. Δεν υπάρχει ακόμη σύνδεση χρηστών, πρόσβαση σε επιχειρησιακά δεδομένα ή έκδοση παραστατικών. Η απομόνωση επιχειρήσεων και η εξουσιοδότηση αποτελούν προϋποθέσεις πριν προστεθούν αντίστοιχα endpoints.

Τα διαπιστευτήρια ανάπτυξης θα αποθηκεύονται με .NET User Secrets. Στο production θα χρησιμοποιούνται ασφαλείς ρυθμίσεις του παρόχου φιλοξενίας. Μην ανεβάζεις κωδικούς, πραγματικά δεδομένα πελατών ή κλειδιά παρόχου στο repository.

## Τεκμηρίωση

- [Πρώτη εκκίνηση](docs/01-getting-started.md)
- [Επιχειρήσεις και καταστήματα](docs/02-tenants-and-stores.md)
- [Σύνδεση SQL Server](docs/03-sql-server-connection.md)
- [Συμφωνημένες απαιτήσεις](docs/requirements.md)
