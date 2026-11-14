# AENAON ERP — 9.5C.2: Barcode/EAN και αναλυτική περιγραφή ειδών

**Έκδοση τεκμηρίωσης:** 14/11/2026  
**Κατάσταση:** Feature branch προς review / Development-only.  
**Προαπαιτούμενο:** merged PR #51 (9.5C.1) και ενημερωμένο ΑΕΝΑΟΝ ERP με τη migration `20261114151000_CatalogItemDetails`.

{% hint style="warning" %}
Η νέα migration **δεν εφαρμόζεται αυτόματα** στη βάση. Πρώτα dry-run, έγκυρο backup και ανασκόπηση SQL script. Η τεκμηρίωση περιγράφει υλοποιημένο κώδικα στο PR· δεν αποτελεί απόδειξη ότι έγινε build, CI, εκτέλεση migrations ή δοκιμή browser στη δική σας εγκατάσταση.
{% endhint %}

## 1. Σκοπός

Το βήμα **9.5C.2** επεκτείνει την καρτέλα **προϊόντος** και **υπηρεσίας** με δύο προαιρετικά εμπορικά πεδία, χωρίς αλλαγή στους εσωτερικούς κωδικούς, κατηγορίες, τιμοκαταλόγους, κανόνες ΦΠΑ ή φορολογικά mappings.

Η διαχείριση υποστηρίζεται από το υπάρχον **Blazor Back Office**, το Development **CLI** και το κοινό `CatalogWriteService`. Δεν υπάρχουν νέα NuGet packages, δημόσια endpoints έκδοσης, Android/POS ή Excel import.

## 2. Πεδία και κανόνες

| Πεδίο | Τύπος SQL | Περιγραφή / έλεγχος |
| --- | --- | --- |
| `Code` | `nvarchar(40) NOT NULL` | **Υπάρχων** εσωτερικός κωδικός SKU, μοναδικός ανά επιχείρηση, δεν τροποποιείται από το barcode |
| `Barcode` | `nvarchar(40) NULL` | Προαιρετικό, μοναδικό ανά `TenantId` όταν δεν είναι κενό, με trimmed uppercase κανονικοποίηση |
| `LongDescription` | `nvarchar(2000) NULL` | Προαιρετικό απλό Unicode κείμενο, έως 2.000 χαρακτήρες, επιτρέπει νέες γραμμές |
| `Name` | `nvarchar(200) NOT NULL` | **Υπάρχουσα** υποχρεωτική σύντομη περιγραφή, παραμένει ανεξάρτητη |

### Μορφές barcode

- **GTIN-8, UPC-A / GTIN-12, EAN-13 / GTIN-13, GTIN-14:** όταν το πεδίο αποτελείται **αποκλειστικά από 8, 12, 13 ή 14 αριθμητικά ψηφία**, ελέγχεται το πρότυπο ψηφίο ελέγχου modulo 10. Τα αρχικά μηδενικά **διατηρούνται** επειδή το barcode είναι string.
- **Εσωτερικοί αλφαριθμητικοί κωδικοί scanner:** λατινικοί χαρακτήρες `A–Z`, ψηφία `0–9`, παύλα `-`, κάτω παύλα `_` και τελεία `.`, έως 40 χαρακτήρες. Πεζά γράμματα μετατρέπονται σε κεφαλαία.
- **Αριθμητικοί εσωτερικοί κωδικοί άλλου μήκους** επιτρέπονται ως εσωτερικοί κωδικοί και **δεν** διαφημίζονται ως ελεγμένοι GTIN.
- Κενό ή μόνο κενά ⇒ `NULL`. Επιτρέπονται πολλές καρτέλες **χωρίς** barcode.
- Απαγορεύονται λευκοί χαρακτήρες μέσα στο barcode, ελληνικά γράμματα και άλλα σύμβολα, καθώς και μη έγκυρο ψηφίο GTIN.

{% hint style="info" %}
Το πεδίο **δεν εκδίδει, δεσμεύει ή καταχωρίζει επίσημο GTIN** και δεν αποδεικνύει ιδιοκτησία κωδικού. Για πολλαπλά barcodes ανά προϊόν απαιτείται ξεχωριστή μελλοντική σχεδίαση· τώρα υποστηρίζεται **ένα κύριο barcode ανά καρτέλα**.
{% endhint %}

### Αναλυτική περιγραφή

Η περιγραφή είναι απλό Unicode κείμενο, όχι HTML/Markdown editor. Τα προβαλλόμενα στοιχεία κωδικοποιούνται από Razor. Οι χαρακτήρες ελέγχου εκτός CR/LF/TAB απορρίπτονται. Η περιγραφή **δεν** μεταβάλλει αυτόματα φορολογική περιγραφή παραστατικού, εκτυπώσεις κουζίνας ή κατάλογο menu — αυτά παραμένουν ξεχωριστές αποφάσεις.

## 3. Τεχνική αρχιτεκτονική

| Επίπεδο | Αρχεία / αλλαγές |
| --- | --- |
| Domain | `CatalogItem.cs`, νέο `CatalogItemDetails.cs`, `ProductCatalog.cs`: normalization, GTIN check-digit, ατομική ενημέρωση δύο πεδίων |
| Application | `CatalogReadService.cs` (barcode στις σελίδες), `CatalogWriteService.cs` (validate πριν από SQL, explicit update flags) |
| Persistence | `CatalogItemConfiguration.cs`, `SqlCatalogReadStore.cs`, `SqlCatalogWriteStore.cs`: nullable fields, SQL index/check, search, sorting, update |
| EF Core | **9η additive migration** `20261114151000_CatalogItemDetails` και νέο target model/snapshot |
| UI | `CatalogManage.razor`, `Catalog.razor`, `CatalogManagementEndpoints.cs`, `CatalogEditorDraftStore.cs`, `aenaon-backoffice.css` |
| CLI | `CatalogManageCommand.cs`: `--barcode`, `--long-description`, ρητό καθάρισμα |
| Tests | Domain GTIN/unique, API SSR POST/fallback, CLI dry-run, isolated SQL Server constraints/migration |

**Προστασία παλιών clients:** κατά το UPDATE το backend λαμβάνει δύο ξεχωριστά flags `SetBarcode`/`SetLongDescription`. Αν δεν σταλεί το αντίστοιχο πεδίο από παλαιότερη φόρμα/CLI, διατηρείται η υπάρχουσα τιμή. Αν σταλεί κενό πεδίο από τη νέα φόρμα ή `--clear-*` από CLI, καθαρίζει ρητά.

## 4. Εγκατάσταση — Development

### Προαπαιτούμενα

1. .NET SDK **10.0.404** ή συμβατό νεότερο patch της σειράς `10.0.4xx` σύμφωνα με `global.json`, SQL Server 2022, Git.
2. Καθαρό/ενημερωμένο branch και **Development** βάση `AenaonERP_Dev`.
3. SQL backup **επιτυχώς επαληθευμένο**, λογαριασμός με δικαιώματα για schema migration μόνο όπου απαιτείται.
4. Εφαρμοσμένες οι **8 προηγούμενες migrations**. Δεν τεκμαίρεται από το GitHub ότι η δική σας βάση έχει όλες εφαρμοσμένες.

### Πριν από οποιαδήποτε SQL μεταβολή

Ανοίξτε PowerShell στον ριζικό φάκελο του repository:

```powershell
git switch main
git pull --ff-only origin main
dotnet --list-sdks
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

**Αναμενόμενο μετά το merge, αν έχετε ήδη εφαρμόσει και τις 8 προηγούμενες:** 8 applied / **1 pending** (`20261114151000_CatalogItemDetails`).

Ελέγξτε το μη καταστροφικό migration script: προστίθενται **δύο nullable στήλες**, `CK_CatalogItems_Barcode` και filtered unique index `IX_CatalogItems_TenantId_Barcode`. **Δεν** αναδιαμορφώνονται παλιές εγγραφές, κωδικοί, ταξινομήσεις, κατηγορίες ή τιμές.

Αποθηκεύστε έγκυρο backup, ελέγξτε ότι υπάρχει διαθέσιμος χώρος SQL, ότι η σύνδεση οδηγεί στη σωστή Development βάση και ότι δεν υπάρχουν απρόσμενες αλλαγές ιστορικού. Η βάση μπορεί να χρειαστεί locks για τον νέο index.

### Εφαρμογή migration

Μόνο **μετά** το backup και την επιβεβαίωση, χρησιμοποιήστε τον ήδη υπάρχοντα Development migration manager:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Έπειτα επαναλάβετε `--migrate-database --dry-run`. **Αναμενόμενο:** 9 applied / 0 pending. Η εφαρμογή στον δικό σας SQL Server απαιτεί πραγματική εκτέλεση και έλεγχο, όχι απλή αντιγραφή αυτών των οδηγιών.

## 5. Blazor Back Office — Αναλυτική χρήση

### Δημιουργία προϊόντος με barcode

1. Εκκινήστε το Development API και συνδεθείτε από `https://localhost:7140/login` ως Owner.
2. Επιλέξτε την επιχείρηση και ανοίξτε **Κατάλογος → Προϊόντα → Νέο προϊόν**.
3. Συμπληρώστε τον υπάρχοντα εσωτερικό κωδικό (αυτόματο ή χειροκίνητο), υποχρεωτική περιγραφή και κατηγορία.
4. Στην περιοχή **Πρόσθετα στοιχεία είδους**, συμπληρώστε **Barcode / EAN** προαιρετικά, π.χ. `4006381333931`. Το σύστημα ελέγχει format και ψηφίο GTIN κατά την αποθήκευση.
5. Προσθέστε **Αναλυτική περιγραφή** αν απαιτείται. Επιτρέπονται παραγράφοι.
6. Επιλέξτε μονάδα μέτρησης και λογιστικό τύπο, όπως και πριν, και αποθηκεύστε.

### Υπηρεσίες

Ακολουθήστε **Κατάλογος → Υπηρεσίες → Νέα υπηρεσία**. Τα δύο πεδία είναι επίσης προαιρετικά στις υπηρεσίες. Η υπηρεσία εξακολουθεί να μη διαχειρίζεται απόθεμα. Δεν αλλάζει ο λογιστικός της τύπος ή ο φορολογικός χαρακτηρισμός.

### Επεξεργασία και καθάρισμα

Στην καρτέλα υπάρχοντος προϊόντος/υπηρεσίας, τα αποθηκευμένα πεδία ανακτώνται από SQL. Τροποποιήστε την τιμή ή αφήστε το barcode/την αναλυτική περιγραφή κενά για **ρητή εκκαθάριση**. Όλα τα υπόλοιπα χαρακτηριστικά παραμένουν ως είχαν.

### Αναζήτηση και λίστα

Στον **Κατάλογο** επιτρέπεται αναζήτηση κατά **αρχή κωδικού, barcode ή περιγραφής**. Η νέα στήλη **Barcode** εμφανίζεται στις λίστες προϊόντων και υπηρεσιών, με server-side ταξινόμηση πατώντας την επικεφαλίδα της. Οι πλήρεις αναλυτικές περιγραφές **δεν φορτώνονται στη σελιδοποιημένη λίστα** για λόγους επιδόσεων, αλλά μόνο στον editor.

### Γρήγορη δημιουργία κατηγορίας

Αν χρειαστεί να δημιουργήσετε νέα κατηγορία από ημιτελές προϊόν/υπηρεσία, ο προσωρινός editor draft αποθηκεύει και τα δύο νέα πεδία με χρονικό όριο. Μετά τη δημιουργία κατηγορίας, επιστρέφετε στην αρχική καρτέλα χωρίς να χάσετε τα στοιχεία που συμπληρώσατε. Απαιτείται επανέλεγχος εξουσιοδότησης.

## 6. CLI / CMD

Η ίδια `CatalogWriteService` εξυπηρετεί Blazor και CLI. Όλες οι εγγραφές απαιτούν έγκυρο Development SQL schema, διαδραστική επαλήθευση κωδικού, Owner/Manage και ρητό `--apply`. Το `--dry-run` **δεν** γράφει στη βάση.

### Δημιουργία (προεπισκόπηση)

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-catalog --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --view products --action create --auto-code --name "Espresso" --category-id "CATEGORY_GUID" --barcode "4006381333931" --long-description "Espresso με πληροφορίες παρασκευής." --dry-run
```

### Δημιουργία με εγγραφή (μόνο με πραγματικά GUID και έγκριση)

Αντικαταστήστε `--dry-run` με `--apply` στην προηγούμενη εντολή. Δεν υπάρχει αυτόματη εγγραφή.

### Αλλαγή barcode (χωρίς καθάρισμα αναλυτικής περιγραφής)

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-catalog --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --view products --action update --id "ITEM_GUID" --code "1" --name "Espresso" --category-id "CATEGORY_GUID" --barcode "INT-ESPRESSO" --dry-run
```

Στο UPDATE απαιτούνται τα υφιστάμενα υποχρεωτικά πεδία κωδικού, περιγραφής και κατηγορίας· χρησιμοποιήστε τα πραγματικά αποθηκευμένα στοιχεία. Το `--barcode` αλλάζει **μόνο** το barcode, όχι την αναλυτική περιγραφή.

### Εκκαθάριση

Χρησιμοποιήστε `--clear-barcode` ή `--clear-long-description` στην εντολή update. Οι επιλογές `--barcode` και `--clear-barcode` αποκλείουν η μία την άλλη, όπως και `--long-description` / `--clear-long-description`.

## 7. Συντομεύσεις / προσβασιμότητα

- `Tab` / `Shift+Tab`: μετακίνηση στα πεδία και στα κουμπιά.
- `Enter`: υποβολή της ενεργής φόρμας, με διατήρηση του φυσικού submit.
- `B`: επιστροφή στον κατάλογο από τον editor (browser access key, συνήθως με `Alt+Shift+B` ανά browser).
- `X`: ανάπτυξη/σύμπτυξη sidebar (επίσης browser access key).
- Κάθε πεδίο έχει σαφές label και χρήσιμο μήνυμα σφάλματος.
- Το textarea είναι πολυγραμμικό και επιτρέπει χειρισμό με πληκτρολόγιο, focus indicator και κάθετη αλλαγή μεγέθους.

## 8. Δικαιώματα, ασφάλεια, δεδομένα

- Η αποθήκευση επιτρέπεται μόνο σε `TenantPermission.Manage` (Owner). Member/Read διαβάζει τη λίστα χωρίς δυνατότητα εγγραφής.
- Όλες οι SQL εγγραφές/αναζητήσεις περιορίζονται από `TenantId` και υπάρχουν σύνθετα tenant-safe FK/unique constraints. Ίδιο barcode επιτρέπεται **σε διαφορετικούς tenants**, όχι στον ίδιο.
- Τα HTTPS POST συνεχίζουν να απαιτούν ASP.NET Core Antiforgery και το ήδη ισχύον authentication cookie.
- Validation επιβάλλεται από Application/Domain και επιπλέον SQL constraints· δεν αρκεί το HTML `maxlength`.
- Τα logs καταγράφουν ενέργειες και διαγνωστικούς κωδικούς χωρίς password/token. Δεν προστίθεται ακόμη immutable field-level audit history (9.5C.3).
- Τα νέα πεδία δεν δημιουργούν POS/παραγγελία, δεν διαβιβάζουν IMPACT και **δεν** παρέχουν φορολογική έγκριση.

## 9. myDATA / IMPACT Compatibility Gate

Ο έλεγχος πηγών 14/11/2026 δείχνει [ΑΑΔΕ myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata). Το [IMPACT API portal](https://developers.s1ecos.com/) και το [OpenAPI](https://einvoiceapi.impact.gr/swagger/coreapi/swagger.json) απαιτούν ανεξάρτητο review πριν από οποιαδήποτε πραγματική ροή παραστατικών.

Το barcode είναι **εμπορικός αναγνωριστικός κωδικός** και το `LongDescription` βοηθητικό κείμενο. **Δεν** αντιστοιχίζονται αυτομάτως σε κωδικούς ΦΠΑ, χαρακτηρισμούς Ε3/myDATA ή τεχνικά πεδία του `EliseCoreDocument`. Τα 254 Pending fiscal review observations παραμένουν μη εγκεκριμένα. Δείτε το [κεντρικό compatibility gate](18-mydata-impact-compatibility.md).

## 10. Προ/μετά έλεγχοι, tests και αποδοχή

### Πριν

- [ ] Καθαρό, ενημερωμένο branch.
- [ ] Διαθέσιμο .NET SDK/SQL Server και παρούσες 8 προηγούμενες migrations.
- [ ] Πραγματικό backup Development SQL, ελεγχθείσα δυνατότητα restore.
- [ ] Επιβεβαιωμένος Owner/Manage, σωστή επιχείρηση/κατάστημα.

### Μετά

- [ ] 9 applied / 0 pending και επιβεβαίωση δύο nullable columns/index/check.
- [ ] Παλιό προϊόν και υπηρεσία φορτώνονται αμετάβλητα με `Barcode = NULL` και `LongDescription = NULL`.
- [ ] GTIN-13 `4006381333931` γίνεται δεκτό, `4006381333932` απορρίπτεται.
- [ ] Εσωτερικό lowercase barcode μετατρέπεται σε uppercase· πεζά/κεφαλαία δεν επιτρέπουν διπλοεγγραφή.
- [ ] Ίδιο barcode σε άλλον Tenant επιτρέπεται· στην ίδια επιχείρηση, ακόμη και μεταξύ προϊόντος/υπηρεσίας, απορρίπτεται.
- [ ] Αναλυτική περιγραφή 2.000 χαρακτήρων αποθηκεύεται, 2.001 απορρίπτεται.
- [ ] CLI/GUI create/edit/clear και παλιός caller χωρίς νέα πεδία δεν δημιουργούν απώλεια δεδομένων.
- [ ] Η αναζήτηση barcode και sorting εκτελούνται στη SQL **πριν** το paging.
- [ ] Προσωρινή νέα κατηγορία επιστρέφει στο προϊόν/υπηρεσία με συμπληρωμένα τα ίδια στοιχεία.
- [ ] Member δεν γράφει και ξένο Tenant δεν αποκαλύπτεται.
- [ ] Επιτυχές build/test, isolated SQL Server, light/dark, responsive desktop/mobile, keyboard, zoom — **απαιτείται πραγματική επαλήθευση**.

### Εντολές ελέγχου (χωρίς ενεργό SQL production)

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Τα πραγματικά SQL integration tests εκτελούνται **μόνο** σε isolated CI SQL Server 2022, όχι απευθείας στην `AenaonERP_Dev`. Ο έλεγχος οπτικής συμπεριφοράς παραμένει απαραίτητος σε Chrome/Edge, desktop και mobile widths.

## 11. Troubleshooting

| Σύμπτωμα | Τι ελέγχουμε |
| --- | --- |
| «Μη έγκυρο barcode» | ASCII σύνταξη, μήκος, κενά και GTIN check digit (για 8/12/13/14 αριθμητικά) |
| «Barcode χρησιμοποιείται ήδη» | Άλλο προϊόν/υπηρεσία του ίδιου Tenant με ίδιο barcode (case-insensitive) |
| Αποτυχημένη SQL migration | Backup, δικαιώματα, διαθέσιμος χώρος, ακριβές schema/index script, logs· **όχι** τυφλό retry |
| Δεν υπάρχουν νέα πεδία στην καρτέλα | Επιβεβαίωση deployed commit, migration status και browser cache |
| Χάνονται στοιχεία κατά quick-category | Ελέγξτε το ίδιο tenant/user, χρονική ισχύ draft και επανέλεγχο authorization |
| Η αναζήτηση δεν βρίσκει barcode | Πρόθεμα αναζήτησης, κατάσταση ενεργού είδους, σωστό tenant και collation |
| CLI απορρίπτει συνδυασμό επιλογών | `--clear-*` και αντίστοιχο πεδίο δεν επιτρέπονται ταυτόχρονα |
| Αναλυτική περιγραφή δεν εμφανίζεται στη λίστα | Είναι σκόπιμα διαθέσιμη στον editor· η λίστα φορτώνει μόνο τα απαραίτητα |

## 12. Rollback / ενημερώσεις / επόμενα βήματα

Το code rollback γίνεται με revert του PR. Για SQL, η 9η migration έχει `Down`, αλλά **μην την εφαρμόσετε σε δεδομένα που ήδη συμπληρώθηκαν** χωρίς εξαγωγή/backup των νέων πεδίων: το SQL downgrade διαγράφει τις νέες στήλες και τις τιμές τους. Προτιμάται restore rehearsal και ξεχωριστή απόφαση διαχείρισης δεδομένων. Δεν επιτρέπεται αυτοματοποιημένη αναστροφή σε παραγωγή.

**Επόμενο βήμα:** **9.5C.3 — Pricing Integrity & History** (αναλλοίωτο ιστορικό αλλαγών τιμών, έλεγχοι επικαλύψεων, concurrent writes). Excel/imports και μαζικές γενικές εργασίες παραμένουν εκτός scope έως την ωρίμανση του μοντέλου. Το 9.6A ακολουθεί μετά το τελικό 9.5C acceptance.
