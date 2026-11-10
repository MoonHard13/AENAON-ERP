# Aenaon ERP — UI/UX Hardening (Βήμα 8.4.5)

## Σκοπός και περίληψη

Αυτό το βήμα βελτιώνει το υπάρχον Blazor Static SSR UI χωρίς αλλαγή της βάσης. Προσφέρει σαφή επιλογή ενεργής επιχείρησης και καταστήματος, καλύτερη εμφάνιση, ασφαλέστερο χειρισμό λαθών και πιο αξιόπιστη πλοήγηση.

Οι υλοποιήσεις περιλαμβάνουν:

- TenantWorkspaceResolver: κεντρικός Application service που αρνείται ρητά μη επιτρεπόμενα TenantId/StoreId.
- Dashboard: εμφάνιση επιλεγμένης επιχείρησης, καταστήματος και κωδικού με δυνατότητα αλλαγής επιλογής.
- Login: ομοιόμορφα ελληνικά κείμενα, Chrome/Edge autofill, μήνυμα rate limiting και ανάκτηση κουμπιού υποβολής.
- AppIcon: SVG εικονίδια χωρίς εξωτερικές βιβλιοθήκες.
- CSS: καλύτερη αντίθεση Light Theme, συμπαγείς κάρτες, προσβασιμότητα focus και mobile προσαρμογές.
- Theme init: απόδοση αποθηκευμένου θέματος πριν από την ανάγνωση CSS.
- UI Login/Logout: μέγιστο μέγεθος request body 8192 bytes.
- Welcome: ανακατεύθυνση ήδη συνδεδεμένου χρήστη στο Dashboard.
- UI version: ανάγνωση από το .NET assembly αντί για διάσπαρτα hardcoded labels.

{% hint style="warning" %}
Το UI παραμένει Development-only. Δεν προκύπτει production readiness, ούτε προστίθεται migration, νέα SQL οντότητα ή CRUD παραστατικών.
{% endhint %}

## Προϋποθέσεις

.NET 10 SDK, Git, ενεργό HTTPS development certificate, SQL Server 2022, User Secrets για AenaonERP_Dev, εφαρμοσμένες migrations και χρήστης admin με Owner membership στην Aenaon Demo. Οι επίσημες εικόνες branding πρέπει να υπάρχουν στο wwwroot/images.

## 1. Ενημέρωση και τεχνικοί έλεγχοι

Μετά το merge στο main:

~~~powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
~~~

Μην κάνεις rebuild όσο εκτελείται άλλο instance του Aenaon.Api. Σταμάτησέ το με Ctrl+C για να μην κλειδώσει DLL.

## 2. Εκκίνηση εφαρμογής

~~~powershell
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε https://localhost:7140/login. Σύνδεση με τον υπάρχοντα Administrator και τον προσωπικό του κωδικό.

## 3. Επιλογή ενεργής επιχείρησης και καταστήματος

Η εφαρμογή επιλέγει το πρώτο επιτρεπόμενο Tenant/Store μόνο όταν δεν έχει δοθεί ρητή επιλογή. Η ενεργή επιλογή εμφανίζεται ξεκάθαρα στην κορυφή του Dashboard.

1. Από τις «Επιχειρήσεις μου» επίλεξε επιχείρηση με ενεργό membership.
2. Από τα «Καταστήματα» επίλεξε το κατάστημα στο οποίο θα εργαστείς.
3. Έλεγξε την ένδειξη «ΕΝΕΡΓΟ ΠΛΑΙΣΙΟ ΕΡΓΑΣΙΑΣ» και τον κωδικό καταστήματος.
4. Η επιλογή μεταφέρεται μόνο μέσω query parameters tenantId και storeId στη διαδρομή /dashboard.
5. Αλλαγή Tenant καθαρίζει την προηγούμενη επιλογή Store· κανένα ξένο Store δεν μπορεί να θεωρηθεί επιλεγμένο.

Μη επιτρεπόμενο TenantId εμφανίζει προειδοποίηση αντί να επιλέγει σιωπηλά άλλη επιχείρηση. Μη επιτρεπόμενο StoreId αφήνει το ενεργό Store κενό και επιτρέπει νέα επιλογή. StoreId χωρίς ρητό TenantId επίσης απορρίπτεται.

{% hint style="danger" %}
Το TenantId/StoreId στο URL αποτελεί **μόνο προσωρινή επιλογή για τη συγκεκριμένη αίτηση** και δεν παρέχει άδεια πρόσβασης. Κάθε μελλοντική SQL εγγραφή, παραστατικό ή άλλη επιχειρησιακή πράξη πρέπει να ξαναελέγχει την ταυτότητα, το membership, τον ρόλο και τη συσχέτιση StoreId→TenantId στον backend.
{% endhint %}

## 4. Light/Dark Theme και οπτική ταυτότητα

Το Logo Login παραμένει αυτό που δόθηκε για σκοτεινό branding. Το Sidebar χρησιμοποιεί τη μικρή εικονική εκδοχή από το favicon. Το Light Theme έχει πιο σκοτεινά δευτερεύοντα κείμενα για αναγνωσιμότητα. Τα πεδία με Chrome/Edge autofill διατηρούν το επιλεγμένο θέμα αντί για μπλε φόντο.

| Συντόμευση | Ενέργεια |
| --- | --- |
| Alt+T | Εναλλαγή φωτεινού/σκοτεινού |
| Alt+L | Login |
| Alt+D | Dashboard |
| Alt+O | Logout όταν διατίθεται |
| Tab / Shift+Tab | Πλοήγηση μεταξύ συνδέσμων/κουμπιών |
| Enter | Επιλογή ή υποβολή φόρμας |

## 5. Σφάλματα, CSRF και rate limiting

Το HTTP login εξακολουθεί να χρησιμοποιεί Identity password verification και antiforgery. Γενικό μήνυμα εμφανίζεται αν αποτύχει η σύνδεση. Η υπέρβαση rate limit στη HTML φόρμα οδηγεί σε σαφή προειδοποίηση, ενώ τα αντίστοιχα API endpoints συνεχίζουν να επιστρέφουν HTTP 429 και Retry-After όπου είναι διαθέσιμο.

Ο browser επαναφέρει το κουμπί Login όταν επιστρέψει από ιστορικό ή όταν η σελίδα δεν ολοκληρώσει τη μετάβαση. Αυτό αφορά μόνο τη συμπεριφορά GUI και δεν ακυρώνει server requests. Η server-side προστασία lockout παραμένει.

Τα HTML login/logout endpoints επιβάλλουν όριο HTTP body 8192 bytes με RequestSizeLimit metadata, επιπλέον της επικύρωσης πεδίων.

## 6. Αρχιτεκτονική, ασφάλεια και logging

- Ο TenantWorkspaceResolver χρησιμοποιεί υπηρεσίες που ελέγχουν το authenticated UserId και κάθε Tenant/Store.
- Τα Server Rendered HTML links είναι χρησιμοποιήσιμα χωρίς JavaScript.
- Το password δεν εισάγεται στη JavaScript, ούτε σε logs.
- Το cookie παραμένει HttpOnly/Secure/SameSite=Strict.
- Η ανάκτηση καταστημάτων γίνεται μόνο μετά από εξουσιοδότηση Tenant.
- Οι αποτυχίες Dashboard καταγράφονται server-side χωρίς επιστροφή SQL secrets.
- Δεν αποθηκεύεται μόνιμο «ενεργό Store» στον client ή στη βάση. Η επιλογή ισχύει για το συγκεκριμένο request.
- Καμία database migration ή destructive operation· δεν απαιτείται database rollback.

## 7. Αντιμετώπιση προβλημάτων

| Σύμπτωμα | Αντιμετώπιση |
| --- | --- |
| Δεν εμφανίζεται Login | Έλεγξε Development environment, HTTPS και ενημερωμένο main |
| Μη διαθέσιμη επιχείρηση | Επίλεξε μία επιχείρηση από τη λίστα και έλεγξε membership |
| Μη διαθέσιμο κατάστημα | Επίλεξε Store της σωστής επιχείρησης |
| Απροσδόκητο μπλε autofill | Έλεγξε ότι φορτώθηκε το τελευταίο aenaon.css |
| Λανθασμένο αρχικό θέμα | Έλεγξε το aenaon-theme-init.js και localStorage permissions |
| Πολλές προσπάθειες σύνδεσης | Περίμενε να ανανεωθεί το rate limiter |
| DLL locked | Σταμάτησε το δεύτερο API πριν από build |
| SQL error | Έλεγξε User Secrets χωρίς να τα κοινοποιήσεις |

## 8. Checklist αποδοχής

- [ ] Επιτυχές GitHub Actions σε Windows/Linux και πραγματικό SQL Server CI.
- [ ] Valid Tenant και Store επιλέγονται και εμφανίζονται σωστά.
- [ ] Άγνωστο Tenant δεν προκαλεί σιωπηλό fallback.
- [ ] Άγνωστο Store δεν επιλέγεται ως ενεργό.
- [ ] Επιβεβαιωμένο Store ανήκει στη σωστή επιχείρηση.
- [ ] Login σε Dark και Light δεν εμφανίζει ανεπιθύμητο autofill χρώμα.
- [ ] SVG icons, branding, labels και focus states είναι συνεπή.
- [ ] Keyboard shortcuts και responsive layouts δουλεύουν.
- [ ] Logout παραμένει προστατευμένο και λειτουργικό.
- [ ] Δεν υπάρχει νέα migration και production exposure.

## 9. Επόμενο βήμα

Πριν αναπτυχθούν πωλήσεις, αγορές ή αποθήκη, κάθε νέο use case πρέπει να χρησιμοποιεί σαφή server-side Tenant/Store authorization και ασφαλές transaction boundary. Παραμένει σε εκκρεμότητα η παραγωγική διαμόρφωση Data Protection keys, email verification, audit και πλήρης accessibility έλεγχος με browser και βοηθητικές τεχνολογίες.
