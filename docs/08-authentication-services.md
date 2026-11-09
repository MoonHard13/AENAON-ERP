# Aenaon ERP — Authentication Services και διόρθωση αρχικού email

## Σκοπός και σύντομη περιγραφή

Το Βήμα 8.1 προσθέτει υπηρεσία **επαλήθευσης username/password με ASP.NET Core Identity** και προστασία από επαναλαμβανόμενες αποτυχίες. Δεν δημιουργεί session, cookie ή token, ούτε προσθέτει HTTP endpoint σύνδεσης. Το αποτέλεσμα επιτυχίας περιέχει μόνο το UserId. Τα δικαιώματα επιχείρησης/καταστήματος θα ελέγχονται ξεχωριστά στο επόμενο στάδιο.

Προστίθεται επίσης μία αυστηρά περιορισμένη Development διαδικασία για τη διόρθωση του αρχικού ενδεικτικού email του πρώτου Administrator, χωρίς απευθείας SQL UPDATE.

## Προϋποθέσεις

- Ενημερωμένος κώδικας Aenaon ERP με τα Βήματα 1–7.
- .NET 10 SDK και SQL Server 2022.
- Σωστή σύνδεση Development στη βάση AenaonERP_Dev μέσω .NET User Secrets.
- 2 εφαρμοσμένες migrations και 0 εκκρεμείς.
- Πρώτος Administrator με UserId 76d2c37f-2e26-4311-a129-e6073057c8dd.
- Επιχείρηση Aenaon Demo με TenantId 9698df79-3c31-41c9-b292-b7240c7265fb.
- Ασφαλής πρόσβαση στο VS Code Terminal, χωρίς redirected stdin.

{% hint style="warning" %}
Η διαδικασία αλλαγής email μεταβάλλει δεδομένα. Επιβεβαίωσε ότι εργάζεσαι αποκλειστικά στη δοκιμαστική βάση και διατήρησε διαθέσιμο backup. Μην εισάγεις, αλλάζεις ή διαγράφεις εγγραφές AspNetUsers χειροκίνητα μέσω SQL.
{% endhint %}

## 1. Ενημέρωση project και έλεγχοι

Μετά την ενσωμάτωση του αντίστοιχου Pull Request στο main:

~~~powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
~~~

Οι υπάρχουσες επιχειρήσεις, καταστήματα και λογαριασμοί παραμένουν αμετάβλητοι κατά το build. Δεν απαιτείται migration.

## 2. Προεπισκόπηση της αλλαγής email

Στο PowerShell, μέσα στον κύριο φάκελο AENAON-ERP, αντικατάστησε το **new-address@your-domain.example** με πραγματικό email που ελέγχεις:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --fix-admin-email --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --user-id 76d2c37f-2e26-4311-a129-e6073057c8dd --new-email new-address@your-domain.example
~~~

Αναμενόμενη έξοδος:

~~~text
Βάση: AenaonERP_Dev
UserId: 76d2c37f-2e26-4311-a129-e6073057c8dd
Η προεπισκόπηση ολοκληρώθηκε χωρίς αλλαγές.
~~~

Η προεπισκόπηση απαιτεί ακριβώς έναν λογαριασμό και ένα membership Owner στην επιλεγμένη επιχείρηση, με username admin και **τρέχον email your-email@example.com**. Αν έχει διορθωθεί ήδη, η εντολή απορρίπτει οποιαδήποτε νέα μεταβολή.

## 3. Εφαρμογή μετά την προεπισκόπηση

Με το **ίδιο πραγματικό email**:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --fix-admin-email --apply --confirm-database AenaonERP_Dev --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --user-id 76d2c37f-2e26-4311-a129-e6073057c8dd --new-email new-address@your-domain.example
~~~

Η εφαρμογή ζητά **τον υπάρχοντα κωδικό του Administrator** χωρίς να εμφανίζει τους χαρακτήρες. Το πρόγραμμα ελέγχει τον κωδικό με την κοινή Identity υπηρεσία και έπειτα χρησιμοποιεί UserManager.SetEmailAsync. Η νέα διεύθυνση δεν θεωρείται αυτομάτως επιβεβαιωμένη. Δεν υπάρχει ακόμη ροή αποστολής email επιβεβαίωσης.

{% hint style="danger" %}
Δεν υπάρχει παράμετρος --password. Μην γράψεις τον κωδικό στο terminal ως όρισμα, σε logs, commit, ticket ή chat. Με Esc ακυρώνεται η καταχώρηση. Η εντολή επιτρέπεται μόνο σε Development και μόνο για το αρχικό placeholder email.
{% endhint %}

## 4. Επαλήθευση email στο SSMS

Εκτέλεσε μόνο SELECT:

~~~sql
USE [AenaonERP_Dev];

SELECT Id, UserName, Email, NormalizedEmail, EmailConfirmed,
       LockoutEnabled, AccessFailedCount
FROM dbo.AspNetUsers
WHERE Id = '76d2c37f-2e26-4311-a129-e6073057c8dd';

SELECT TenantId, UserId, Role
FROM dbo.TenantMemberships
WHERE UserId = '76d2c37f-2e26-4311-a129-e6073057c8dd';
~~~

Αναμένονται το νέο email, το αντίστοιχο κανονικοποιημένο email, EmailConfirmed = 0, LockoutEnabled = 1 και **ίδιο UserId** με Role = 1. Μην επιλέξεις ή κοινοποιήσεις το PasswordHash, το SecurityStamp ή άλλα token fields.

## 5. Δοκιμή διαπιστευτηρίων μέσω CLI

Η επαλήθευση κωδικού γίνεται χωρίς login cookie ή session:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --verify-credentials --username admin --confirm-database AenaonERP_Dev
~~~

Η εντολή ζητά διαδραστικά τον κωδικό. Σε επιτυχία εμφανίζει UserId και μήνυμα ότι δεν δημιουργήθηκε session. Σε αποτυχία δεν αποκαλύπτει αν το username είναι λανθασμένο ή ο λογαριασμός κλειδωμένος.

**Προσοχή:** Κάθε λανθασμένη δοκιμή για υπαρκτό username αυξάνει τον μετρητή αποτυχιών. Μετά από **5 αποτυχίες** ο λογαριασμός κλειδώνεται για **15 λεπτά**. Μην επαναλαμβάνεις δοκιμές με μαντεμένους κωδικούς.

## 6. Αρχιτεκτονική και ασφάλεια

| Επίπεδο | Στοιχείο | Ευθύνη |
| --- | --- | --- |
| Application | ICredentialVerifier | Κοινό συμβόλαιο ελέγχου |
| Infrastructure | IdentityCredentialVerifier | Identity password verification, lockout, logging |
| Application | AdministratorEmailChangeService | Επικύρωση των δεδομένων αλλαγής email |
| Infrastructure | SqlAdministratorEmailChangeStore | Έλεγχος δικαιούχου και αλλαγή email μέσω UserManager |
| API | VerifyCredentialsCommand | Διαδραστική δοκιμή μόνο στο Development |
| API | ChangeAdministratorEmailCommand | Προεπισκόπηση/εφαρμογή μόνο στο Development |

Ο έλεγχος κωδικού βασίζεται σε UserManager.CheckPasswordAsync, AccessFailedAsync, ResetAccessFailedCountAsync και IsLockedOutAsync. Αποτυχημένα αποτελέσματα δεν περιλαμβάνουν UserId. Για ανύπαρκτο/κλειδωμένο username χρησιμοποιείται ένα τεχνικό dummy password hash για μείωση της διαφοράς χρόνου. Αυτό δεν υποκαθιστά HTTP rate limiting, τα οποία θα απαιτηθούν όταν προστεθούν endpoints.

Η επιτυχής επαλήθευση **δεν απονέμει δικαιώματα** από μόνη της. Μία επόμενη εξουσιοδοτημένη ροή πρέπει να επαληθεύει τη συμμετοχή TenantMemberships και τον ρόλο στη συγκεκριμένη επιχείρηση, πριν από πρόσβαση σε οποιαδήποτε ERP δεδομένα.

## 7. Logs, ακύρωση και exit codes

Οι ενέργειες καταγράφουν προεπισκόπηση, επιτυχία, άρνηση και αριθμούς SQL σφαλμάτων, χωρίς plaintext password, email, connection string ή hash. Η πληκτρολόγηση κωδικού είναι διαδραστική και αποκρύπτεται. Esc ακυρώνει την εισαγωγή και Ctrl+C ζητά ακύρωση. Αν τερματιστεί η διαδικασία αλλαγής email, επανέλεγξε την υπάρχουσα διεύθυνση μέσω SELECT.

| Exit code | Περιγραφή |
| --- | --- |
| 0 | Επιτυχής προεπισκόπηση ή ενέργεια |
| 1 | SQL αποτυχία ή timeout |
| 2 | Λανθασμένα ορίσματα, μη έγκυρη κατάσταση/κωδικός ή μη επιτρεπτή βάση |
| 130 | Ακύρωση |

## 8. Troubleshooting και ανάκτηση

| Περίπτωση | Αντιμετώπιση |
| --- | --- |
| Μηδέν εκκρεμείς migrations αλλά άρνηση | Επιβεβαίωσε σωστό Development profile και database |
| Ο Administrator έχει ήδη άλλο email | Η ειδική εντολή χρησιμοποιείται μόνο μία φορά· μην επαναλάβεις |
| EmailConfirmed = 0 | Αναμενόμενο· επιβεβαίωση διεύθυνσης θα προστεθεί σε επόμενο στάδιο |
| Αποτυχία κωδικού | Επιβεβαίωσε τον πραγματικό κωδικό, μην επιμένεις με πολλές δοκιμές |
| Lockout | Περίμενε 15 λεπτά, μην κάνεις χειροκίνητο SQL reset |
| Διπλό email | Η μοναδικότητα του Identity/SQL απορρίπτει αλλαγή |
| Αδυναμία σύνδεσης SQL | Έλεγξε --check-database χωρίς κοινοποίηση μυστικών |
| Ακύρωση κατά την αλλαγή email | Έλεγξε τη διεύθυνση με read-only SELECT πριν επανάληψη |

Για τα επόμενα deployments θα προστεθούν HTTPS cookie authentication, προστασία CSRF, rate limiting, session invalidation, ασφαλής επιβεβαίωση email και δυνατότητες MFA. Μην εκθέτεις το τρέχον credential verifier σε δημόσιο API χωρίς τις παραπάνω προστασίες.

## 9. Έλεγχοι πριν/μετά και checklist

- [ ] Το Git branch είναι το αναμενόμενο και το build/tests περνούν.
- [ ] Η SQL δείχνει 2 applied migrations / 0 pending.
- [ ] Έχει επιβεβαιωθεί το σωστό TenantId και UserId.
- [ ] Η προεπισκόπηση αλλαγής email δεν τροποποιεί δεδομένα.
- [ ] Υπάρχει backup πριν από τη μοναδική αλλαγή.
- [ ] Το email διορθώθηκε χωρίς αλλαγή UserId, username ή membership.
- [ ] Το νέο email παραμένει μη επιβεβαιωμένο.
- [ ] Το CLI επαλήθευσης δέχεται τον σωστό κωδικό, χωρίς δημιουργία session.
- [ ] Δεν κοινοποιήθηκαν passwords, tokens ή hashes.
- [ ] Τα logs δεν εκθέτουν προσωπικά στοιχεία ή μυστικά.

## 10. Επόμενο βήμα

Στο Βήμα 8.2 θα ορίσουμε authorization ανά επιχείρηση και κατάστημα. Στη συνέχεια θα προστεθούν ασφαλή login/logout HTTP endpoints με session cookies και τις αντίστοιχες προστασίες.
