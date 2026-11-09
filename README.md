# Aenaon ERP

Cloud σύστημα εστίασης για πολλές ανεξάρτητες επιχειρήσεις, με POS, Back office και υποστήριξη υπηρεσιών.

## Τρέχον στάδιο

**Βήμα 2 — Μοντέλα επιχείρησης και καταστήματος.** Η βάση του API επεκτείνεται με ανεξάρτητο Domain project, κανόνες δημιουργίας επιχειρήσεων/καταστημάτων και unit tests. Η διαχείριση από φόρμες και η αποθήκευση στη βάση θα προστεθούν σε επόμενα βήματα.

Κάθε κατάστημα έχει συγκεκριμένο ιδιοκτήτη (`TenantId`). Ο κωδικός καταστήματος είναι μοναδικός μέσα στην ίδια επιχείρηση· διαφορετικές επιχειρήσεις μπορούν να έχουν τον ίδιο κωδικό. Η συλλογή καταστημάτων τροποποιείται μέσα από τους κανόνες της επιχείρησης.

| Διαδρομή | Απάντηση |
| --- | --- |
| `GET /health` | `Healthy`, όταν το API ανταποκρίνεται. |
| `GET /api/info` | `{"name":"Aenaon ERP API"}`. |

Το `/health` σε αυτό το στάδιο ελέγχει τη διαθεσιμότητα του API. Δεν ελέγχει βάση δεδομένων, πάροχο ή συγχρονισμό.

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
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore
dotnet list AenaonERP.slnx package --outdated
dotnet list AenaonERP.slnx package --vulnerable --include-transitive
```

Οι ίδιες εντολές build και test εκτελούνται από το GitHub Actions σε Windows και Linux. Η επιτυχία κάθε εκτέλεσης φαίνεται στην καρτέλα **Actions**.

## Δομή

| Διαδρομή | Περιεχόμενο |
| --- | --- |
| `src/Aenaon.Api` | API, controllers, συμβόλαια και κοινή υποδομή HTTP. |
| `src/Aenaon.Domain` | Μοντέλα και επιχειρησιακοί κανόνες, χωρίς εξάρτηση από web, βάση ή επιπλέον packages. |
| `tests/Aenaon.Api.Tests` | Integration tests του HTTP pipeline με απομονωμένο test server. |
| `tests/Aenaon.Domain.Tests` | Unit tests ιδιοκτησίας, μοναδικότητας κωδικών και επικύρωσης πεδίων. |
| `docs/01-getting-started.md` | Αναλυτικές οδηγίες εγκατάστασης, CLI, logs και αντιμετώπισης προβλημάτων. |
| `docs/02-tenants-and-stores.md` | Οδηγός του δεύτερου βήματος και παραδείγματα των νέων κανόνων. |
| `docs/requirements.md` | Απαιτήσεις και αποφάσεις για τα επόμενα βήματα. |
| `global.json` | Σταθερή επιλογή SDK χωρίς preview εκδόσεις. |
| `Directory.Packages.props` | Κεντρικές εκδόσεις των πακέτων δοκιμών. |

Το παραγωγικό API χρησιμοποιεί τα ενσωματωμένα στοιχεία του ASP.NET Core, χωρίς επιπλέον NuGet packages. Τα πακέτα MSTest και MVC Testing αφορούν τις δοκιμές.

## Ασφάλεια και όρια του σταδίου

Το τρέχον API εκθέτει αποκλειστικά πληροφορίες λειτουργίας. Δεν υπάρχει ακόμη σύνδεση χρηστών, πρόσβαση σε επιχειρησιακά δεδομένα ή έκδοση παραστατικών. Η απομόνωση επιχειρήσεων και η εξουσιοδότηση αποτελούν προϋποθέσεις πριν προστεθούν αντίστοιχα endpoints.

Τα διαπιστευτήρια ανάπτυξης θα αποθηκεύονται με .NET User Secrets. Στο production θα χρησιμοποιούνται ασφαλείς ρυθμίσεις του παρόχου φιλοξενίας. Μην ανεβάζεις κωδικούς, πραγματικά δεδομένα πελατών ή κλειδιά παρόχου στο repository.

## Τεκμηρίωση

- [Πρώτη εκκίνηση](docs/01-getting-started.md)
- [Επιχειρήσεις και καταστήματα](docs/02-tenants-and-stores.md)
- [Συμφωνημένες απαιτήσεις](docs/requirements.md)
