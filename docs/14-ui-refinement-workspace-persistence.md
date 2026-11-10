# Aenaon ERP — Βήμα 8.4.7: UI Refinement & Workspace Persistence

## Σκοπός

Το Βήμα 8.4.7 βελτιώνει το Blazor SSR Login και το Dashboard: πλήρες Light/Dark theme με το σωστό logo, responsive πλοήγηση, Logout στο User Menu και **διατήρηση της επιλογής Tenant/Store** με ασφαλές, κρυπτογραφημένο cookie.

{% hint style="warning" %}
Το UI παραμένει αποκλειστικά Development-only. Δεν εισάγονται νέες migrations ή NuGet εξαρτήσεις, ούτε πραγματοποιούνται εγγραφές στις επιχειρησιακές SQL οντότητες.
{% endhint %}

## Προϋποθέσεις

- .NET 10 SDK, VS Code, SQL Server 2022 και υπάρχουσα AenaonERP_Dev.
- Ενεργά User Secrets, έγκυρος admin με membership στην Aenaon Demo και MAIN.
- HTTPS development certificate και τα επίσημα logos στον φάκελο wwwroot/images.
- Το Pull Request του βήματος έχει γίνει merge στο main.

## 1. Ενημέρωση, build και εκτέλεση

Σταμάτησε το ήδη ενεργό Aenaon.Api με Ctrl+C. Από τη ρίζα του σωστού Git repository:

~~~powershell
git status
git switch main
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε τη διεύθυνση https://localhost:7140/login.

## 2. Login και Themes

1. Στο Light Mode πρέπει και οι δύο πλευρές να είναι φωτεινές: αριστερά sage/mint πράσινο, δεξιά λευκό.
2. Το αριστερό λογότυπο πρέπει να είναι η έκδοση για ανοιχτό φόντο.
3. Στο Dark Mode και οι δύο πλευρές πρέπει να είναι σκούρες και το logo να αλλάζει στην έκδοση για σκούρο φόντο.
4. Η εναλλαγή δεν επηρεάζει τα στοιχεία σύνδεσης ή το session cookie.
5. Επιβεβαίωσε σωστή εμφάνιση σε Chrome/Edge με autofill και σε mobile viewport.

Η προτίμηση εμφάνισης αποθηκεύεται μόνο ως theme σε localStorage και δεν περιέχει προσωπικά στοιχεία.

## 3. Επιλογή ενεργής επιχείρησης και καταστήματος

Μετά το Login:

1. Άνοιξε την Επισκόπηση.
2. Στην κάρτα Επιχειρήσεις επίλεξε μία επιτρεπόμενη επιχείρηση.
3. Στην κάρτα Καταστήματα επίλεξε ένα κατάστημα.
4. Το POST /ui/workspace/select απαιτεί authenticated cookie, έγκυρο CSRF token και ελέγχει το Tenant/Store στον server.
5. Αν γίνει δεκτό, το Dashboard εμφανίζει την ενεργή επιλογή στο «ΕΝΕΡΓΟ ΠΛΑΙΣΙΟ ΕΡΓΑΣΙΑΣ».
6. Πάτησε Επισκόπηση στο Sidebar, ανανέωσε τη σελίδα ή χρησιμοποίησε Alt+D. Η προηγούμενη επιλογή πρέπει να διατηρείται.
7. Αν επιλέξεις άλλη επιχείρηση, ο server επιλύει το πρώτο εξουσιοδοτημένο κατάστημά της, αν υπάρχει.

Το workspace preference βρίσκεται σε κρυπτογραφημένο cookie με όνομα __Host-AenaonERP.Workspace, HttpOnly/Secure/SameSite=Strict και διάρκεια 30 λεπτών. Συνδέεται με το authenticated UserId και δεν διαβάζεται από JavaScript. Login και Logout διαγράφουν την προηγούμενη επιλογή για προστασία από μεταφορά μεταξύ λογαριασμών.

{% hint style="danger" %}
Η επιλογή Workspace αποτελεί **μόνο προτίμηση πλοήγησης**, όχι απόδειξη εξουσιοδότησης. Κάθε μελλοντική εγγραφή ERP πρέπει να ξαναεπαληθεύει χρήστη, membership, permission και τη σχέση Store→Tenant στη business service.
{% endhint %}

### Μη έγκυρες επιλογές

- POST με ξένο/ανύπαρκτο TenantId ή StoreId: HTTP 403, χωρίς νέα αποθήκευση.
- POST χωρίς CSRF token: HTTP 400.
- GET /dashboard με ρητό ξένο TenantId: προειδοποίηση, χωρίς αντικατάσταση της αποθηκευμένης προτίμησης.
- Αν η συμμετοχή χρήστη ανακληθεί μετά την αποθήκευση, το cookie δεν παρακάμπτει το server-side authorization.
- Χωρίς cookie, το Dashboard επιλέγει την πρώτη επιτρεπόμενη επιχείρηση/κατάστημα ως αρχική επιλογή.

## 4. User Menu, Logout και Mobile Menu

Το avatar/username στο επάνω δεξί τμήμα ανοίγει το User Menu. Η ταυτότητα και η ενεργή επιχείρηση εμφανίζονται ως πληροφορίες. Η μόνη ενέργεια λογαριασμού που είναι ήδη υλοποιημένη είναι η Αποσύνδεση.

Η αποσύνδεση χρησιμοποιεί το υπάρχον HTTPS POST /ui/session/logout και έγκυρο CSRF token. Διαγράφει το auth cookie και το workspace preference cookie.

Σε μικρές οθόνες εμφανίζεται το κουμπί «Μενού» με τις υπάρχουσες επιλογές πλοήγησης. Οι υπό ανάπτυξη ενότητες εμφανίζονται ως «Σύντομα» και δεν εκτελούν ανύπαρκτες ενέργειες.

## 5. Keyboard shortcuts

| Συντόμευση | Ενέργεια |
| --- | --- |
| Alt+T | Εναλλαγή Light/Dark |
| Alt+L | Login |
| Alt+D | Dashboard χωρίς απώλεια επιλογής |
| Alt+O | Logout μέσω υπάρχουσας φόρμας |
| Tab / Shift+Tab | Εστίαση σε controls |
| Enter / Space | Ενεργοποίηση μενού ή κουμπιών |
| Escape | Κλείσιμο User Menu και Mobile Menu |

## 6. Αρχιτεκτονική

| Αρχείο | Ευθύνη |
| --- | --- |
| Ui/WorkspacePreferenceService.cs | Προστασία και ανάγνωση/έκδοση/διαγραφή cookie |
| Ui/WorkspaceSelectionEndpoints.cs | POST, antiforgery και server authorization |
| Ui/Pages/Dashboard.razor | Κατάλογοι επιλογής, active context, User Menu και mobile navigation |
| Ui/Pages/Login.razor | Δύο επίσημα logos για πλήρες Light/Dark |
| wwwroot/css/aenaon.css | Branding, responsive UI, μενού και focus states |
| wwwroot/js/aenaon.js | Κλείσιμο αναδυόμενων μενού και keyboard controls |
| tests/Aenaon.Api.Tests/BlazorUiTests.cs | HTTP regression tests |

## 7. Logging και ασφάλεια

Το WorkspacePreferenceService καταγράφει αλλαγή μόνο με UserId, TenantId και StoreId, **όχι** την κρυπτογραφημένη τιμή του cookie. Οι ανεπιτυχείς απόπειρες επιλογής δεν αποκαλύπτουν ξένες επιχειρήσεις. Ο server εφαρμόζει την υπάρχουσα Identity cookie verification και κανόνες SQL Tenant Authorization.

Η προστασία του cookie εξαρτάται από το ASP.NET Core Data Protection key ring. Αλλαγή ή απώλεια κλειδιών μπορεί να ακυρώσει την προτίμηση. Δεν υπάρχει επέκταση δικαιωμάτων μέσω της διατήρησης επιλογής.

## 8. Troubleshooting

| Σύμπτωμα | Έλεγχος |
| --- | --- |
| Το Light Login παραμένει σκούρο | Έλεγξε το fingerprint του aenaon.css και κάνε hard refresh |
| Δεν αλλάζει logo | Έλεγξε τα webp αρχεία και τις κλάσεις logo-for-light/dark |
| Η επιλογή δεν παραμένει | Έλεγξε HTTPS, cookie settings και απόκριση POST |
| HTTP 400 | Έλλειψη/λήξη antiforgery token ή μη έγκυρα πεδία |
| HTTP 403 | Ο χρήστης δεν έχει πρόσβαση σε ζητούμενο Tenant/Store |
| Το μενού λείπει στο κινητό | Έλεγξε viewport και το CSS breakpoint |
| Cookie χάνεται μετά Logout | Αναμενόμενο, για προστασία του χρήστη |
| DLL locked στο build | Κλείσε το ήδη ενεργό Aenaon.Api πριν από επανεκκίνηση |
| Προβλήματα SQL | Έλεγξε User Secrets χωρίς κοινοποίηση κωδικών |

## 9. Checklist αποδοχής

- [ ] Windows/Linux CI και SQL Server integration tests επιτυχή.
- [ ] Το Light/Dark αλλάζει και τις δύο πλευρές του Login και το αντίστοιχο logo.
- [ ] Το User Menu ανοίγει με πληκτρολόγιο και περιέχει προστατευμένο Logout.
- [ ] Οι επιλογές Tenant/Store αποθηκεύονται με CSRF-protected POST.
- [ ] Η επιλογή παραμένει σε Sidebar, refresh και Alt+D.
- [ ] Ξένο Tenant/Store απορρίπτεται χωρίς αλλαγή της επιλογής.
- [ ] Logout διαγράφει και το Workspace cookie.
- [ ] Mobile Menu προσβάσιμο σε μικρά πλάτη.
- [ ] Δεν εμφανίζονται στοιχεία διαπιστευτηρίων σε logs ή JavaScript.
- [ ] Δεν υπάρχει νέα migration ή schema change.

## 10. Συντήρηση, rollback και επόμενα βήματα

Σε περίπτωση οπτικού ή λειτουργικού regression μπορεί να γίνει Git revert του PR και επανεκκίνηση API, χωρίς SQL rollback. Η έκδοση παραμένει Development-only και χρειάζεται staging, παραγωγική προστασία Data Protection keys, email validation και εξειδικευμένο access control για τις επόμενες επιχειρησιακές ενότητες.
