# Aenaon ERP — Βήμα 9.4B: Blazor Back Office Catalog

## 1. Σκοπός

Το βήμα 9.4B προσθέτει την πρώτη πραγματική **οθόνη Back Office καταλόγου** στο υπάρχον Aenaon ERP. Επιτρέπει στους πιστοποιημένους χρήστες να προβάλλουν με ασφάλεια τα **προϊόντα, τις υπηρεσίες και τις κατηγορίες της επιχείρησής τους**, χωρίς να μεταβάλλουν δεδομένα.

Η οθόνη εξυπηρετείται από το ίδιο ASP.NET Core / Blazor **Static Server-Side Rendering (SSR)** περιβάλλον που ήδη χρησιμοποιούν το Login και το Dashboard. Δεν εγκαθίσταται νέο JavaScript framework, τρίτο UI component package, public catalog API ή client-side store.

{% hint style="warning" %}
**Μόνο ανάγνωση.** Δεν υποστηρίζονται προσθήκη, επεξεργασία, διαγραφή, μεταβολές αποθεμάτων, τιμολογιακών πολιτικών, συντελεστών ΦΠΑ ή έκδοση παραστατικών. Οι 254 εκκρεμότητες myDATA και οι 6 ανεξάρτητοι έλεγχοι IMPACT δεν θεωρούνται εγκεκριμένοι.
{% endhint %}

## 2. Προϋποθέσεις

- Πρόσβαση στο repository [MoonHard13/AENAON-ERP](https://github.com/MoonHard13/AENAON-ERP).
- Εγκατεστημένο **σταθερό .NET 10 SDK** συμβατό με το `global.json`, Visual Studio / VS Code και Git.
- Υπάρχων **SQL Server 2022** και Development βάση `AenaonERP_Dev`, με τις ήδη εφαρμοσμένες πέντε migrations.
- Έγκυρη διαμόρφωση σύνδεσης SQL μέσω **User Secrets**, χωρίς κωδικούς σε κώδικα ή Git.
- Υπάρχων Identity λογαριασμός (`admin` ή άλλος) με συμμετοχή `Owner` ή `Member` στον επιλεγμένο Tenant.
- Πρόσβαση στο τοπικό HTTPS endpoint, π.χ. `https://localhost:7140`.

{% hint style="info" %}
Το βήμα δεν προσθέτει migration και **δεν εισάγει δείγματα προϊόντων ή κατηγοριών**. Άδειοι πίνακες `dbo.CatalogCategories` και `dbo.CatalogItems` εμφανίζονται ως κενά αποτελέσματα — αυτό είναι φυσιολογικό.
{% endhint %}

## 3. Εγκατάσταση και εκκίνηση

Από PowerShell στον υπολογιστή ανάπτυξης:

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git switch main
git pull --ff-only origin main

dotnet --version
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
```

Εφόσον το PR δεν έχει συγχωνευθεί, ελέγχουμε πρώτα το αντίστοιχο feature branch αντί του `main`. Η εφαρμογή **δεν εφαρμόζει migrations κατά την εκκίνηση**.

Ανοίγουμε:

- `https://localhost:7140/login` — είσοδος με τον υπάρχοντα λογαριασμό.
- `https://localhost:7140/dashboard` — επισκόπηση και επιλογή επιχείρησης/καταστήματος.
- `https://localhost:7140/catalog` — κατάλογος Back Office.

## 4. Πλοήγηση και αναλυτική χρήση

### 4.1 Ενεργή επιχείρηση

Η επιλεγμένη επιχείρηση και το κατάστημα εμφανίζονται στην κορυφή της σελίδας. Η επιλογή διατηρείται από το ήδη υπάρχον **κρυπτογραφημένο, χρήστη-δεσμευμένο workspace cookie** και επαληθεύεται εκ νέου στον server.

Η οθόνη καταλόγου καλύπτει **ολόκληρο τον Tenant**: η αναφορά στο επιλεγμένο κατάστημα είναι πληροφοριακή, όχι φίλτρο αποθήκης. Αν δεν υπάρχουν καταστήματα, η προβολή επιχείρησης εξακολουθεί να είναι διαθέσιμη.

Αν ζητηθεί ρητά άγνωστο `tenantId` ή `storeId`, η οθόνη εμφανίζει μήνυμα άρνησης. **Δεν μεταφέρεται σιωπηλά** σε άλλο Tenant ή κατάστημα και δεν πραγματοποιείται ανάγνωση καταλόγου.

### 4.2 Καρτέλα «Προϊόντα»

Εμφανίζει αποκλειστικά εγγραφές `CatalogItemKind.Product`, με:

| Στήλη | Περιγραφή |
| --- | --- |
| Κωδικός | Μοναδικός κωδικός προϊόντος μέσα στον Tenant |
| Περιγραφή | Ονομασία προϊόντος |
| Κατηγορία | Όνομα της κατηγορίας μέσα στον ίδιο Tenant |
| Τύπος | Προϊόν |
| Απόθεμα | Υπάρχουσα πληροφοριακή ένδειξη `TracksStock` |
| Κατάσταση | Ενεργό ή ανενεργό |

**Προσοχή:** `TracksStock=Ναι` δεν είναι πραγματικό υπόλοιπο ούτε πραγματοποιεί κίνηση αποθήκης.

### 4.3 Καρτέλα «Υπηρεσίες»

Εμφανίζει αποκλειστικά `CatalogItemKind.Service`. Ο τύπος εφαρμόζεται **στο SQL query πριν από τη σελιδοποίηση**. Οι υπηρεσίες δεν παρακολουθούν απόθεμα, σύμφωνα με τον υπάρχοντα κανόνα domain/SQL.

### 4.4 Καρτέλα «Κατηγορίες»

Εμφανίζει τον κωδικό, την περιγραφή και την ενεργή/ανενεργή κατάσταση κατηγοριών του Tenant. Η κατάσταση των κατηγοριών δεν μεταβάλλεται από αυτή την οθόνη.

### 4.5 Αναζήτηση και φίλτρα

| Πεδίο | Τι κάνει |
| --- | --- |
| Κωδικός ή περιγραφή | **Αρχή** κωδικού ή περιγραφής, έως 80 χαρακτήρες· δεν είναι αναζήτηση «περιέχει» |
| Εγγραφές | Επιλογή 25, 50 ή 100 αποτελεσμάτων ανά σελίδα |
| Εμφάνιση ανενεργών | Συμπεριλαμβάνει εγγραφές που δεν είναι ενεργές |
| Αναζήτηση | Εκτελεί ασφαλές HTTP `GET` με τα φίλτρα, χωρίς SQL writes |
| Καθαρισμός | Επαναφέρει την καρτέλα σε προεπιλεγμένα φίλτρα |
| Προηγούμενη / Επόμενη | Πλοήγηση σελίδων, διατηρώντας τα φίλτρα |

Η σελίδα επιτρέπεται από **1 έως 1000**. Η ένδειξη «επόμενη» υπολογίζεται με μία επιπλέον εγγραφή (`pageSize+1`) χωρίς `COUNT(*)`. Δεν εμφανίζεται συνολικός αριθμός αποτελεσμάτων, επειδή δεν έχει υπολογιστεί από SQL.

Οι καρτέλες διατηρούν τα ενεργά φίλτρα και επιστρέφουν στην πρώτη σελίδα. Το `GET` είναι κατάλληλο για bookmarks και αποφεύγει πρόσθετα μη απαραίτητα endpoints.

### 4.6 Loading, κενά αποτελέσματα και σφάλματα

- **Loading:** το SSR μπορεί να εμφανίσει μήνυμα φόρτωσης μέσω `[StreamRendering]`, χωρίς να παγώνει η πλοήγηση.
- **Empty:** όταν δεν υπάρχουν καταχωρήσεις ή δεν ταιριάζουν τα φίλτρα, εμφανίζεται πληροφοριακό μήνυμα.
- **Invalid query:** απορρίπτεται χωρίς query καταλόγου.
- **Access denied:** δεν εμφανίζονται στοιχεία μη εξουσιοδοτημένου Tenant.
- **Database failure:** εμφανίζεται ασφαλές γενικό μήνυμα στον χρήστη· τα τεχνικά στοιχεία βρίσκονται αποκλειστικά στα logs.

## 5. Δικαιώματα και ασφάλεια

1. Ο χρήστης αυθεντικοποιείται μέσω του υπάρχοντος **HTTPS secure session cookie**.
2. Το `TenantWorkspaceResolver` επαληθεύει τη δυνατότητα επιλογής του Tenant / Store.
3. Το κοινό `CatalogReadService` ελέγχει εκ νέου το δικαίωμα `TenantPermission.Read` **πριν** το SQL store.
4. Το `SqlCatalogReadStore` εφαρμόζει υποχρεωτικό `Where(TenantId == ...)`, `AsNoTracking` και προβολή μόνο επιτρεπτών DTO πεδίων.
5. Για προϊόντα/υπηρεσίες υπάρχει επίσης server-side `Kind` predicate και σύνθετο join κατηγορίας κατά `TenantId` και `CategoryId`.
6. Η έξοδος Razor HTML-encodes δυναμικά πεδία, χωρίς `MarkupString` ή ανεξέλεγκτο HTML rendering.

Ο **Owner** και ο **Member** διαθέτουν μόνο δυνατότητα προβολής στην παρούσα οθόνη. Δεν παραχωρείται `Manage` / CRUD. Το Logout παραμένει **POST με CSRF protection**. Οι αναζητήσεις είναι `GET` και δεν μεταβάλλουν δεδομένα.

Το Back Office διατίθεται **μόνο στο Development**, όπως το υπάρχον Login/Dashboard. Σε Production δεν γίνεται mapping της Razor UI. Αυτή η τεχνική περίμετρος δεν πρέπει να παρακαμφθεί χωρίς νέο έλεγχο ασφάλειας.

## 6. Keyboard shortcuts

| Συντόμευση | Ενέργεια |
| --- | --- |
| `Alt+D` | Επισκόπηση |
| `Alt+C` | Κατάλογος |
| `Alt+P` | Προϊόντα (μέσα στον κατάλογο) |
| `Alt+S` | Υπηρεσίες (μέσα στον κατάλογο) |
| `Alt+G` | Κατηγορίες (μέσα στον κατάλογο) |
| `Alt+F` | Εστίαση στην αναζήτηση |
| `Alt+T` | Εναλλαγή θέματος |
| `Alt+O` | Αποσύνδεση με CSRF-protected form |
| `Tab` / `Shift+Tab` | Περιήγηση στα στοιχεία φόρμας |
| `Enter` | Εκτέλεση αναζήτησης / ενεργοποίηση εστιασμένου συνδέσμου |
| `Escape` | Κλείσιμο μενού χρήστη / πλοήγησης |

{% hint style="info" %}
Ορισμένοι συνδυασμοί `Alt` ενδέχεται να δεσμεύονται από το λειτουργικό σύστημα ή τον browser. Όλες οι λειτουργίες είναι πάντοτε προσβάσιμες και με συνηθισμένο πληκτρολόγιο ή ποντίκι.
{% endhint %}

## 7. CLI — κοινή επιχειρησιακή υπηρεσία

Για τεχνικό έλεγχο από PowerShell λειτουργεί ο ήδη υπάρχων **Development-only** CLI reader του 9.4A. Χρησιμοποιεί το ίδιο `CatalogReadService` και πραγματική επαλήθευση κωδικού:

**Κατηγορίες**

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --preview-catalog --username admin --tenant-id "9698df79-3c31-41c9-b292-b7240c7265fb" --confirm-database AenaonERP_Dev --entity categories
```

**Είδη (προϊόντα και υπηρεσίες)**

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --preview-catalog --username admin --tenant-id "9698df79-3c31-41c9-b292-b7240c7265fb" --confirm-database AenaonERP_Dev --entity items --search SKU --page 1 --page-size 25 --include-inactive
```

Το CLI **δεν προσφέρει ακόμη ξεχωριστή παράμετρο `--kind`**. Ο διαχωρισμός στις καρτέλες του UI χρησιμοποιεί το νέο προαιρετικό `Kind` filter της κοινής υπηρεσίας. Για βοήθεια: `dotnet run --project src/Aenaon.Api --launch-profile https -- --help`.

Ο κωδικός ζητείται διαδραστικά. Δεν προσθέτουμε password σε command line, scripts ή logs.

## 8. Έλεγχοι πριν και μετά τη χρήση

**Πριν:** επιβεβαιώνουμε ότι έχουμε ανοίξει την `AenaonERP_Dev`, ότι η τελευταία migration είναι ήδη εφαρμοσμένη, ότι το HTTPS session ισχύει και ότι η σωστή επιχείρηση είναι επιλεγμένη.

**Μετά:** επαληθεύουμε ότι τα στοιχεία αντιστοιχούν μόνο στον επιτρεπτό Tenant, ότι τα φίλτρα δεν μεταβάλλουν εγγραφές και ότι δεν καταχωρήθηκαν δεδομένα τιμολόγησης ή αποθήκης.

### Checklist αποδοχής

- [ ] Locked restore και Release build των project.
- [ ] `dotnet format` χωρίς εκκρεμότητες.
- [ ] HTTP tests για anonymous/authorized, ξένο Tenant/Store, query bounds, φίλτρα, paging, XSS και Production 404.
- [ ] Application tests για `Kind` και έλεγχο άκυρων enum τιμών.
- [ ] Real SQL Server integration για `Kind`, tenant join και σωστό `HasMore`.
- [ ] Οπτική επιθεώρηση laptop/tablet/mobile, light/dark, High DPI και keyboard-only πλοήγησης.
- [ ] `AenaonERP_Dev`: πέντε migrations, **καμία νέα**.
- [ ] Καμία αλλαγή σε ΑΑΔΕ/IMPACT mapping, provider calls ή έκδοση παραστατικών.

Εντολές ελέγχου:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Οι πραγματικοί SQL integration έλεγχοι εκτελούνται μόνο στον **απομονωμένο SQL Server 2022** του GitHub Actions CI, όχι στην παραγωγική βάση.

## 9. Troubleshooting

| Σύμπτωμα | Ενέργεια |
| --- | --- |
| Ανακατεύθυνση στο Login | Το session έχει λήξει· συνδεόμαστε ξανά |
| Μήνυμα «επιχείρηση δεν είναι διαθέσιμη» | Επιστροφή στο Dashboard, επιλέγουμε εξουσιοδοτημένο Tenant |
| Μήνυμα για κατάστημα | Επιλέγουμε επιτρεπόμενο Store στο Dashboard |
| Κενά προϊόντα/κατηγορίες | Ελέγχουμε αν υπάρχουν εγγραφές και αν το φίλτρο είναι πρόθεμα |
| «Μη έγκυρα φίλτρα» | Σελίδα 1–1000, μέγεθος 25/50/100, αναζήτηση έως 80 χαρακτήρες |
| Γενικό σφάλμα φόρτωσης | Ελέγχουμε `--check-database`, SQL Server, User Secrets και τα logs |
| 404 στο `/catalog` | Ελέγχουμε ότι τρέχει το feature σε **Development** και όχι Production |
| Το `/catalog` μένει στο «Φόρτωση καταλόγου…» | Βεβαιωνόμαστε ότι το `blazor.web.js` φορτώνεται με 200 στον browser (DevTools → Network), ότι τρέχει η νεότερη έκδοση και κάνουμε `Ctrl+F5`· αν το αίτημα παραμένει ανοικτό, ελέγχουμε SQL/logs |
| Το shortcut δεν λειτουργεί | Δοκιμάζουμε click/Tab· ο browser μπορεί να δεσμεύει το Alt |

## 10. Logs, ασφάλεια δεδομένων, backup και rollback

Τα δομημένα logs σημειώνουν την πρόσβαση (`UserId`, `TenantId`, καρτέλα, page, page-size), την άρνηση πρόσβασης και τεχνικά σφάλματα. **Δεν αποθηκεύονται** αναζητήσεις, κωδικοί πρόσβασης, credentials SQL ή αποτελέσματα καταλόγου μέσα στα νέα logs.

Η λειτουργία είναι μη καταστροφική: δεν απαιτείται ιδιαίτερο backup για την προβολή. Διατηρούμε ωστόσο την πολιτική backup της Development βάσης πριν από οποιαδήποτε μελλοντική migration/CRUD.

**Rollback:** με `git revert` του merge commit ή επαναφορά στην προηγούμενη έκδοση εφαρμογής. Δεν υπάρχει νέα DB migration προς αναστροφή. Δεν χρησιμοποιούμε `git reset --hard` σε εργασίες με τοπικές αλλαγές χωρίς έλεγχο.

## 11. Πότε χρησιμοποιείται / πότε όχι

**Χρησιμοποιείται:** τεχνική επαλήθευση αρχικού καταλόγου, ανάγνωση κωδικών, έλεγχος ενεργών/ανενεργών εγγραφών, διάκριση προϊόντων και υπηρεσιών στον σωστό Tenant.

**Δεν χρησιμοποιείται:** καταχώρηση ειδών, παραγγελιοληψία, έκδοση ΑΠΥ/ΤΠΥ, ορισμός ΦΠΑ, διαχείριση αποθήκης, έγκριση παραστατικών ή myDATA classifications.

{% hint style="danger" %}
Ο λειτουργικός διαχωρισμός `Product` / `Service` **δεν αποτελεί φορολογική ταξινόμηση** και δεν επιτρέπει αυτόματη χρήση σε παραστατικό.
{% endhint %}

## 12. Συμβατότητα και πηγές

- [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata).
- [IMPACT — Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0).
- [Προηγούμενο βήμα 9.4A — Catalog Read Preview](34-catalog-read-preview.md).
- [Aenaon Fiscal Compatibility Gate](18-mydata-impact-compatibility.md).

Το 9.4B **δεν αλλάζει φορολογικά συμβόλαια**, δεν αποδέχεται τις 254 εκκρεμείς παρατηρήσεις και δεν επιλύει τους 6 μη ολοκληρωμένους ελέγχους IMPACT. Οι επόμενες εργασίες δημιουργίας/επεξεργασίας καταλόγου χρειάζονται ξεχωριστά αιτήματα, επιβεβαίωση δικαιωμάτων `Manage`, validation και regression tests.
