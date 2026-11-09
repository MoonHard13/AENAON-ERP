# Aenaon ERP — Tenant Authorization (Βήμα 8.2)

## Σκοπός και σύντομη περιγραφή

Το Βήμα 8.2 υλοποιεί το **Tenant Authorization**: υποχρεωτικό έλεγχο συμμετοχής σε επιχείρηση, ρόλου χρήστη και ιδιοκτησίας καταστήματος. Η προεπιλεγμένη απόφαση είναι πάντα **Denied**.

Δεν προστίθενται νέοι πίνακες, migrations, HTTP login endpoints, cookies ή tokens. Οι υπηρεσίες είναι προετοιμασμένες για μελλοντική χρήση σε προστατευμένα endpoints.

## Προϋποθέσεις

- .NET 10 SDK, SQL Server 2022, υπάρχουσα AenaonERP_Dev και ενεργά .NET User Secrets.
- Δύο εφαρμοσμένες migrations, μηδέν εκκρεμείς.
- Administrator username admin, UserId 76d2c37f-2e26-4311-a129-e6073057c8dd.
- Επιχείρηση Aenaon Demo, TenantId 9698df79-3c31-41c9-b292-b7240c7265fb.
- Κεντρικό κατάστημα MAIN, StoreId c697b9c3-e221-4156-b533-f54dee27d979.
- Πρόσβαση σε τοπικό διαδραστικό PowerShell / VS Code Terminal, με τον κωδικό του Administrator.

{% hint style="warning" %}
Το UserId που δέχεται ο κοινός κώδικας authorization πρέπει να προέρχεται **μόνο από επικυρωμένη ταυτότητα στον server**, ποτέ από αίτημα πελάτη, query parameter ή header. Στο συγκεκριμένο CLI προηγείται η πραγματική επαλήθευση κωδικού μέσω Identity.
{% endhint %}

## 1. Πίνακας δικαιωμάτων

| Ταυτότητα | Read επιχείρησης | Manage επιχείρησης | Πρόσβαση σε ξένο κατάστημα |
| --- | --- | --- | --- |
| Owner της επιχείρησης | Επιτρέπεται | Επιτρέπεται | Απαγορεύεται |
| Member της επιχείρησης | Επιτρέπεται | Απαγορεύεται | Απαγορεύεται |
| Μη μέλος | Απαγορεύεται | Απαγορεύεται | Απαγορεύεται |
| Owner άλλης επιχείρησης | Απαγορεύεται | Απαγορεύεται | Απαγορεύεται |

**Περιορισμός του τρέχοντος schema:** Η συμμετοχή υφίσταται σε επίπεδο **επιχείρησης** και όχι σε επίπεδο καταστήματος. Ένας Owner ή Member μπορεί προς το παρόν να διαβάσει όλα τα καταστήματα **της δικής του επιχείρησης**. Για λεπτομερή δικαιώματα ανά κατάστημα θα απαιτηθεί νέο μοντέλο memberships/permissions και migration πριν χρησιμοποιηθεί σε πραγματικές λειτουργίες.

## 2. Ενημέρωση κώδικα και έλεγχοι

Μετά το merge του Pull Request:

~~~powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
~~~

Δεν χρειάζεται νέα migration.

## 3. Επιβεβαίωση υφιστάμενων δεδομένων

Στο SSMS εκτέλεσε **μόνο SELECT**:

~~~sql
USE [AenaonERP_Dev];

SELECT Id, Name FROM dbo.Tenants;
SELECT Id, TenantId, Code, Name FROM dbo.Stores;
SELECT TenantId, UserId, Role FROM dbo.TenantMemberships;
~~~

Επιβεβαίωσε την επιχείρηση Aenaon Demo, το κατάστημα MAIN και μία συμμετοχή Owner (Role = 1).

## 4. Δοκιμή δικαιώματος Read

Στο VS Code Terminal:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --permission Read --confirm-database AenaonERP_Dev
~~~

Δώσε διαδραστικά τον **υπάρχοντα** κωδικό Administrator. Η εφαρμογή ελέγχει πρώτα τα διαπιστευτήρια και μετά τη συμμετοχή του χρήστη. Αναμένεται UserId, TenantId, ρόλος Owner, δικαίωμα Read και μήνυμα ότι **δεν δημιουργήθηκε session**.

## 5. Δοκιμή δικαιώματος Manage για το κατάστημα MAIN

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --store-id c697b9c3-e221-4156-b533-f54dee27d979 --permission Manage --confirm-database AenaonERP_Dev
~~~

Ο Owner αναμένεται να εγκριθεί. Για έλεγχο απόρριψης σε μη υπαρκτό κατάστημα, μπορείς να δώσεις διαφορετικό τυχαίο StoreId (GUID). Η εντολή πρέπει να επιστρέψει **exit code 3** χωρίς αλλαγή των ERP δεδομένων.

{% hint style="info" %}
Δεν είναι απαραίτητο να δημιουργηθεί δεύτερη επιχείρηση στη βάση σου για δοκιμές. Το CI εξετάζει δύο επιχειρήσεις, διαφορετικά καταστήματα, Owner/Member και χρήστες χωρίς membership σε απομονωμένο SQL Server.
{% endhint %}

## 6. Αρχιτεκτονική και ασφάλεια

| Στοιχείο | Ευθύνη |
| --- | --- |
| ITenantAccessStore | Εξειδικευμένα συμβόλαια ανάγνωσης συμμετοχών, επιχειρήσεων και καταστημάτων |
| SqlTenantAccessStore | Parameterized EF Core queries, υποχρεωτικό φιλτράρισμα TenantId |
| TenantAuthorizationService | Πολιτικές Read/Manage, έλεγχος ρόλου, deny by default |
| TenantAccessCommand | Διαδραστική Development CLI δοκιμή με password verification |
| ICredentialVerifier | Πραγματική επαλήθευση ταυτότητας μέσω ASP.NET Core Identity |

Η λειτουργία GetAccessibleTenantsAsync επιστρέφει μόνο επιχειρήσεις με membership για το **ήδη επαληθευμένο** UserId. Η εντολή δεν δέχεται UserId ως είσοδο.

**Προσοχή:** Το authorization result δεν αποτελεί αποδεικτικό session. Σε μελλοντικό HTTP API απαιτούνται authentication cookies, antiforgery, server-side identity και επανέλεγχος tenant στο κάθε query.

## 7. CLI, logging και επιστρεφόμενοι κωδικοί

| Exit code | Ερμηνεία |
| --- | --- |
| 0 | Ο χρήστης επαληθεύτηκε και επιτρέπεται η ζητούμενη πρόσβαση |
| 2 | Λανθασμένα ορίσματα, μη επιβεβαιωμένη βάση ή αποτυχία κωδικού |
| 3 | Ο χρήστης επαληθεύτηκε αλλά δεν διαθέτει το απαιτούμενο δικαίωμα |
| 130 | Ακύρωση |

Το CLI λειτουργεί μόνο σε Development. Δεν αποθηκεύει ή εμφανίζει password, hash ή connection string. Η επαλήθευση λανθασμένου κωδικού μπορεί να αυξήσει τον μετρητή Identity: μετά από πέντε αποτυχίες ενεργοποιείται lockout 15 λεπτών.

## 8. Troubleshooting

| Πρόβλημα | Διάγνωση |
| --- | --- |
| Η βάση δεν επιβεβαιώνεται | Έλεγξε User Secrets, --check-database και --migrate-database --dry-run |
| Ο κωδικός απορρίπτεται | Μην κάνεις επαναλαμβανόμενες προσπάθειες· μπορεί να ενεργοποιηθεί lockout |
| Exit code 3 για επιχείρηση | Έλεγξε το membership και το ακριβές TenantId |
| Exit code 3 για κατάστημα | Έλεγξε στο dbo.Stores αν το StoreId ανήκει στο σωστό TenantId |
| Owner άλλης επιχείρησης απορρίπτεται | Αναμενόμενη απομόνωση |
| Χρειάζονται διακριτά δικαιώματα ανά Store | Το τρέχον schema υποστηρίζει μόνο TenantMemberships |

Δεν γίνονται αλλαγές επιχειρησιακών δεδομένων, συνεπώς δεν απαιτείται rollback. Μην διορθώνεις memberships απευθείας στη SQL για να περάσει ο έλεγχος.

{% hint style="danger" %}
Δεν πρέπει να δημοσιευτεί endpoint που εμπιστεύεται UserId πελάτη, ούτε επιχειρησιακό API πριν από authenticated sessions, rate limiting, anti-CSRF και tenant-scoped authorization σε κάθε λειτουργία.
{% endhint %}

## 9. Checklist

- [ ] Release build και unit tests επιτυχή.
- [ ] GitHub CI Windows, Linux και SQL Server ελεγμένο.
- [ ] Δύο applied migrations, μηδέν pending.
- [ ] Read για Aenaon Demo επιτρέπεται.
- [ ] Manage για το MAIN επιτρέπεται στον Owner.
- [ ] Άσχετο StoreId απορρίπτεται με exit code 3.
- [ ] Τα υπάρχοντα δεδομένα δεν μεταβλήθηκαν.
- [ ] Δεν καταγράφηκε κωδικός ή άλλο μυστικό.

## 10. Επόμενο βήμα

Στο Βήμα 8.3 θα προστεθούν προστατευμένα login/logout endpoints, HTTPS cookie sessions, rate limiting και CSRF προστασία. Οι ενέργειες που αφορούν επιχειρήσεις θα χρησιμοποιούν το ίδιο TenantAuthorizationService, πάντα με UserId που προκύπτει από επικυρωμένη συνεδρία.
