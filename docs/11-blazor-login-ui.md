# Aenaon ERP — Blazor Login UI & Dashboard (Βήμα 8.4)

## Σκοπός

Το Βήμα 8.4 εισάγει την πρώτη web εφαρμογή του Aenaon ERP, σε **Blazor Static Server-Side Rendering (SSR)**, μέσα στο ίδιο ASP.NET Core API. Η λύση επαναχρησιμοποιεί ακριβώς το Identity Authentication, τα HTTPS cookies, το antiforgery και την εξουσιοδότηση ανά Tenant των προηγούμενων βημάτων.

Στόχος είναι ο πρώτος επαγγελματικός ιστός εφαρμογής: επώνυμο Login, αρχικό Dashboard, επιλογή επιχείρησης, κατάλογος καταστημάτων, ασφάλεια και responsive Light/Dark θέμα.

## Αρχιτεκτονικές αποφάσεις

- **Ίδιο origin με το API:** τα cookies λειτουργούν χωρίς CORS, reverse proxy ή τρίτο service.
- **Blazor SSR χωρίς SignalR circuit:** η σύνδεση αποστέλλεται με κανονική HTTPS φόρμα στον server, χωρίς JavaScript κωδικού.
- **Authentication μέσω υπάρχοντος Identity:** οι φόρμες εκδίδουν το ίδιο ασφαλές cookie μέσω του κοινόχρηστου SessionCookieIssuer.
- **Server-side authorization:** κανένας TenantId δεν αρκεί από μόνος του. Πριν προβληθούν τα καταστήματα, το TenantWorkspaceService επιβάλλει Read δικαίωμα μέσω TenantAuthorizationService.
- **Ελάχιστη JavaScript:** μόνο εναλλαγή εμφάνισης κωδικού, αποθήκευση theme preference και keyboard shortcuts.
- **Development-only:** όπως το Βήμα 8.3, ούτε οι σελίδες ούτε τα HTML auth endpoints είναι διαθέσιμα σε production.
- **Χωρίς νέα migrations ή τρίτα UI packages.**

## Επίσημο branding Aenaon ERP by MoonHard

Χρησιμοποιούνται τα δύο λογότυπα που δόθηκαν:

| Αρχείο | Περιβάλλον |
| --- | --- |
| aenaon-logo-dark.webp | Σκούρο φόντο, brand panel, sidebar, dark theme |
| aenaon-logo-light.webp | Ανοιχτό φόντο, light theme, welcome page |
| aenaon-favicon.png | Το κυκλικό σύμβολο A στο tab του browser |

Τα στοιχεία σχεδίασης ακολουθούν το σκούρο emerald, mint green και metal/ivory της οπτικής ταυτότητας.

{% hint style="warning" %}
**Εγκατάσταση λογοτύπων:** Επειδή η τρέχουσα σύνδεση GitHub υποστηρίζει μόνο αποστολή UTF-8 αρχείων κώδικα, τα binary WebP/PNG δεν περιλαμβάνονται αυτόματα στο GitHub Pull Request. Κατέβασε το συμπληρωματικό ZIP που παραδόθηκε στη συνομιλία και αποσυμπίεσέ το στον ριζικό φάκελο του repository. Περιλαμβάνει ακριβώς το src/Aenaon.Api/wwwroot/images/ με τις τρεις εικόνες και το branding/source/ με τα αρχικά PNG. Πριν από merge, πρόσθεσε και κάνε commit αυτά τα binary αρχεία στο ίδιο feature branch ώστε η εφαρμογή να δημοσιευτεί με τα assets της.
{% endhint %}

## Προϋποθέσεις

- .NET 10 LTS SDK, Visual Studio Code, SQL Server 2022.
- Δύο εφαρμοσμένες migrations στη βάση AenaonERP_Dev.
- Ενεργά .NET User Secrets και HTTPS development certificate.
- Επιτυχή Βήματα 8.1–8.3, συμπεριλαμβανομένου HTTP Login/Logout.
- Δικαιώματα για τη δοκιμαστική Aenaon Demo (TenantId 9698df79-3c31-41c9-b292-b7240c7265fb) και το MAIN (StoreId c697b9c3-e221-4156-b533-f54dee27d979).

## 1. Ενσωμάτωση κώδικα και εικόνων

Επίλεξε ή κατέβασε το feature branch σύμφωνα με το αντίστοιχο Pull Request. Αποσυμπίεσε το Branding ZIP **στο root του repository** και επιβεβαίωσε:

~~~powershell
Test-Path 'src/Aenaon.Api/wwwroot/images/aenaon-logo-dark.webp'
Test-Path 'src/Aenaon.Api/wwwroot/images/aenaon-logo-light.webp'
Test-Path 'src/Aenaon.Api/wwwroot/images/aenaon-favicon.png'
~~~

Κάθε εντολή πρέπει να επιστρέψει True. Πρόσθεσε τις εικόνες στο Git και κάνε commit πριν από merge:

~~~powershell
git add src/Aenaon.Api/wwwroot/images branding/source
git commit -m "assets(branding): add official Aenaon ERP logos"
git push
~~~

Το GitHub PR πρέπει να περιέχει **κώδικα και τα τρία images** πριν γίνει merge.

## 2. Build και tests

~~~powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet dev-certs https --trust
~~~

Μετά την ενσωμάτωση στο main οι ίδιες εντολές πρέπει να περάσουν και τοπικά. Τα GitHub Actions ελέγχουν Windows, Linux και isolated SQL Server 2022 χωρίς να συνδέονται στην πραγματική AenaonERP_Dev.

## 3. Εκκίνηση

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε στον browser:

- https://localhost:7140/ — σελίδα καλωσορίσματος.
- https://localhost:7140/login — επώνυμη φόρμα σύνδεσης.
- https://localhost:7140/dashboard — προστατευμένο Dashboard.

## 4. Πρώτη σύνδεση

1. Άνοιξε την οθόνη Login από τον browser.
2. Συμπλήρωσε username **admin** και τον ήδη δημιουργημένο προσωπικό κωδικό.
3. Επίλεξε **Σύνδεση** ή πάτησε Enter.
4. Το API επαληθεύει τον κωδικό και τις συμμετοχές και εκδίδει Secure HttpOnly session cookie.
5. Μετά το login μεταφέρεσαι στο **/dashboard**.
6. Επιβεβαίωσε ότι εμφανίζεται η επιχείρηση **Aenaon Demo** και το κατάστημα **MAIN — Κεντρικό**.

{% hint style="info" %}
Το password δεν περνά από JavaScript: η φόρμα υποβάλλεται απευθείας στον server μέσω HTTPS. Η αποτυχία εμφανίζει γενικό μήνυμα χωρίς να αποκαλύπτει αν ένας λογαριασμός υπάρχει.
{% endhint %}

## 5. Dashboard και δικαιώματα

Το Dashboard δείχνει πραγματικά στοιχεία από την SQL: αριθμό επιχειρήσεων στις οποίες ανήκει ο χρήστης, αριθμό καταστημάτων στην επιλεγμένη επιχείρηση, ρόλο και ενεργή συνεδρία.

Αν ο χρήστης ανήκει σε πολλές επιχειρήσεις, επιλέγει την αντίστοιχη από τη λίστα. Το query parameter tenantId λειτουργεί μόνο ως **επιλογή** και δεν χορηγεί πρόσβαση: το σύστημα επαληθεύει το membership και φιλτράρει καταστήματα με TenantId στη βάση.

Οι μελλοντικές λειτουργίες πωλήσεων, αποθήκης και ρυθμίσεων εμφανίζονται με την ένδειξη **Σύντομα**. Δεν δημιουργούνται ψευδή KPI ή ανενεργά κουμπιά που υπονοούν λειτουργική υποστήριξη.

## 6. Light/Dark θέμα και συντομεύσεις

Η εφαρμογή αρχικά ακολουθεί τη ρύθμιση φωτεινού/σκούρου θέματος του λειτουργικού και διαθέτει ξεχωριστό κουμπί Theme. Η επιλογή αποθηκεύεται τοπικά στον browser χωρίς να καταγράφει στοιχεία ταυτότητας.

| Συντόμευση | Ενέργεια |
| --- | --- |
| Enter (σε φόρμα Login) | Σύνδεση |
| Alt + T | Εναλλαγή Light/Dark |
| Alt + L | Οθόνη Login |
| Alt + D | Dashboard |
| Alt + O | Logout όταν εμφανίζεται η σχετική φόρμα |
| Tab / Shift + Tab | Μετακίνηση μεταξύ στοιχείων |
| Space / Enter σε κουμπί | Ενεργοποίηση του επιλεγμένου κουμπιού |

Το κουμπί «Εμφάνιση» δίπλα στον κωδικό εναλλάσσει τον τύπο password/text χωρίς να αποθηκεύει ή να αποστέλλει τον κωδικό.

## 7. Logging και ασφάλεια

- Το UI ενεργοποιείται **μόνο σε Development**.
- Οι Login/Logout φόρμες απαιτούν έγκυρο antiforgery token.
- Τα αιτήματα Login και Logout υπόκεινται στο ίδιο rate limiting και lockout των προηγούμενων βημάτων.
- Το Dashboard απαιτεί authenticated cookie με επανέλεγχο security stamp.
- Η λίστα καταστημάτων εκδίδεται μόνο μετά από Read authorization του server.
- Όλα τα UI δεδομένα περνούν από το Razor output encoding.
- Τα session cookies παραμένουν HttpOnly/Secure/SameSite Strict και δεν αποθηκεύονται σε localStorage.
- Το μόνο τοπικό browser preference είναι το χρώμα θέματος.
- Το logging παρακολουθεί επιτυχείς/απορριφθείσες συνδέσεις και αποτυχίες ανάκτησης χωρίς password, cookie ή CSRF token.

{% hint style="danger" %}
Μη χρησιμοποιήσεις Development endpoints σε δημόσιο URL ή παραγωγική υποδομή. Μη χρησιμοποιήσεις SQL admin account ως κωδικό του ERP. Μη δημοσιεύεις screenshots που περιλαμβάνουν passwords, cookie values ή authorization headers.
{% endhint %}

## 8. Troubleshooting

| Πρόβλημα | Διάγνωση |
| --- | --- |
| 404 στο /login | Δεν εκτελείται Development ή λείπει η ενημέρωση του κώδικα |
| Τα logos δεν εμφανίζονται | Δεν έχει αποσυμπιεστεί το ZIP στο σωστό src/Aenaon.Api/wwwroot/images |
| 400 στο Login | Πρόβλημα antiforgery token ή απουσία HTTPS |
| Επιστροφή στο Login με μήνυμα | Μη έγκυρα στοιχεία, lockout ή λογαριασμός χωρίς membership |
| Dashboard επιστρέφει στο Login | Δεν υπάρχει έγκυρη ή ενεργή συνεδρία |
| Δεν εμφανίζεται κατάστημα | Έλεγξε SELECT στη βάση, την αντιστοίχιση Store.TenantId και το membership |
| Η αλλαγή θέματος δεν μένει | Ο browser πιθανόν εμποδίζει την αποθήκευση localStorage |
| Πρόβλημα Data Protection | Έλεγξε key ring και HTTPS host, όχι τα secrets σε output |
| CSS δεν φορτώνει | Έλεγξε static files, wwwroot και τη διεύθυνση /css/aenaon.css |

Δεν αλλάζει το schema, δεν προστίθενται migrations και δεν απαιτείται database rollback. Μπορείς να επανέλθεις στην προηγούμενη έκδοση API με ασφαλές Git revert του PR, χωρίς τροποποίηση των πραγματικών δεδομένων.

## 9. Checklist

- [ ] Τα δύο λογότυπα αντιστοιχούν σωστά σε dark/light φόντο.
- [ ] Τα τρία web assets βρίσκονται στο wwwroot/images και είναι tracked στο Git.
- [ ] Επιτυχές Release build και CI.
- [ ] Το Login είναι responsive σε desktop και mobile.
- [ ] Ο κωδικός δεν καταγράφεται ούτε περνά από JavaScript.
- [ ] Εσφαλμένος κωδικός δίνει γενικό μήνυμα.
- [ ] Anonymous /dashboard οδηγεί στο Login.
- [ ] Επιτυχές login εμφανίζει Aenaon Demo και MAIN.
- [ ] Ξένο TenantId δεν εμφανίζει ξένα δεδομένα.
- [ ] Light/Dark και keyboard shortcuts λειτουργούν.
- [ ] Logout ανακαλεί τη συνεδρία.
- [ ] Production δεν εκθέτει το Development UI.

## 10. Επόμενο βήμα

Μετά τον πρώτο UI έλεγχο μπορεί να προστεθεί πλήρες Shell / Navigation Architecture, επιβεβαίωση email και production hosting configuration. Οι ενότητες πωλήσεων και αποθήκης θα υλοποιούνται μόνο μαζί με τον αντίστοιχο ασφαλή backend, SQL σχεδιασμό και επιχειρησιακούς κανόνες.
