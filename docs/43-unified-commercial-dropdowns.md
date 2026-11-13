# AENAON ERP — 9.5A.1: Unified Commercial Dropdowns

## 1. Σκοπός

Το βήμα **9.5A.1** ενοποιεί την εμφάνιση και τη συμπεριφορά των πεδίων **Μονάδα μέτρησης** και **Λογιστικός τύπος** στην καρτέλα προϊόντος/υπηρεσίας με το υπάρχον **Category Picker**. Τα πεδία διατηρούν τη λογική και τις αριθμητικές τιμές του βήματος 9.5A.

{% hint style="info" %}
Πρόκειται αποκλειστικά για αλλαγή παρουσίασης/προσβασιμότητας. **Δεν απαιτείται νέα SQL migration**, δεν αλλάζουν το schema, τα API, οι CLI παράμετροι, οι εμπορικές προεπιλογές ή οι αντιστοιχίσεις myDATA/IMPACT. Δεν εκδίδονται ούτε διαβιβάζονται παραστατικά.
{% endhint %}

## 2. Προϋποθέσεις

- Εγκεκριμένη εγκατάσταση του AENAON ERP 9.5A με **7 εφαρμοσμένες και 0 εκκρεμείς migrations** στη βάση `AenaonERP_Dev`.
- Πρόσβαση στο Back Office μέσω HTTPS και δικαίωμα Owner/Manage στον επιλεγμένο Tenant.
- Υποστηριζόμενο σύγχρονο πρόγραμμα περιήγησης, κατά προτίμηση Chrome/Edge.
- Για ανάπτυξη: συμβατό .NET SDK από το `global.json`, Git και Node.js για το browser smoke test.
- Για αλλαγές κώδικα: ξεχωριστό feature branch και επιτυχές CI πριν από merge.

## 3. Χρήση: προϊόν

1. Ανοίξτε **Κατάλογος → Προϊόντα → Νέο προϊόν**.
2. Συμπληρώστε **Περιγραφή** και επιλέξτε **Κατηγορία**.
3. Στα **Εμπορικά και λογιστικά στοιχεία**, κάντε κλικ στη **Μονάδα μέτρησης** ή στο βέλος.
4. Αναζητήστε π.χ. **Κιλό** ή μετακινηθείτε στα αποτελέσματα και επιλέξτε την επιθυμητή τιμή.
5. Ανοίξτε τον **Λογιστικό τύπο** και επιλέξτε π.χ. **Πρώτη ύλη**.
6. Αποθηκεύστε κανονικά. Οι πραγματικές τιμές που μεταφέρονται στο backend είναι αντίστοιχα `2` και `3`, όχι το κείμενο των ετικετών.
7. Επανανοίξτε το προϊόν και βεβαιωθείτε ότι οι επιλογές εμφανίζονται ως επιλεγμένες.

## 4. Χρήση: υπηρεσία

Ανοίξτε **Κατάλογος → Υπηρεσίες → Νέα υπηρεσία**.

- **Μονάδα μέτρησης:** μόνο **Υπηρεσία** ή **Ώρα**.
- **Λογιστικός τύπος:** μόνο **Υπηρεσία**.

Ακόμη και όταν υπάρχει μία μόνο επιλογή, χρησιμοποιείται το ίδιο οπτικό component. Δεν επιτρέπονται άκυροι συνδυασμοί από το backend ή τη βάση.

## 5. Πληκτρολόγιο και προσβασιμότητα

| Χειρισμός | Αποτέλεσμα |
| --- | --- |
| Κλικ στο πεδίο ή στο βέλος | Άνοιγμα/κλείσιμο αναδυόμενης λίστας |
| Πληκτρολόγηση | Τοπικό φιλτράρισμα διαθέσιμων επιλογών |
| `↑` / `↓` | Μετακίνηση στην προηγούμενη/επόμενη επιλογή |
| `Home` / `End` | Μετάβαση στην πρώτη/τελευταία εμφανιζόμενη επιλογή |
| `Enter` | Επιλογή του ενεργού αποτελέσματος, χωρίς υποβολή φόρμας |
| `Escape` | Ακύρωση της τρέχουσας αναζήτησης και επαναφορά προηγούμενης επιλογής |
| `Tab` / `Shift+Tab` | Κλείσιμο λίστας και φυσιολογική μετάβαση εστίασης |
| Ποντίκι | Άμεση επιλογή, με διατήρηση της εστίασης |

Κάθε πεδίο παρέχει `role="combobox"`, συνδεδεμένο `listbox`, `aria-expanded`, `aria-selected`, `aria-activedescendant`, περιγραφή, ζωντανή κατάσταση αναζήτησης και εμφανές focus indicator. Το popup τοποθετείται δυναμικά προς τα πάνω ή κάτω και παραμένει εντός του ορατού viewport, ακόμη και σε μικρές οθόνες/zoom.

## 6. Ασφάλεια και fallback χωρίς JavaScript

Η HTML φόρμα περιέχει δύο εναλλακτικές αναπαραστάσεις *ανά πεδίο*:

- Ένα **native `<select>`**, αρχικά ενεργό, με το έγκυρο `name` και τον αριθμητικό κωδικό.
- Ένα **`<input type="hidden">`**, αρχικά `disabled`.

Με την επιτυχή αρχικοποίηση JavaScript, ενεργοποιείται το hidden αριθμητικό πεδίο και απενεργοποιείται το native select. **Ποτέ δεν υποβάλλονται δύο ενεργές τιμές για το ίδιο όνομα.**

Αν η JavaScript δεν φορτωθεί, η καταχώριση λειτουργεί μέσω του native select. Αν ο χρήστης γράψει ελεύθερο κείμενο χωρίς να επιλέξει έγκυρη τιμή, το UI αποκλείει την αποθήκευση και εμφανίζει ελληνικό μήνυμα. Ανεξάρτητα από τον browser, το backend επανελέγχει τις τιμές με τους υπάρχοντες Domain/Application/SQL περιορισμούς.

{% hint style="warning" %}
Ο λογιστικός τύπος παραμένει εμπορική ταξινόμηση. **Δεν** μετατρέπεται αυτόματα σε χαρακτηρισμό Ε3/myDATA, ΦΠΑ, λογιστικό λογαριασμό ή κωδικό παρόχου IMPACT.
{% endhint %}

## 7. Αρχεία υλοποίησης

| Αρχείο | Ρόλος |
| --- | --- |
| `Ui/Components/CatalogCommercialPicker.razor` | Επαναχρησιμοποιούμενο SSR component με fallback |
| `Ui/CatalogCommercialOptions.cs` | Ελεγχόμενες τιμές και ελληνικές ετικέτες |
| `Ui/Pages/CatalogManage.razor` | Ενσωμάτωση στη φόρμα |
| `wwwroot/js/aenaon-commercial-picker.js` | Εμφάνιση, keyboard, mouse, ARIA, έλεγχος και viewport |
| `wwwroot/css/aenaon-backoffice.css` | Κοινή εμφάνιση με Category Picker |
| `Ui/UiAssetVersions.cs` και `Ui/App.razor` | Φόρτωση fingerprinted script χωρίς προβλήματα cache |
| `tests/Aenaon.Api.Tests/CatalogCommercialPickerUiTests.cs` | HTTP/SSR regression tests |
| `scripts/Test-CommercialPickerBrowser.mjs` | Πραγματικό Chromium/CDP smoke test |

## 8. Εγκατάσταση, build και έλεγχοι

Από PowerShell, στη ρίζα του repository:

```powershell
git switch main
git pull --ff-only origin main

# Μετά την έγκριση και το merge του PR:
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
node --check src/Aenaon.Api/wwwroot/js/aenaon-commercial-picker.js
node scripts/Test-CommercialPickerBrowser.mjs
dotnet run --project src/Aenaon.Api --launch-profile https
```

Το browser test απαιτεί εγκατεστημένο Chrome/Chromium και είναι αυτόνομο, χωρίς npm dependencies. Για χρήση σε Windows, εκτελείται εφόσον το Chrome είναι διαθέσιμο στο PATH ή οριστεί `CHROME_BIN`.

**CLI:** Οι εντολές `--manage-catalog --unit Kilogram --accounting-type RawMaterial` παραμένουν αμετάβλητες. Για πλήρη σύνταξη, συμβουλευτείτε το `docs/42-catalog-commercial-profile.md`.

**Logging:** Τα υπάρχοντα server-side structured logs καταγράφουν τις πράξεις αποθήκευσης. Δεν προστίθενται credentials ή ευαίσθητες τιμές σε client logs.

## 9. Checklist ελέγχου

- [ ] Ίδιο ύψος, χρώματα, περιγράμματα, βέλη και πράσινη ένδειξη επιλογής με την Κατηγορία.
- [ ] Light και dark theme, χωρίς ασυνέπειες μετά από `Ctrl+Shift+R`.
- [ ] Popup εντός viewport σε 1366×768, 2560×1380 και 390×640, πάνω/κάτω ανάλογα με τον χώρο.
- [ ] `↑`, `↓`, `Enter`, `Escape`, `Tab` και `Shift+Tab`.
- [ ] Επιλογή με ποντίκι και ορατή εστίαση πληκτρολογίου.
- [ ] Επιβεβαίωση `aria-expanded`, `aria-selected`, `aria-activedescendant` με Chrome DevTools.
- [ ] Καμία διπλή τιμή `unitOfMeasure` ή `accountingType` στο POST.
- [ ] Η απενεργοποίηση JavaScript εμφανίζει λειτουργικά native selects.
- [ ] Οι μονάδες/λογιστικοί τύποι υπηρεσίας περιορίζονται σωστά.
- [ ] Δημιουργία νέας κατηγορίας από μη αποθηκευμένο προϊόν διατηρεί τις δύο επιλογές.
- [ ] Καμία migration ή αλλαγή στη `AenaonERP_Dev`.

## 10. Troubleshooting και rollback

| Πρόβλημα | Έλεγχος |
| --- | --- |
| Εμφανίζεται μόνο native select | Ελέγξτε φόρτωση `aenaon-commercial-picker.js`, κονσόλα, CSP και asset fingerprint. |
| Λάθος βέλη/χρώματα | Ελέγξτε `aenaon-backoffice.css` και cache· ανανεώστε με `Ctrl+Shift+R`. |
| Δεν ανοίγει η λίστα | Ελέγξτε ότι το component έχει `data-commercial-picker` και δεν εμφανίζεται JavaScript error. |
| Δεν αποθηκεύεται επιλογή | Ελέγξτε ότι επιλέχθηκε πραγματικό αποτέλεσμα, το HTTP POST μεταφέρει έναν αριθμό και υπάρχει δικαίωμα Manage. |
| Επιλογή κόβεται στο κάτω άκρο | Ελέγξτε viewport/zoom και την εκτέλεση `positionPopup()`. |
| Αναδρομικό bug στο Category Picker | Εκτελέστε `node scripts/Test-CategoryPickerBrowser.mjs` και τα υπάρχοντα regression tests. |

**Rollback εφαρμογής:** Επαναφέρετε το προηγούμενο commit/Release της εφαρμογής. **Δεν χρειάζεται SQL rollback**, καθώς το βήμα δεν τροποποιεί schema ή δεδομένα. Η αντικατάσταση της εφαρμογής γίνεται με το καθιερωμένο ασφαλές deployment και διαθέσιμο προηγούμενο build.

{% hint style="info" %}
Η οπτική αποδοχή στον πραγματικό Chrome/Edge και στη Development εγκατάσταση πρέπει να ολοκληρωθεί πριν από rollout. Τα αυτοματοποιημένα smoke tests δεν αποδεικνύουν από μόνα τους τέλεια οπτική ταύτιση.
{% endhint %}
