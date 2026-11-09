# Aenaon ERP — Πρώτος διαχειριστής επιχείρησης

## Σκοπός

Το έβδομο βήμα δημιουργεί **μία φορά** τον πρώτο λογαριασμό ASP.NET Core Identity και τον συνδέει ως **Owner** με την υπάρχουσα επιχείρηση. Δεν προσθέτει migrations, δεν δημιουργεί επιχείρηση/κατάστημα και δεν προσθέτει HTTP endpoint εγγραφής.

Η εντολή ισχύει μόνο σε Development. Ο χρήστης και το membership γράφονται στην ίδια συναλλαγή, με SQL application lock για προστασία από ταυτόχρονες αρχικοποιήσεις.

{% hint style="warning" %}
Πριν από την εφαρμογή έλεγξε το σωστό TenantId, την απομονωμένη βάση Development και το διαθέσιμο backup. Μην δημιουργείς χρήστες με χειροκίνητα SQL INSERT.
{% endhint %}

## Προϋποθέσεις

- .NET 10 SDK, Git και πρόσβαση στον υπάρχοντα SQL Server 2022.
- Η σύνδεση User Secrets δείχνει στη βάση AenaonERP_Dev.
- Δύο εφαρμοσμένες migrations και μηδέν εκκρεμείς.
- Επιχείρηση Aenaon Demo με TenantId 9698df79-3c31-41c9-b292-b7240c7265fb.
- Μηδέν εγγραφές στους πίνακες AspNetUsers και TenantMemberships.
- Έγκυρο email διαχειριστή και ισχυρός κωδικός που δεν περιέχεται σε αρχεία ή ιστορικό terminal.
- Σταματημένο API κατά την αρχικοποίηση.

## 1. Ενημέρωση κώδικα και έλεγχοι

Μετά την ενσωμάτωση του αντίστοιχου PR, από τον βασικό φάκελο:

~~~powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
~~~

Προστάτευσε τις τοπικές αλλαγές πριν από το pull.

## 2. Προεπισκόπηση

Αντικατάστησε το ενδεικτικό email με το δικό σου πραγματικό email ανάπτυξης:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Αναμενόμενη έξοδος:

~~~text
Βάση: AenaonERP_Dev
Επιχείρηση: Aenaon Demo
TenantId: 9698df79-3c31-41c9-b292-b7240c7265fb
Η προεπισκόπηση ολοκληρώθηκε χωρίς καταχώρηση χρήστη.
~~~

Δεν ζητείται κωδικός και δεν γίνονται αλλαγές δεδομένων.

## 3. Εφαρμογή

Μόνο μετά την επιτυχή προεπισκόπηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --apply --confirm-database AenaonERP_Dev --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Το CLI ζητά τον κωδικό **δύο φορές** με απόκρυψη χαρακτήρων. Πάτησε Esc για ακύρωση. Η πολιτική απαιτεί τουλάχιστον 14 χαρακτήρες, 6 διαφορετικούς χαρακτήρες, κεφαλαίο, πεζό, αριθμό και σύμβολο.

{% hint style="danger" %}
Δεν υποστηρίζεται παράμετρος --password ή ανακατεύθυνση stdin στο --apply. Μη βάζεις κωδικό στο PowerShell history, στο repository, σε logs ή σε tickets.
{% endhint %}

Σε επιτυχία εμφανίζονται η βάση, η επιχείρηση, το TenantId και το νέο UserId. Ο κωδικός δεν εμφανίζεται.

## 4. Έλεγχοι μετά τη δημιουργία

Στο SSMS εκτέλεσε **μόνο read-only** queries:

~~~sql
USE [AenaonERP_Dev];

SELECT COUNT_BIG(*) AS TotalUsers FROM dbo.AspNetUsers;
SELECT COUNT_BIG(*) AS TotalMemberships FROM dbo.TenantMemberships;

SELECT u.Id AS UserId, u.UserName, u.Email,
       t.Id AS TenantId, t.Name AS TenantName, m.Role
FROM dbo.TenantMemberships AS m
JOIN dbo.AspNetUsers AS u ON u.Id = m.UserId
JOIN dbo.Tenants AS t ON t.Id = m.TenantId;
~~~

Αναμένονται ένας χρήστης, ένα membership και Role = 1 (Owner) στη σωστή επιχείρηση. Οι υπάρχουσες εγγραφές Tenants/Stores διατηρούνται.

{% hint style="info" %}
Η δημιουργία του πρώτου χρήστη **δεν ενεργοποιεί login**. Authentication, sessions, αλλαγές κωδικών και tenant-aware authorization θα υλοποιηθούν ξεχωριστά πριν από επιχειρησιακά endpoints.
{% endhint %}

## Πότε χρησιμοποιείται / δεν χρησιμοποιείται

Η εντολή προορίζεται αποκλειστικά για **μία αρχική εγκατάσταση Development**. Απορρίπτει επανάληψη όταν υπάρχουν ήδη χρήστες ή memberships. Δεν είναι διαδικασία δημιουργίας επιπλέον χρηστών, ανάκτησης λογαριασμού, αντικατάστασης Owner ή παραγωγικής εγκατάστασης.

## CLI, logs και ασφάλεια

| Exit code | Σημασία |
| --- | --- |
| 0 | Επιτυχής προεπισκόπηση ή δημιουργία |
| 1 | SQL σφάλμα, timeout ή απασχολημένος πόρος |
| 2 | Επικύρωση, schema, επιχείρηση, ήδη υπάρχοντες χρήστες ή μη έγκυρες επιλογές |
| 130 | Ακύρωση |

Τα logs καταγράφουν προεπισκόπηση, δημιουργία, άρνηση και SQL error numbers, όχι κωδικό, email, connection string ή password hash. Η δημιουργία λογαριασμού και membership είναι ατομική: σε αποτυχία αποθήκευσης membership γίνεται rollback του χρήστη. Το SQL application lock εμποδίζει δύο ταυτόχρονες επιτυχείς αρχικοποιήσεις.

## Ανάκτηση και troubleshooting

| Πρόβλημα | Ενέργεια |
| --- | --- |
| Εκκρεμείς migrations | Προεπισκόπηση με --migrate-database --dry-run |
| Ανύπαρκτο TenantId | Ανάγνωση dbo.Tenants και επιβεβαίωση ID |
| ExistingUsers | Έλεγχος users/memberships, χωρίς χειροκίνητες διαγραφές |
| PasswordRejected | Νέος ισχυρός κωδικός σύμφωνα με την πολιτική |
| Λάθος βάση | Έλεγχος του ακριβούς ονόματος AenaonERP_Dev |
| SQL 547 | Έλεγχος constraints και rollback, χωρίς νέα προσπάθεια στα τυφλά |
| Timeout / Busy | Έλεγχος SQL locks και ύπαρξης δεδομένων πριν επανάληψη |
| Μη διαδραστικό terminal | Τοπικό PowerShell ή VS Code Terminal χωρίς stdin redirection |

Μετά από timeout ή αποτυχία, **πρώτα** επανέλεγξε τους πίνακες με τα παραπάνω SELECT. Μη διαγράφεις λογαριασμό για να επαναλάβεις, ιδίως αν έγινε ήδη commit. Για λάθος username/email χρειάζεται μελλοντική ελεγχόμενη διαδικασία διαχείρισης χρηστών. Κράτησε backup και μην επεξεργάζεσαι απευθείας password hashes.

## Checklist

- [ ] Επιτυχές restore/build/tests.
- [ ] Επιβεβαίωση AenaonERP_Dev, TenantId και 2 migrations χωρίς pending.
- [ ] Μηδέν users και memberships πριν.
- [ ] Προεπισκόπηση χωρίς αλλαγές.
- [ ] Διαδραστική είσοδος κωδικού και διπλή επιβεβαίωση.
- [ ] Ένας χρήστης και ένα membership με Owner ρόλο.
- [ ] Αμετάβλητες επιχειρήσεις και καταστήματα.
- [ ] Καμία διαρροή διαπιστευτηρίων.

## Επόμενο βήμα

Θα σχεδιαστούν ασφαλείς ροές authentication/session, προστασία login, MFA όπου απαιτείται, και tenant-aware authorization πριν εκτεθούν δεδομένα ERP μέσω API.
