# Aenaon ERP — 9.4B.2: Back Office UI/UX Refinement

## 1. Σκοπός και σύντομη περιγραφή

Το βήμα **9.4B.2** είναι ελεγχόμενη βελτίωση χρηστικότητας του υπάρχοντος **Blazor Static SSR Back Office**. Προέκυψε από ανασκόπηση πραγματικής καταγραφής 22,36 δευτερολέπτων σε ανάλυση 2560×1380. Επικεντρώνεται στην ευανάγνωστη πλοήγηση, στην ελαχιστοποίηση του περιττού κάθετου χώρου, στα ορατά states του καταλόγου και στη συνεπή πρόσβαση στην επιχείρηση/κατάστημα.

**Δεν προσθέτει** λειτουργίες καταχώρησης, αποθήκης, τιμολόγησης, αλλαγές API ή migrations.

{% hint style="warning" %}
Το Back Office παραμένει **Development-only** και ο κατάλογος **μόνο ανάγνωσης**. Η μεταφορά του workspace indicator στην επάνω μπάρα δεν αποτελεί από μόνη της δικαίωμα πρόσβασης. Κάθε αλλαγή επιχείρησης/καταστήματος συνεχίζει να ελέγχεται στον server.
{% endhint %}

## 2. Προϋποθέσεις

- Το AENAON ERP στο νεότερο commit του branch `feature/9-4b-2-backoffice-ui-refinement`.
- .NET 10 SDK όπως ορίζεται στο `global.json`, Git, PowerShell και browser ενημερωμένης έκδοσης.
- Ήδη λειτουργικό HTTPS Login/Dashboard και κατάλογος (διορθώσεις 9.4B και PR #32).
- Υπάρχουσα βάση Development `AenaonERP_Dev` με εφαρμοσμένα τα προβλεπόμενα migrations.
- Identity χρήστης με δικαίωμα `Read` στο Tenant και, για τις αλλαγές επιλογής, τα υπάρχοντα δικαιώματα.

## 3. Αλλαγές ανά οθόνη

### 3.1 Κοινή επάνω μπάρα — ενεργή επιχείρηση

Η τρέχουσα έγκυρη επιχείρηση και το επιλεγμένο κατάστημα εμφανίζονται στο επάνω μέρος και των δύο οθονών, σε συμπαγή σύνδεσμο:

`Aenaon Demo / Κεντρικό (MAIN)`

Ο σύνδεσμος **«Αλλαγή επιχείρησης ή καταστήματος»** οδηγεί στο σημείο `#workspace-selection` του Dashboard. Εκεί παραμένουν οι ήδη υπάρχουσες φόρμες αλλαγής Tenant/Store με **HTTP POST, έλεγχο CSRF, cookie προστασίας και server-side validation**. Δεν επιτρέπεται απευθείας αλλαγή Tenant από JavaScript ή με μη ελεγμένο query string.

Όταν το workspace δεν είναι έγκυρο ή δεν έχει επιλεχθεί, εμφανίζεται μόνο `Επιλογή επιχείρησης` χωρίς να προβάλλονται στοιχεία ξένου Tenant.

### 3.2 Επισκόπηση

- Αφαιρέθηκε η μεγάλη επαναλαμβανόμενη κάρτα «Ενεργό πλαίσιο εργασίας».
- Αφαιρέθηκαν οι τρεις μεγάλες στατιστικές κάρτες που επαναλάμβαναν πληροφορίες χρήστη/επιχείρησης.
- Οι πραγματικές λίστες επιλογής Tenant/Store **παραμένουν λειτουργικές** κάτω από ευανάγνωστο τίτλο.
- Οι αριθμοί επιχειρήσεων/καταστημάτων εμφανίζονται σε ένα μικρό πληροφοριακό στοιχείο.
- Προστέθηκε άμεσος σύνδεσμος προς `/catalog`, χωρίς να υπονοείται διαθέσιμο CRUD.

### 3.3 Κατάλογος

- Αφαιρέθηκε η μεγάλη λευκή κάρτα ενεργής επιχείρησης (η πληροφορία βρίσκεται στο topbar).
- Ο κύριος τίτλος έγινε σύντομος: `Κατάλογος`. Οι καρτέλες Προϊόντα/Υπηρεσίες/Κατηγορίες παραμένουν διακριτές.
- Περιορίστηκαν τα περιθώρια/ύψη κεφαλίδας, φίλτρων, tab bar και empty state.
- Τα κενά αποτελέσματα εξηγούν αν **δεν έχουν καταχωριστεί είδη** ή αν **δεν βρέθηκαν είδη με την αναζήτηση**.
- Η σελιδοποίηση εμφανίζεται **μόνο** όταν υπάρχει επόμενη σελίδα ή χρειάζεται επιστροφή σε προηγούμενη. Δεν εμφανίζεται άχρηστη σε κενή πρώτη σελίδα.
- Διατηρούνται server-side αναζήτηση προθέματος, ενεργά/ανενεργά, `Kind`, `PageSize`, ασφάλεια, φίλτρα και πίνακες.

### 3.4 Sidebar και σταθερή πλοήγηση

- Οι τρεις μελλοντικές, ανενεργές ενότητες συμπτύχθηκαν στο προσβάσιμο `Επόμενες λειτουργίες`. Δεν παρουσιάζονται ως ενεργοί σύνδεσμοι.
- Σε desktop το sidebar μένει σταθερό με δική του κύλιση και το topbar παραμένει ορατό κατά το scroll.
- Σε στενή οθόνη το sidebar επιστρέφει στη φυσιολογική ροή, ενώ το topbar και η mobile πλοήγηση παραμένουν προσβάσιμα.
- Τα στοιχεία χρήστη, η αλλαγή θέματος και η αποσύνδεση διατηρούν τις υπάρχουσες λειτουργίες.

## 4. Διόρθωση πράσινου πλαισίου

Το αρχικό πρόβλημα προέκυπτε από τη σύζευξη `<FocusOnNavigate ... Selector="h1" />` με τον καθολικό CSS κανόνα `:focus-visible`. Το Blazor θέτει πρόγραμμα εστίασης στον τίτλο κατά την πλοήγηση.

Η διόρθωση:

- **Διατηρεί** το `FocusOnNavigate`, ώστε οι χρήστες screen reader να φτάνουν στον νέο τίτλο.
- Αφαιρεί μόνο το μεγάλο οπτικό `outline` όταν ο τίτλος `h1` έχει `tabindex="-1"` (προγραμματισμένη εστίαση).
- **Δεν αφαιρεί** το εμφανές `:focus-visible` από links, buttons, forms, menu summaries ή χειριστήρια πληκτρολογίου.

{% hint style="success" %}
Το πράσινο περίγραμμα στους διαδραστικούς στόχους **παραμένει σκόπιμα**. Είναι ένδειξη προσβασιμότητας. Το bug αφορούσε το μεγάλο περίγραμμα γύρω από τον μη διαδραστικό τίτλο.
{% endhint %}

## 5. Οργάνωση κώδικα και assets

| Αρχείο | Ρόλος |
| --- | --- |
| `Ui/Components/BackOfficeTopbar.razor` | Προβολή επαληθευμένου workspace και ασφαλής σύνδεσμος αλλαγής |
| `Ui/Components/BackOfficeSidebar.razor` | Συμπαγής navigation με ανενεργές ενότητες σε `details` |
| `Ui/Pages/Dashboard.razor` | Συμπτυγμένη επιλογή επιχείρησης και καταστήματος |
| `Ui/Pages/Catalog.razor` | Νέα ιεραρχία, empty states, pager |
| `wwwroot/css/aenaon.css` | Κεντρικά design tokens, Login και κοινά στοιχεία |
| `wwwroot/css/aenaon-backoffice.css` | Ειδικά στιλ Catalog / Dashboard / responsive shell |
| `Ui/UiAssetVersions.cs` και `Ui/App.razor` | SHA-256 fingerprint και φόρτωση CSS χωρίς stale cache |
| `wwwroot/js/aenaon.js` | Keyboard shortcut για αλλαγή workspace |
| `tests/Aenaon.Api.Tests/` | Regression tests για HTML, ασφάλεια, states και static CSS |

Δεν εισάγεται νέο NuGet, UI framework ή service. Η αρχιτεκτονική Blazor Static SSR και το `CatalogReadService` παραμένουν ίδια.

## 6. Πλήκτρα συντομεύσεων

| Συντόμευση | Λειτουργία |
| --- | --- |
| `Alt+D` | Επισκόπηση |
| `Alt+C` | Κατάλογος |
| `Alt+W` | Μετάβαση σε ασφαλή επιλογή επιχείρησης/καταστήματος |
| `Alt+P` | Προϊόντα όταν βρίσκεστε στον κατάλογο |
| `Alt+S` | Υπηρεσίες όταν βρίσκεστε στον κατάλογο |
| `Alt+G` | Κατηγορίες όταν βρίσκεστε στον κατάλογο |
| `Alt+F` | Εστίαση στην αναζήτηση καταλόγου |
| `Alt+T` | Εναλλαγή φωτεινού/σκούρου θέματος |
| `Alt+O` | Αποσύνδεση μέσω υπάρχοντος POST + CSRF |
| `Tab`, `Shift+Tab`, `Enter`, `Escape` | Πλοήγηση και κλείσιμο μενού |

Αν το λειτουργικό σύστημα δεσμεύει κάποιον συνδυασμό Alt, όλες οι λειτουργίες παραμένουν προσβάσιμες από το UI.

## 7. Εγκατάσταση και εκκίνηση

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git fetch origin
git switch feature/9-4b-2-backoffice-ui-refinement

dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https
```

Ανοίξτε `https://localhost:7140/dashboard` και `https://localhost:7140/catalog` και κάντε `Ctrl+F5` μετά από αλλαγή έκδοσης. Ο browser μπορεί να κρατάει cookies/theme preference· οι επιλογές αυτές δεν χρειάζεται να διαγραφούν.

## 8. Προ- και μετεγχειρητικοί έλεγχοι

**Πριν:**
1. Επιβεβαιώστε τη σωστή Development βάση.
2. Ελέγξτε ενεργή HTTPS session.
3. Βεβαιωθείτε ότι οι existing tenant/store φόρμες λειτουργούν.

**Μετά:**
1. Ελέγξτε desktop (1920×1080 και 1366×768), tablet (π.χ. 820px) και mobile (π.χ. 375px).
2. Δοκιμάστε light/dark, high contrast, zoom 200% και keyboard-only πλοήγηση.
3. Περιηγηθείτε Dashboard → Catalog → Προϊόντα/Υπηρεσίες/Κατηγορίες → Dashboard.
4. Επιλέξτε διαφορετικό **επιτρεπόμενο** κατάστημα· επιβεβαιώστε τον ενημερωμένο indicator και το προστατευμένο cookie.
5. Ελέγξτε ότι ξένο TenantId/StoreId **δεν** αποκαλύπτει εγγραφές και δεν μεταφέρει αυθαίρετα την επιλογή.
6. Ελέγξτε φίλτρα, empty states, σελιδοποίηση και θεματοποίηση μετά από scroll.
7. Ελέγξτε ότι το εστιασμένο `h1` δεν παίρνει μεγάλο πράσινο περίγραμμα, ενώ links και buttons εξακολουθούν να έχουν ορατή ένδειξη focus.

## 9. Troubleshooting

| Πρόβλημα | Αντιμετώπιση |
| --- | --- |
| Παλιό UI μετά από update | Σταματήστε την εφαρμογή, ξανακάντε build και `Ctrl+F5`· ελέγξτε hash των CSS URLs |
| Αδυναμία αλλαγής επιχείρησης | Χρησιμοποιήστε τις φόρμες του Dashboard και επιβεβαιώστε το ενεργό TenantMembership |
| Πράσινο περίγραμμα σε κουμπί/link | **Αναμενόμενο** focus indicator για keyboard accessibility |
| Πράσινο περίγραμμα γύρω από τίτλο `h1` | Ελέγξτε ότι το νέο `aenaon-backoffice.css` φορτώνεται με HTTP 200 |
| Σε browser zoom 150–200% ο τίτλος εμφανίζεται κάτω από το sticky topbar | Ελέγξτε το νεότερο `aenaon-backoffice.css`, το `scroll-padding-block-start` και το `scroll-margin-block-start`, και κάντε `Ctrl+F5` |
| Κενός κατάλογος | Χωρίς seed/CRUD, δεν υπάρχουν ακόμη δεδομένα· διαβάστε το εγχειρίδιο 9.4B |
| Δεν εμφανίζεται η πρώτη σελίδα / Σφάλμα SQL | Ελέγξτε logs, User Secrets και συνδεσιμότητα SQL χωρίς να πειράξετε παραγωγικά δεδομένα |
| Χαμένη εστίαση στο mobile | Χρησιμοποιήστε Tab και mobile menu· καταγράψτε browser/device για regression |

## 10. Logs, ασφάλεια, backup και rollback

Το refinement δεν προσθέτει νέα SQL writes, public endpoints, migrations ή νέα secrets. Οι υπάρχουσες structured καταγραφές εργασιών και αποτυχιών διατηρούνται. Το τελικό UI εξακολουθεί να αποκρύπτει μη επιτρεπτούς Tenants, και το logout παραμένει CSRF-protected.

Για επαναφορά της εμφάνισης, χρησιμοποιήστε `git revert` στο merge commit και επανεκκινήστε την εφαρμογή. **Δεν υπάρχει schema rollback**, καθώς το βήμα είναι αποκλειστικά παρουσίαση/UX. Διατηρήστε τη συνήθη πολιτική backup της βάσης, ιδιαίτερα πριν από μελλοντική καταχώρηση δεδομένων στο 9.4C.

## 11. Πότε χρησιμοποιείται και πότε όχι

**Χρησιμοποιείται** για καθημερινή πλοήγηση στο Back Office και για ανάγνωση, αναζήτηση και έλεγχο καταλόγου.

**Δεν χρησιμοποιείται** για εγγραφή προϊόντων, αλλαγές αποθεμάτων, έκδοση ΑΠΥ/ΤΠΥ, τιμολόγηση, χαρακτηρισμούς εσόδων ή σύνδεση με πάροχο.

## 12. Checklist αποδοχής

- [ ] Windows & Linux locked restore / format / Release build / tests.
- [ ] SQL Server 2022 isolated integration tests χωρίς regression.
- [ ] HTTP tests: active workspace, foreign tenant/store, CSRF logout, CSS fingerprint, paging και empty states.
- [ ] Καμία νέα migration, νέο dependency, δημόσιο Catalog API ή fiscal modification.
- [ ] Χειροκίνητος browser smoke: sticky layout, mobile, light/dark, zoom, keyboard focus.
- [ ] Pull request review και merge **μόνο μετά** από πράσινο CI και ικανοποιητικό οπτικό έλεγχο.

## 13. Συναφή manuals

- [9.4A — Catalog Read-only Preview](34-catalog-read-preview.md)
- [9.4B — Blazor Back Office Catalog](35-blazor-back-office-catalog.md)
- [Fiscal Compatibility Gate](18-mydata-impact-compatibility.md)

Οι 254 εκκρεμείς παρατηρήσεις myDATA και οι ανεξάρτητοι έλεγχοι IMPACT παραμένουν ανεπηρέαστοι. Το βήμα δεν εκτελεί καμία ενέργεια φορολογικής έκδοσης.
