# Aenaon ERP — Διόρθωση SVG Icons & Static Asset Cache (Βήμα 8.4.6)

## Σκοπός

Αυτό το βήμα διορθώνει ένα πραγματικό regression που εμφανίστηκε μετά το UI/UX Hardening: το Login και το Dashboard αποδίδουν υπερμεγέθη SVG icons, κάρτες και κουμπιά επειδή ένα παλιότερο CSS μπορεί να παραμένει αποθηκευμένο στον browser ενώ το νέο HTML περιλαμβάνει καινούργια εικονίδια.

Το σενάριο αναπαράχθηκε οπτικά στα screenshots του Aenaon ERP. Η υπάρχουσα έκδοση aenaon.css περιέχει ήδη μικρές διαστάσεις SVG, οπότε η ασυμφωνία CSS/HTML είναι ιδιαίτερα πιθανό αίτιο. Δεν έχει επιβεβαιωθεί με απομακρυσμένη επιθεώρηση του πραγματικού browser network cache, επομένως η αιτία περιγράφεται ως τεκμηριωμένη διάγνωση, όχι ως παρατηρημένο network trace.

## Διορθώσεις

- Το AppIcon.razor ορίζει εγγενείς διαστάσεις SVG width=20 και height=20, ανεξάρτητα από το πότε θα φορτωθεί CSS.
- Το UiAssetVersions υπολογίζει SHA-256 fingerprint από τα πραγματικά CSS, JavaScript, theme-init και favicon αρχεία, μία φορά ανά εκκίνηση του API.
- Το App.razor αναφέρει τα assets με την αντίστοιχη παράμετρο version στα URLs τους. Έτσι κάθε αλλαγή περιεχομένου οδηγεί σε νέο URL και αποφεύγει τη σύγχυση παλιού CSS/JS από cache.
- Το provider καταγράφει προειδοποίηση όταν λείπει κάποιο στατικό αρχείο, χωρίς αποκάλυψη filesystem paths.
- Τα BlazorUiTests επιβεβαιώνουν ότι τα SVG έχουν προκαθορισμένες διαστάσεις και ότι τα fingerprints ανταποκρίνονται στο πραγματικό περιεχόμενο που σερβίρει το API.

{% hint style="info" %}
Η προσθήκη fingerprint δεν απαιτεί third-party NuGet, JavaScript framework, νέα SQL migration ή αλλαγή δεδομένων. Η έκδοση αλλάζει όταν αλλάζει το πραγματικό περιεχόμενο του αρχείου, όχι επειδή αυξήθηκε ένας hardcoded αριθμός έκδοσης.
{% endhint %}

## Προϋποθέσεις

- .NET 10 SDK, υπάρχον Aenaon ERP και SQL Server 2022 για το Dashboard.
- HTTPS Development API, ήδη δημιουργημένος λογαριασμός admin και έγκυρη AenaonERP_Dev.
- Τα τρία αρχεία branding στο src/Aenaon.Api/wwwroot/images.
- Windows PowerShell και browser Chrome ή Edge.

## 1. Ενημέρωση εφαρμογής

Μετά το merge του Pull Request, σταμάτησε το ήδη εκτελούμενο API με Ctrl+C. Από το root του repository:

~~~powershell
git status
git switch main
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
~~~

{% hint style="warning" %}
Αν εμφανιστούν MSB3021/MSB3027 και κλειδωμένα DLL, βεβαιώσου πρώτα ότι δεν λειτουργεί δεύτερο Aenaon.Api. Μην διαγράφεις αρχεία εκτελέσιμου χωρίς έλεγχο της διεργασίας.
{% endhint %}

## 2. Εκκίνηση και οπτική επαλήθευση

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε https://localhost:7140/login και https://localhost:7140/dashboard.

Έλεγξε ότι:

1. Το κουμπί «Σύνδεση» είναι κανονικού ύψους και το βέλος εμφανίζεται περίπου 18–20px.
2. Τα εικονίδια των KPI «Επιχειρήσεις», «Καταστήματα», «Σύνδεση» έχουν μικρές, ομοιόμορφες διαστάσεις.
3. Το πλαίσιο «Ενεργό πλαίσιο εργασίας» είναι οριζόντιο, με επιχείρηση Aenaon Demo και κατάστημα Κεντρικό / MAIN.
4. Δεν προκύπτει υπερβολική κάθετη κύλιση από μεγάλα εικονίδια.
5. Τα δύο θέματα Light/Dark λειτουργούν χωρίς αλλοίωση των διαστάσεων.

Αν η εικόνα δεν ενημερώθηκε, δοκίμασε μία φορά Ctrl+Shift+R ή DevTools → Network → Disable cache → Refresh. Η νέα υλοποίηση θα πρέπει να ενημερώνει τα URL assets αυτόματα χωρίς μόνιμη ανάγκη hard refresh.

## 3. Έλεγχος δικτύου/browser

Στον Chrome/Edge πάτησε F12 και άνοιξε Network. Επαναφόρτωσε την οθόνη Login.

Εντόπισε το αίτημα του CSS: πρέπει να είναι της μορφής:

~~~text
/css/aenaon.css?v=<16-hex-characters>
~~~

Ανάλογη παράμετρο πρέπει να έχουν τα αρχεία aenaon.js, aenaon-theme-init.js και το favicon.

Στο Elements επέλεξε το SVG βέλος του Login. Επαλήθευσε ότι υπάρχει width=20 και height=20. Στο Computed πρέπει να εμφανίζεται η τελική διάσταση από το CSS, συνήθως 18×18 px μέσα στο κουμπί.

Στο Console δεν πρέπει να υπάρχουν 404 για CSS/JS ή runtime errors.

## 4. Δοκιμή μέσω PowerShell

Χωρίς να απαιτείται login για το CSS, μπορείς να επιβεβαιώσεις ότι τα static files εξυπηρετούνται:

~~~powershell
Invoke-WebRequest -Uri "https://localhost:7140/css/aenaon.css" -Method Head |
    Select-Object StatusCode, Headers
~~~

Το αναμενόμενο HTTP status είναι 200. Μη κοινοποιείς session cookies, password ή CSRF tokens.

## 5. Αρχιτεκτονική

| Αρχείο | Ευθύνη |
| --- | --- |
| Ui/Components/AppIcon.razor | Εγγενείς διαστάσεις διανυσματικών εικονιδίων |
| Ui/UiAssetVersions.cs | SHA-256 fingerprint στατικών αρχείων |
| Ui/App.razor | Προσθήκη versions στα URLs |
| Configuration/ApiServiceCollectionExtensions.cs | Dependency Injection singleton fingerprint provider |
| tests/Aenaon.Api.Tests/BlazorUiTests.cs | HTTP regression tests για SVG και static assets |

Το fingerprint υπολογίζεται μία φορά κατά την πρώτη χρήση του singleton ανά εκτέλεση. Μετά από αλλαγή CSS/JS, επανεκκίνησε το API ώστε να ενημερωθεί το fingerprint. Σε μελλοντικό production hosting τα HTTP cache headers πρέπει να αξιολογηθούν ξεχωριστά πριν ενεργοποιηθεί μακροχρόνιο immutable cache.

## 6. Troubleshooting

| Πρόβλημα | Ενέργεια |
| --- | --- |
| Τεράστια SVG icons | Έλεγξε width/height στο Elements και CSS στο Network |
| Παλιό CSS URL χωρίς version | Το τρέχον App.razor δεν έχει ενημερωθεί ή τρέχει παλιό API process |
| Παλιό fingerprint μετά από αλλαγή CSS | Επανεκκίνησε το API, καθώς το fingerprint υπολογίζεται ανά εκκίνηση |
| 404 για aenaon.css | Έλεγξε wwwroot/css και το Development static file middleware |
| SQL exception στο Dashboard | Έλεγξε τα ήδη αποθηκευμένα User Secrets, όχι την CSS αλλαγή |
| Login/Logout αποτυγχάνει | Δοκίμασε ξανά με HTTPS και έλεγξε την υπάρχουσα antiforgery ροή |

## 7. Checklist πριν από το επόμενο βήμα

- [ ] GitHub CI Windows/Linux + SQL Server 2022 integration tests επιτυχή.
- [ ] Το Login εμφανίζει μικρό εικονίδιο στο κουμπί και σωστό ύψος.
- [ ] Οι KPI κάρτες έχουν κανονικά SVG icons.
- [ ] Το ενεργό πλαίσιο Tenant/Store εμφανίζεται συμπαγές.
- [ ] CSS και JS URLs έχουν fingerprints που αλλάζουν μαζί με το περιεχόμενο.
- [ ] Δεν υπάρχουν HTTP 404/500 για assets.
- [ ] Logout και Tenant/Store authorization παραμένουν λειτουργικά.
- [ ] Δεν υπάρχει database migration.

## 8. Ανάκτηση προηγούμενης έκδοσης

Επειδή η αλλαγή αφορά μόνο HTML/CSS asset references και SVG sizing, μπορεί να γίνει Git revert του αντίστοιχου PR χωρίς SQL rollback. Για rollback στο hosting πρέπει να επανεκκινήσεις τη σωστή έκδοση του API και να επαληθεύσεις τα network requests από browser.
