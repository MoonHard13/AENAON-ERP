# Aenaon ERP — Βήμα 8.4.8: Visual Consistency Polish

## Σκοπός

Το Βήμα 8.4.8 διορθώνει τη διαφορά οπτικού μεγέθους του λογοτύπου όταν αλλάζει το Light/Dark Theme. Επιπλέον, μειώνει την ένταση της σκιάς στο User Menu και βελτιώνει τη γραμματοσειρά μικρών labels. Δεν επηρεάζονται Authentication, Workspace, SQL Server ή άλλες υπάρχουσες λειτουργίες.

## Αίτιο και λύση

Η αρχική dark εικόνα είχε καμβά 960×960, ενώ η light 960×877. Τα διαφορετικά διαφανή περιθώρια προκαλούσαν οπτική μεταβολή, παρότι το CSS όριζε ίδιο πλάτος.

Οι νέες εικόνες κατασκευάστηκαν από τα **αυθεντικά PNG** της MoonHard. Έχουν κοινό διαφανή καμβά 960×960, κοινό οπτικό κέντρο και εξισορρόπηση της ορατής έκτασης με βάση το alpha channel. Διατηρήθηκαν οι αναλογίες και οι λεπτομέρειες, χωρίς stretching. Επειδή τα αρχικά artwork δεν έχουν ακριβώς ίδιο aspect ratio, μπορεί να απομείνει απόκλιση περίπου 2% στο επιμέρους ορατό πλάτος ή ύψος, όχι όμως η προηγούμενη εμφανής αλλαγή μεγέθους.

Το CSS προσθέτει σταθερό aspect-ratio 1:1, object-fit: contain, διακριτικότερη light/dark σκιά στο User Menu και πιο ευανάγνωστα μικρά labels.

{% hint style="warning" %}
Τα δύο νέα WebP είναι δυαδικά αρχεία. Διανέμονται στο ZIP AenaonERP_VisualConsistency_Assets.zip της συνομιλίας, επειδή το GitHub connector δεν μπορεί να πάρει απευθείας τα τοπικά image bytes. **Πρόσθεσε τα δύο WebP στο feature branch πριν από το merge του PR.**
{% endhint %}

## Προϋποθέσεις

- .NET 10 SDK, Visual Studio Code, PowerShell, Git.
- Υπάρχον AenaonERP_Dev με τις τρέχουσες migrations και HTTPS Dev configuration.
- Το ZIP AenaonERP_VisualConsistency_Assets.zip στον φάκελο Downloads.
- Σωστό repository root με AenaonERP.slnx.

## 1. Έλεγχος και αλλαγή branch

~~~powershell
git status
git fetch origin
git switch --track origin/feature/step-08-4-8-visual-consistency
git branch --show-current
~~~

Αν υπάρχει ήδη local branch:

~~~powershell
git switch feature/step-08-4-8-visual-consistency
git pull --ff-only
~~~

Πριν από commit επιβεβαίωσε ότι το branch είναι ακριβώς feature/step-08-4-8-visual-consistency και δεν υπάρχουν άσχετες αλλαγές.

## 2. Εγκατάσταση των δύο normalized WebP

~~~powershell
$repo = (git rev-parse --show-toplevel).Trim()
if (!(Test-Path (Join-Path $repo 'AenaonERP.slnx'))) { throw "Λανθασμένη ρίζα repository: $repo" }
$zip = Get-ChildItem "$env:USERPROFILE\Downloads" -Filter "AenaonERP_VisualConsistency_Assets*.zip" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$zip) { throw "Δεν βρέθηκε το ZIP στις Λήψεις." }
Expand-Archive -LiteralPath $zip.FullName -DestinationPath $repo -Force
~~~

Το ZIP αντικαθιστά μόνο:

- src/Aenaon.Api/wwwroot/images/aenaon-logo-dark.webp
- src/Aenaon.Api/wwwroot/images/aenaon-logo-light.webp

Περιέχει και branding-normalization-report.json με τεχνικές μετρήσεις. Δεν χρειάζεται να προστεθεί αυτό το report στο Git. Το favicon παραμένει ως έχει.

## 3. Git commit των εικόνων

~~~powershell
Test-Path 'src/Aenaon.Api/wwwroot/images/aenaon-logo-dark.webp'
Test-Path 'src/Aenaon.Api/wwwroot/images/aenaon-logo-light.webp'
git diff --stat -- src/Aenaon.Api/wwwroot/images
git add -- src/Aenaon.Api/wwwroot/images/aenaon-logo-dark.webp
git add -- src/Aenaon.Api/wwwroot/images/aenaon-logo-light.webp
git diff --cached --stat
git commit -m "assets(branding): normalize Aenaon light and dark logos"
git push -u origin feature/step-08-4-8-visual-consistency
~~~

{% hint style="danger" %}
Μην κάνεις git add . χωρίς έλεγχο. Μη δημοσιοποιείς appsettings secrets, SQL passwords ή προσωπικά session cookies.
{% endhint %}

## 4. Αυτοματοποιημένοι έλεγχοι

Το νέο BrandingConsistencyTests.cs χρησιμοποιεί HTTP requests για να διαβάσει τα πραγματικά WebP και να επαληθεύσει ισότιμες διαστάσεις καμβά 960×960. Μέχρι να ανεβάσεις τις δύο νέες εικόνες στο branch, το test αναμένεται να αποτυγχάνει, καθώς η παλιά light εικόνα είναι 960×877. Αυτό είναι σκόπιμο για να μη γίνει κατά λάθος merge του CSS χωρίς τα αντίστοιχα assets.

Μετά το push έλεγξε GitHub Actions σε Windows, Ubuntu και isolated SQL Server 2022. Κάνε Squash and merge μόνο εφόσον όλοι οι απαιτούμενοι έλεγχοι είναι επιτυχείς.

## 5. Build και λειτουργικός έλεγχος

Σταμάτησε πρώτα το τρέχον API με Ctrl+C, ώστε να μη μείνουν κλειδωμένα DLL.

~~~powershell
git switch main
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε https://localhost:7140/login και https://localhost:7140/dashboard. Άλλαξε theme με Alt+T και επιβεβαίωσε ότι τα logos έχουν οπτικά ίδια έκταση χωρίς αλλαγή του υπόλοιπου layout. Άνοιξε User Menu στο Dashboard και έλεγξε ότι η σκιά είναι πιο διακριτική, ιδίως στο Light Mode. Έλεγξε Login/Logout και refresh της επιλεγμένης επιχείρησης/καταστήματος.

## 6. Συντομεύσεις

| Συντόμευση | Ενέργεια |
| --- | --- |
| Alt+T | Light/Dark |
| Alt+L | Login |
| Alt+D | Dashboard |
| Alt+O | Logout όταν είναι διαθέσιμη η φόρμα |
| Tab / Shift+Tab | Πλοήγηση μεταξύ controls |
| Enter / Space | Επιλογή κουμπιών |
| Escape | Κλείσιμο User Menu |

## 7. Troubleshooting

| Σύμπτωμα | Αντιμετώπιση |
| --- | --- |
| Διαφορά μεγέθους παραμένει | Βεβαιώσου ότι ανέβηκαν και τα δύο νέα WebP |
| Το CI αποτυγχάνει στο light WebP | Το παλιό 960×877 αρχείο δεν αντικαταστάθηκε σωστά |
| Παλιό CSS | Επανεκκίνησε API και κάνε Ctrl+Shift+R |
| Missing image | Έλεγξε τα δύο αρχεία στο wwwroot/images |
| Build MSB3021/MSB3027 | Κλείσε το άλλο Aenaon.Api πριν ξανακάνεις build |
| Login/SQL error | Έλεγξε υπάρχοντα logs χωρίς να μοιραστείς passwords |

## 8. Ασφάλεια, συντήρηση και rollback

Δεν προστίθενται νέα NuGet packages ή database migrations. Τα SVG icons, HttpOnly cookies, CSRF tokens και Tenant/Store authorization παραμένουν αμετάβλητα. Δεν απαιτείται SQL backup ειδικά για αυτήν την οπτική αλλαγή. Το CSS χρησιμοποιεί το υφιστάμενο SHA-256 asset versioning ώστε να ανανεώνεται στο browser.

Για rollback, κάνε ασφαλές Git revert του PR και επανεκκίνηση της προηγούμενης έκδοσης του API. Δεν απαιτείται SQL rollback.

## 9. Checklist

- [ ] Δύο νέα WebP 960×960 σε ίδιο καμβά.
- [ ] Καμία παραμόρφωση του αυθεντικού artwork.
- [ ] Οι εικόνες και το CSS βρίσκονται στο ίδιο PR.
- [ ] Windows, Ubuntu και SQL integration checks περασμένα.
- [ ] Καμία εμφανής μεταβολή μεγέθους στην εναλλαγή theme.
- [ ] Η σκιά του User Menu είναι διακριτική.
- [ ] Τα Sidebar/footer/store labels είναι ευανάγνωστα.
- [ ] Login, Logout και Workspace λειτουργούν όπως πριν.
- [ ] Καμία αλλαγή στην AenaonERP_Dev.

## 10. Επόμενο βήμα

Μετά το merge και τον οπτικό έλεγχο συνεχίζουμε με την επόμενη επιχειρησιακή ενότητα του ERP, διατηρώντας καθαρό server-side authorization ανά Tenant/Store.
