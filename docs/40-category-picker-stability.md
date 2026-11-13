# AENAON ERP — 9.4C.3: Category Picker Stability & UI Finalization

## 1. Σκοπός

Διορθώνει τα πραγματικά προβλήματα που καταγράφηκαν στο βίντεο 13/11/2026: **πράσινο περίγραμμα γύρω από τίτλους**, dropdown κατηγοριών που βγαίνει **εκτός οθόνης** και ενέργεια **«Δημιουργήστε νέα κατηγορία»** που δεν είναι πάντοτε προσβάσιμη/δεν ενεργοποιείται αξιόπιστα. Παράλληλα βελτιώνει την ελληνική ορολογία, την αντίθεση και την προσβασιμότητα.

{% hint style="info" %}
Πρόκειται μόνο για αλλαγές στο UI, JavaScript και tests. **Δεν προστίθεται migration, δεν αλλάζει το schema**, δεν ενημερώνονται φορολογικά δεδομένα, myDATA ή IMPACT και δεν εκδίδεται κανένα παραστατικό.
{% endhint %}

## 2. Απαιτήσεις

- .NET SDK συμβατό με το `global.json` (10.0.404).
- Development περιβάλλον AENAON, SQL Server 2022 με 6 εφαρμοσμένες και 0 εκκρεμείς migrations.
- HTTPS session και δικαίωμα Owner/Manage για δημιουργία και επεξεργασία.
- Google Chrome ή σύγχρονος Chromium browser. Ο χρήστης πρέπει να έχει επιλέξει επιχείρηση και, όπου απαιτείται, κατάστημα.

## 3. Τι διορθώθηκε

| Πρόβλημα | Διόρθωση |
| --- | --- |
| Μεγάλο πράσινο περίγραμμα στην αρχική οθόνη και στους τίτλους | Ειδικός CSS κανόνας για `h1[tabindex="-1"]:focus-visible`· η προγραμματιστική εστίαση του Blazor διατηρείται, τα διαδραστικά controls εξακολουθούν να έχουν σαφές keyboard focus |
| Popup κατηγοριών εκτός viewport | `position: fixed`, υπολογισμός διαθέσιμου χώρου πάνω/κάτω και clamp σε `visualViewport`; ενημέρωση σε scroll/resize/zoom |
| Πολλές κατηγορίες καλύπτουν το κουμπί δημιουργίας | Κύλιση **μόνο** στη λίστα· σταθερό footer και ξεχωριστό **πάντοτε ορατό** κουμπί κάτω από το πεδίο |
| Το click στο popup κλείνει πριν από την ενέργεια | Χειρισμός mouse/focusout και ασφαλής `requestSubmit` με `formaction=/ui/catalog/new-category` και `formnovalidate` |
| Διπλά αιτήματα αναζήτησης σε κάθε keystroke | Μία debounce διαδικασία ανά αλλαγή, `AbortController` και sequence guard |
| Ασυνέπεια επιλογής πληκτρολογίου | Ξεχωριστό `aria-selected` για την καταχωρισμένη επιλογή και active class για το τρέχον αποτέλεσμα |
| Επανάληψη ονομασίας κατηγορίας | Περιγραφή σε πρώτη γραμμή, πλήρης ιεραρχική διαδρομή **μόνο εφόσον διαφέρει** και κωδικός στη δεύτερη |
| Χαμηλή αναγνωσιμότητα | Αύξηση βοηθητικών γραμματοσειρών και contrast· ο αυτόματος κωδικός παραμένει ευανάγνωστος όταν το πεδίο είναι disabled |
| Breadcrumbs με «Εφαρμογή» | Ελληνική πλοήγηση, π.χ. `Κατάλογος / Προϊόντα / Νέο προϊόν` |

## 4. Χρήση νέου Category Picker

1. Ανοίξτε **Κατάλογος → Προϊόντα → Νέο προϊόν** ή **Υπηρεσίες → Νέα υπηρεσία**.
2. Εστιάστε στο πεδίο **Κατηγορία**. Η λίστα ανοίγει εντός της ορατής οθόνης — προς τα πάνω αν δεν υπάρχει αρκετός χώρος κάτω.
3. Πληκτρολογήστε κωδικό ή αρχικά γράμματα ονόματος. Εμφανίζονται έως 30 αποτελέσματα.
4. Επιλέξτε με ποντίκι ή `↑ / ↓` και `Enter`.
5. Αν δεν υπάρχει η επιθυμητή κατηγορία, επιλέξτε το **πάντοτε ορατό** «Δεν βρίσκετε την κατηγορία; Δημιουργήστε νέα» ακριβώς κάτω από το πεδίο.
6. Δημιουργήστε την κατηγορία. Επιστρέφετε στο αρχικό είδος/υπηρεσία με τα προσωρινά στοιχεία σας και τη νέα κατηγορία επιλεγμένη.
7. Πατήστε **Αποθήκευση** ή **Αποθήκευση και νέο** για να ολοκληρώσετε την καταχώριση είδους.

Η γρήγορη δημιουργία **δεν αποθηκεύει** αυτόματα το προϊόν ή την υπηρεσία: δημιουργείται μόνο η κατηγορία και μετά επιστρέφετε για να αποφασίσετε πώς θα συνεχίσετε.

### Χωρίς JavaScript

Το native select παραμένει fallback. Για πλήρη γρήγορη δημιουργία και responsive popup απαιτείται η JavaScript του Category Picker. Αν το asset δεν φορτώνει, ελέγξτε πρώτα το tab Network του Chrome πριν δοκιμάσετε ξανά.

## 5. Συντομεύσεις

| Πλήκτρα | Ενέργεια |
| --- | --- |
| `Tab`, `Shift+Tab` | Εστίαση πεδίων |
| `↑`, `↓` | Κίνηση ανάμεσα στα αποτελέσματα |
| `Enter` | Επιλογή ενεργού αποτελέσματος, **όχι** υποβολή καταχώρισης |
| `Escape` | Κλείσιμο και επιστροφή στην ήδη επιλεγμένη κατηγορία |
| `Alt+K` | Νέα κατηγορία από ανοιχτό προϊόν/υπηρεσία |
| `Alt+C` | Κατάλογος |
| `Alt+N` | Νέα καταχώριση καταλόγου |
| `Ctrl+Shift+R` | Ανανέωση Chrome χωρίς cached resources |

Οι συντομεύσεις Alt ενδέχεται να δεσμεύονται από τον browser. Οι ίδιες λειτουργίες παραμένουν διαθέσιμες μέσω ορατών κουμπιών.

## 6. Δοκιμές και CI

Το GitHub Actions τρέχει:
- `node --check` σε όλα τα JS assets του UI.
- `dotnet restore --locked-mode`, `dotnet format`, Release build και unit/HTTP regression tests σε Windows και Ubuntu.
- Απομονωμένο SQL Server 2022 integration test.
- **Πραγματικό headless Chromium smoke test** μέσω Chrome DevTools Protocol, χωρίς npm packages, σε desktop 1366×768, μεγάλο desktop 2560×1380 και mobile 390×640. Ελέγχει ότι το popup/footer είναι εντός viewport, ότι το `h1` δεν εμφανίζει περίγραμμα από προγραμματιστική εστίαση, ότι `↑ / ↓ / Enter` επιλέγουν category ID και ότι πραγματικό mouse click στο footer κάνει POST νέας κατηγορίας.

Το browser smoke χρησιμοποιεί το πραγματικό JavaScript/CSS σε **απομονωμένη τοπική HTML φόρμα** και ψεύτικο HTTP endpoint. **Δεν** υποκαθιστά οπτική δοκιμή στην πραγματική βάση και εφαρμογή με αυθεντικό session.

Για τοπική εκτέλεση (εφόσον υπάρχει Chrome/Chromium και Node.js ≥22):

```powershell
node scripts/Test-CategoryPickerBrowser.mjs
```

## 7. Εγκατάσταση / αναβάθμιση Development

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git status --short
git fetch origin
git switch fix/9-4c-3-category-picker-stability
git pull --ff-only origin fix/9-4c-3-category-picker-stability
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https
```

Ελέγξτε ότι έχει σταματήσει το παλιό instance του API πριν από την εκκίνηση. Ανοίξτε `https://localhost:7140/catalog`. Χρησιμοποιήστε `Ctrl+Shift+R` για ανανέωση χωρίς cache εάν χρειαστεί. Τα νέα CSS/JS χρησιμοποιούν fingerprinted URLs, οπότε η νέα έκδοση ανακτά τα αντίστοιχα αρχεία αυτόματα.

**Δεν τρέχουμε `--migrate-database --apply`** για αυτό το βήμα.

## 8. Έλεγχος αποδοχής στην πραγματική εγκατάσταση

- [ ] Άνοιγμα αρχικής οθόνης: κανένα τεράστιο περίγραμμα γύρω από τον τίτλο.
- [ ] Πλήκτρο Tab στα κουμπιά: σαφές ορατό focus.
- [ ] Category Picker κάτω στο παράθυρο: ανοίγει προς τα πάνω.
- [ ] Category Picker επάνω στο παράθυρο: ανοίγει προς τα κάτω.
- [ ] Πολλές κατηγορίες: μόνο η λίστα κάνει scroll, το footer παραμένει ορατό.
- [ ] Κάτω από το πεδίο φαίνεται πάντα η δημιουργία νέας κατηγορίας.
- [ ] Click μέσα στο popup και στο μόνιμο κουμπί: ίδια σωστή δημιουργία.
- [ ] Διατήρηση κωδικού, περιγραφής, `tracksStock`, Tenant/Store και νέα κατηγορία.
- [ ] `Enter` και `Escape` χωρίς απρόσμενη υποβολή.
- [ ] Αργές ή μη διαθέσιμες απαντήσεις δικτύου δεν αντικαθιστούν νεότερα αποτελέσματα.
- [ ] Ελληνικά breadcrumbs, light/dark, 100%–200% zoom, κινητό.
- [ ] Χωρίς ανεπιθύμητες εγγραφές προϊόντων ή παραστατικών κατά τη δοκιμή.

## 9. Αντιμετώπιση προβλημάτων

| Σύμπτωμα | Ενέργεια |
| --- | --- |
| Εμφανίζεται παλιό popup | Βεβαιωθείτε ότι εκτελείται το σωστό branch και επανεκκινήστε το API. Ελέγξτε fingerprint URL και `Ctrl+Shift+R`. |
| Το popup βγαίνει εκτός οθόνης | Ελέγξτε ότι φορτώθηκε το νέο `aenaon-category-picker.js` και `aenaon-backoffice.css`. Επιβεβαιώστε τη θέση με DevTools `getBoundingClientRect()`. |
| Δεν δημιουργείται κατηγορία | Δοκιμάστε το μόνιμο κουμπί. Ελέγξτε το POST `/ui/catalog/new-category`, το αντι-CSRF token και τα HTTP status codes. |
| HTTP 403 | Ο χρήστης δεν έχει δικαίωμα Manage στο Tenant/Store. |
| HTTP 400 | Ελέγξτε κανονικό HTTPS, τα απαιτούμενα πεδία και την εγκυρότητα του CSRF token. |
| Η αναζήτηση δεν εμφανίζει δεδομένα | Ελέγξτε την εξουσιοδότηση του `GET /ui/catalog/categories`, το δίκτυο και τα server logs. |
| Τα πρόχειρα στοιχεία χάθηκαν | Η προσωρινή μνήμη draft λήγει ή καθαρίζεται στην επανεκκίνηση του API. |
| Το Chrome smoke αποτυγχάνει | Βεβαιωθείτε ότι εγκαταστάθηκε Chrome/Chromium, Node.js 22+ και ότι επιτρέπεται το loopback interface. |

## 10. Logging, ασφάλεια, backup και rollback

Οι ήδη υπάρχοντες ASP.NET Core structured logs καταγράφουν πρόσβαση, ενέργειες καταλόγου και σφάλματα χωρίς credentials. Η γρήγορη δημιουργία παραμένει development-only, με anti-CSRF, user/Tenant/store authorization, bounded drafts και server-side SQL validation. Το νέο browser smoke χρησιμοποιεί ψεύτικη βάση, δεν απαιτεί πραγματικά credentials.

Εφόσον δεν υπάρχει αλλαγή στο SQL schema, **η επαναφορά κώδικα** γίνεται με revert του merge ή επιστροφή στο προηγούμενο commit και επανεκκίνηση. Πριν από πραγματικές δοκιμαστικές εγγραφές, επιβεβαιώστε ότι υπάρχει πρόσφατο backup Development. Η επαναφορά κώδικα δεν διαγράφει δεδομένα που έχουν ήδη εγγραφεί.

## 11. Σχετικά

- [9.4C.2 — Category Picker & UI Polish](39-backoffice-category-picker-polish.md)
- [9.4C.1 — Ιεραρχία και αυτόματη αρίθμηση](38-catalog-hierarchy-auto-numbering.md)
- [WAI-ARIA Combobox Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/combobox/)
- [ASP.NET Core Blazor — Navigation and Focus](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)
