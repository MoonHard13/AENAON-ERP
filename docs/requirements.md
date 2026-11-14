# Aenaon ERP — Απαιτήσεις και αρχική κατεύθυνση

## Σκοπός

Ανάπτυξη από το μηδέν, βήμα βήμα, ανεξάρτητα από προηγούμενα έργα ή υλοποιήσεις.

## Υποχρεωτική οριζόντια προδιαγραφή — ΑΑΔΕ myDATA & IMPACT

**Κάθε** αλλαγή που αφορά ή μπορεί να επηρεάζει είδη/υπηρεσίες, ΦΠΑ, τιμές, ταμείο, παραγγελίες, POS, παραστατικά, πληρωμές, πιστωτικά, ακυρώσεις, απόθεμα/διακίνηση ή offline λειτουργία σχεδιάζεται και ελέγχεται βάσει:
1. Των πιο πρόσφατων **επίσημων τεχνικών και επιχειρησιακών προδιαγραφών της ΑΑΔΕ myDATA**, συμπεριλαμβανομένων XSD, ERP, παρόχων και ψηφιακής διακίνησης.
2. Της **επίσημης τεκμηρίωσης του παρόχου ηλεκτρονικής τιμολόγησης IMPACT (SoftOne Group)**, του δημόσιου OpenAPI και τυχόν εφαρμοστέας ειδικής τεκμηρίωσης/σύμβασης πελάτη.

Πριν από την υλοποίηση επιβεβαιώνονται έκδοση και ημερομηνία πηγών, απαιτούμενα/conditional πεδία, φορολογικοί χαρακτηρισμοί, φορολογική μεταχείριση, προϋποθέσεις εκτύπωσης/διαβίβασης, χειρισμός αποτυχιών και offline/online ροές. **Κανένα μοντέλο καταλόγου ή schema δεν χαρακτηρίζεται μόνο του φορολογικά συμβατό**. Ο παραπάνω έλεγχος είναι προϋπόθεση design review και PR, ακόμα κι όταν η επίπτωση είναι «καμία» (με αιτιολόγηση).

Δεσμευτικό αναλυτικό checklist: [myDATA & IMPACT Compatibility Gate](18-mydata-impact-compatibility.md).

## Συμφωνημένες απαιτήσεις

| Περιοχή | Απαίτηση |
| --- | --- |
| Κλάδος | Εστίαση, με πώληση προϊόντων και τιμολόγηση υπηρεσιών. |
| Ενότητες | POS εστίασης και Back office στο ίδιο σύστημα. |
| Πρόσβαση | Cloud εφαρμογή από σύγχρονο browser σε υπολογιστές, tablets και κινητά. |
| Επιχειρήσεις | Πολλές ανεξάρτητες επιχειρήσεις, με απομονωμένα δεδομένα, χρήστες και καταστήματα. |
| Παραγγελιοληψία | Ξεχωριστή εφαρμογή Android, με παράδοση ως APK. |
| Υπηρεσίες στο POS | Έκδοση Απόδειξης Παροχής Υπηρεσιών και Τιμολογίου Παροχής Υπηρεσιών μέσα από το POS εστίασης. |
| Διακοπή internet | Συνέχιση παραγγελιών μεταξύ ταμείου, σερβιτόρων και κουζίνας, όσο λειτουργεί το τοπικό δίκτυο και ο τοπικός κόμβος. |
| Επανασύνδεση | Συγχρονισμός με το cloud, με ασφαλή αποφυγή διπλοεγγραφών και διαχείριση συγκρούσεων. |

## Αρχιτεκτονική κατεύθυνση

Η cloud πλευρά θα κρατά την κεντρική διαχείριση. Ανά κατάστημα προβλέπεται τοπικός κόμβος σε συσκευή που παραμένει ενεργή, ώστε το POS, η εφαρμογή σερβιτόρων και η κουζίνα να συνεργάζονται και κατά τη διακοπή της σύνδεσης internet.

Τα δεδομένα, οι άδειες πρόσβασης και οι λειτουργίες κάθε επιχείρησης θα απομονώνονται από τις υπόλοιπες. Η τελική επιλογή απομόνωσης στη βάση δεδομένων θα γίνει μαζί με τη σχεδίαση χρηστών, επιχειρήσεων και καταστημάτων.

Οι υπηρεσίες θα διακρίνονται από τα αποθεματικά είδη. Μια γραμμή υπηρεσίας δεν θα μεταβάλλει το απόθεμα προϊόντων.

## Αποφάσεις που παραμένουν ανοιχτές

- Πάροχος ηλεκτρονικής τιμολόγησης και υποστηριζόμενες φορολογικές ροές.
- Διαδικασία έκδοσης παραστατικών σε διακοπή internet, σύμφωνα με το ισχύον πλαίσιο και τον επιλεγμένο πάροχο.
- Ακριβής αρχιτεκτονική αποθήκευσης και συγχρονισμού, κανόνες συγκρούσεων και ανάκτησης.
- Περιφερειακά: εκτυπωτές κουζίνας/παραστατικών, οθόνες κουζίνας και τερματικά πληρωμών.
- Στοίβα Android εφαρμογής, φιλοξενία cloud και διαδικασία διάθεσης/ενημερώσεων.

## Κριτήρια ολοκλήρωσης του πρώτου βήματος

- Η solution χρησιμοποιεί σταθερό .NET 10 SDK και κάνει restore/build.
- Το API εκκινεί τοπικά με HTTPS.
- Το `/health` επιστρέφει `200 Healthy` και το `/api/info` δημόσιο όνομα εφαρμογής.
- Καταγράφονται εκκίνηση, HTTP status, διάρκεια και trace ID, χωρίς σώματα αιτημάτων, query strings ή διαπιστευτήρια.
- Υπάρχουν integration tests και αυτοματοποιημένος έλεγχος σε Windows και Linux.
- Οι οδηγίες εκτέλεσης είναι πλήρεις και γραμμένες στα Ελληνικά.

Το πρώτο βήμα υλοποιεί τη βάση του API. Οι παραπάνω επιχειρησιακές δυνατότητες αποτελούν απαιτήσεις για επόμενα στάδια.

## Δεύτερο βήμα — Μοντέλα επιχείρησης και καταστήματος

- Ξεχωριστό Domain project με κλάσεις `Tenant` και `Store`.
- Δημιουργία καταστήματος μέσα από την επιχείρηση που το κατέχει.
- Σταθερά αναγνωριστικά και `TenantId` που δεν αλλάζει μέσω δημόσιου setter.
- Κωδικοί καταστημάτων μοναδικοί μέσα στην ίδια συλλογή επιχείρησης, χωρίς διάκριση πεζών/κεφαλαίων.
- Ίδιος κωδικός καταστήματος επιτρέπεται σε διαφορετικές επιχειρήσεις.
- Έλεγχοι ονομάτων/κωδικών πριν τροποποιηθεί η συλλογή καταστημάτων.
- Unit tests για ανεξάρτητες συλλογές, ιδιοκτησία, διπλοεγγραφές και άκυρες εισόδους.

Τα μοντέλα αυτού του βήματος είναι αντικείμενα στη μνήμη. Η απομόνωση δεδομένων σε API/SQL, η εξουσιοδότηση και η μοναδικότητα υπό ταυτόχρονες εγγραφές θα επιβληθούν στα επίπεδα εφαρμογής και βάσης πριν προστεθεί διαχείριση πραγματικών επιχειρησιακών δεδομένων.

## Τρίτο βήμα — Διάγνωση σύνδεσης SQL

- Ξεχωριστή βάση ανάπτυξης `AenaonERP_Dev` σε SQL Server 2022, compatibility level 160, collation `Greek_CI_AS`.
- Τοπική ρύθμιση SQL Authentication μέσω User Secrets, χωρίς διαπιστευτήρια στο repository.
- Κοινή υπηρεσία για έλεγχο σύνδεσης από CLI και Development health endpoint.
- Υποχρεωτική κρυπτογράφηση, περιορισμένοι χρόνοι και διαγνωστικά χωρίς στοιχεία σύνδεσης.
- Επαλήθευση read-only metadata, χωρίς δημιουργία ή μεταβολή schema/δεδομένων από την εφαρμογή.
- Αυτοματοποιημένες δοκιμές, συμπεριλαμβανομένου απομονωμένου SQL Server 2022 στο CI.

## Τέταρτο βήμα — Σχήμα επιχειρήσεων και καταστημάτων

- EF Core SQL Server με versioned migration, snapshot και παραγόμενο SQL για επιθεώρηση.
- Πίνακες `Tenants`, `Stores` και `__EFMigrationsHistory` στην υπάρχουσα βάση.
- Foreign key, μοναδικότητα κωδικού ανά επιχείρηση και διαγραφή χωρίς cascade.
- Development CLI για προεπισκόπηση χωρίς εγγραφές και ρητή εφαρμογή με επιβεβαίωση βάσης.
- Αποτροπή αρχικής migration σε βάση με άσχετα αντικείμενα ή ασύμβατο ιστορικό.
- Πραγματικές δοκιμές SQL στο CI με αποκλειστικά προσωρινά δεδομένα.

Η εξουσιοδότηση, η ασφαλής επιλογή/απομόνωση επιχείρησης και τα business endpoints παραμένουν επόμενα στάδια. Η προσθήκη σχήματος δεν εκθέτει επιχειρησιακά δεδομένα μέσω HTTP.

## Πέμπτο βήμα — Αρχική επιχείρηση και κατάστημα

- Application service για κοινή επικύρωση και συντονισμό αποθήκευσης μέσω interface.
- Development CLI με προεπισκόπηση και εφαρμογή, επιβεβαιώνοντας το όνομα βάσης.
- Αρχική καταχώρηση μόνο σε ενημερωμένη βάση χωρίς επιχειρησιακά δεδομένα.
- Ατομική αποθήκευση επιχείρησης/καταστήματος, serializable transaction και application lock για ταυτόχρονες εκτελέσεις.
- Αποτροπή επανάληψης χωρίς αλλαγή/διαγραφή προηγούμενων εγγραφών.
- Δοκιμές SQL για rollback, ταυτόχρονη καταχώρηση και ασφαλή επανάληψη.

Το βήμα χρησιμοποιεί το υπάρχον σχήμα, χωρίς νέα migration. Οι χρήστες, η εξουσιοδότηση και η ασφαλής διαχείριση πρόσθετων επιχειρήσεων από HTTP θα ακολουθήσουν.

## Έκτο βήμα — Σχήμα λογαριασμών και συμμετοχών

- Identity user-only context με GUID accounts και σταθερή έκδοση του επίσημου EF store.
- Νέα migration με τέσσερις πίνακες Identity και `TenantMemberships`, χωρίς αλλαγή υφιστάμενων business εγγραφών.
- Σύνθετος primary key `(TenantId, UserId)`, έγκυροι ρόλοι `Owner`/`Member` και foreign keys χωρίς cascade memberships.
- Ο λογαριασμός μπορεί να συμμετέχει σε πολλές επιχειρήσεις, με ανεξάρτητο ρόλο ανά επιχείρηση.
- Unique normalized username και unique normalized email όταν υπάρχει τιμή.
- Πραγματικό SQL upgrade test από την προηγούμενη έκδοση με διατήρηση δεδομένων και έλεγχος του Identity user-store.

Η δημιουργία διαχειριστή, οι ροές authentication/session και η επιβολή εξουσιοδότησης παραμένουν επόμενα βήματα. Τα membership records δεν απονέμουν από μόνα τους πρόσβαση σε HTTP endpoints.


## Έβδομο βήμα — Ασφαλής δημιουργία πρώτου διαχειριστή

- Μία αρχική λειτουργία Development CLI με --dry-run και --apply με ρητή επιβεβαίωση βάσης.
- Χρήση ASP.NET Core Identity για πραγματικό salted password hash· χωρίς plaintext σε αρχεία ή SQL.
- Έλεγχος TenantId, login/email, schema και κενών λογαριασμών/memberships.
- Ατομική SQL transaction και application lock για αποτροπή μερικής ή ταυτόχρονης εγγραφής.
- Μία εγγραφή TenantMembership με ρόλο Owner στην επιλεγμένη επιχείρηση.
- Διπλή εισαγωγή κωδικού χωρίς εμφάνιση plaintext και άρνηση εκτέλεσης με redirected stdin.
- Automated tests για επικύρωση, CLI, rollback, Identity hashing, μοναδικότητα και ταυτόχρονη αρχικοποίηση.

Η δημιουργία λογαριασμού **δεν** συνιστά login. Η ασφαλής authentication, η εξουσιοδότηση ανά επιχείρηση/κατάστημα και τα προστατευμένα endpoints παραμένουν επόμενα στάδια.


## Βήμα 8.1 — Authentication Services

- Κοινό interface ICredentialVerifier στην Application και κλάση υλοποίησης μέσω ASP.NET Core Identity στην Infrastructure.
- Αποτέλεσμα επιτυχίας με UserId, χωρίς cookie/session, token ή tenant authorization.
- Έλεγχος με UserManager, καταγραφή αποτυχιών, lockout μετά από 5 λανθασμένους κωδικούς για 15 λεπτά και reset μετά από επιτυχία.
- Προστασία από username enumeration με ενιαίο αποτέλεσμα αποτυχίας και τεχνικό dummy hash.
- Διαδραστικό Development CLI για έλεγχο του πραγματικού κωδικού, με ρητή επιβεβαίωση βάσης.
- Εφάπαξ, Development-only διόρθωση του αρχικού placeholder email για τον μοναδικό Owner της υπάρχουσας επιχείρησης, μέσω UserManager και μετά από επαλήθευση τρέχοντος κωδικού.
- Προεπισκόπηση, επιβεβαίωση βάσης, έλεγχοι SQL, logging χωρίς secrets, unit και απομονωμένα SQL integration tests.
- Καμία νέα migration, κανένα δημόσιο HTTP login endpoint και καμία αυτόματη επιβεβαίωση email.

Τα tenant-aware authorization, cookie authentication, antiforgery, login rate limiting, email verification και MFA αποτελούν επόμενα βήματα.


## Βήμα 8.2 — Tenant Authorization

- Εφαρμογή deny-by-default και έλεγχος membership πριν από κάθε επιτρεπόμενη πρόσβαση.
- Βασικές πολιτικές Read (Owner/Member) και Manage (μόνο Owner).
- Προαιρετικός έλεγχος StoreId με υποχρεωτική αντιστοίχιση στο TenantId.
- Ασφαλής λίστα επιχειρήσεων μόνο για χρήστη με ήδη επιβεβαιωμένη ταυτότητα.
- Ξεχωριστό Application service, EF Core Infrastructure store και Development CLI που ζητά password και επιβεβαίωση βάσης.
- Χωρίς migration και χωρίς δημόσια endpoints ή session. Η αυθεντικοποίηση πρέπει να προηγείται οποιασδήποτε κλήσης authorization.
- Unit tests και πραγματική SQL Server integration δοκιμή με δύο επιχειρήσεις, διαφορετικά καταστήματα και ρόλους.
- **Δεν** υπάρχουν ακόμη store-specific memberships. Οι Owner/Member της επιχείρησης έχουν Read πρόσβαση σε όλα τα δικά της καταστήματα. Οι πιο λεπτομερείς άδειες απαιτούν επόμενη φάση σχεδίασης.


## Βήμα 8.3 — HTTPS Login/Logout και Secure Sessions

- Development-only HTTP endpoints για CSRF token, login, logout, session identity και tenant access.
- Ασφαλές Cookie Authentication με host-only Secure HttpOnly cookie, SameSite=Strict, λήξη 30 λεπτών χωρίς sliding renewal.
- Identity password verification, ενεργό membership, επανέλεγχος security stamp και δυνατότητα ανάκλησης συνεδρίας.
- ASP.NET Core Antiforgery σε κάθε POST login/logout με ειδικό request header και ανανέωση token μετά login.
- Rate limiting έως 10 auth requests ανά λεπτό ανά IP, ανεξάρτητα από το υπάρχον account lockout.
- Καμία μεταβίβαση UserId από μη έμπιστη είσοδο για χρήση σε authorization.
- Integration tests HTTP, ασφάλειας cookies/CSRF/rate limit και πραγματικό SQL Server CI για ανάκληση.
- Χωρίς νέα migration και χωρίς δημόσιο production login μέχρι να ρυθμιστούν φιλοξενία, HTTPS proxy, shared Data Protection keys, audit και λοιποί έλεγχοι.


## Βήμα 8.4 — Blazor Login UI και πρώτο Dashboard

- Blazor Static SSR UI στο ίδιο origin με το ASP.NET Core API, χωρίς νέα NuGet packages.
- Ασφαλές HTML POST/redirect/GET με IAntiforgery και το υφιστάμενο Identity cookie χωρίς password σε JavaScript.
- Responsive οθόνη Login, protected Dashboard και επιλογή πραγματικών επιχειρήσεων/καταστημάτων από SQL.
- Υποχρεωτικό TenantAuthorizationService πριν από την ανάγνωση καταστημάτων, απόρριψη ξένου TenantId.
- Επίσημο πράσινο/μεταλλικό Aenaon ERP branding, dark/light theme, προσιτότητα και συντομεύσεις Alt+T/L/D/O.
- Development-only UI μέχρι production readiness με προστασία keys, HTTPS, proxy και security hardening.
- GitHub PR κώδικα + ZIP binary branding για προσθήκη στο feature branch πριν από merge.
- Unit/HTTP integration tests και ελληνικό GitBook manual.


## Βήμα 8.4.5 — UI/UX Hardening

- Σαφής ενεργή επιχείρηση και ενεργό κατάστημα με επιλογή μέσω HTTPS link.
- TenantWorkspaceResolver στο Application layer: μη επιτρεπόμενο tenant/store απορρίπτεται χωρίς σιωπηλό fallback, κάθε query συνεχίζει να ελέγχει membership.
- Responsive βελτιώσεις, ενιαία SVG icons και light/dark theme χωρίς flash.
- Σωστή εμφάνιση Chrome/Edge autofill και βελτιωμένη αντίθεση Light Mode.
- Φιλικό σφάλμα για υπέρβαση rate limit στη SSR φόρμα και Retry-After στο API.
- Επαλήθευση request size 8192 bytes στα POST login/logout.
- Ανάκτηση κουμπιού login σε stalled navigation και επίσημη έκδοση από .NET assembly.
- Unit και HTTP integration tests για ενεργό Tenant/Store, άκυρους πόρους, rate limiting, theme και ανακατεύθυνση.
- Καμία migration ή αλλαγή στην AenaonERP_Dev· UI παραμένει μόνο σε Development.


## Βήμα 8.4.6 — SVG sizing και Static Asset Cache

- Εγγενείς SVG διαστάσεις width=20/height=20 στο κοινό AppIcon για ασφάλεια εμφάνισης ακόμη και χωρίς νέο CSS.
- Singleton UiAssetVersions με SHA-256 fingerprint από τα πραγματικά CSS/JS/favicon αρχεία.
- Versioned HTML URLs για aenaon.css, aenaon.js, aenaon-theme-init.js και favicon ώστε νέα αρχεία να μη συγχέονται με παλιότερα του browser cache.
- HTTP regression tests για πραγματικό περιεχόμενο static assets και διαστάσεις SVG.
- Ελληνικό GitBook manual, χωρίς νέα migrations, NuGet packages ή αλλαγή της AenaonERP_Dev.


## Βήμα 8.4.7 — UI Refinement & Workspace Persistence

- Πλήρες Light/Dark Login (και στις δύο πλευρές), χρήση του σωστού επίσημου logo.
- User Menu στο avatar/username, ασφαλές POST/CSRF logout εντός του μενού, χωρίς νέα μη λειτουργικά account links.
- Responsive mobile navigation με προσβάσιμο details/summary, κλείσιμο με Escape ή click έξω.
- Authenticated CSRF-protected POST για επιλογή Tenant/Store, server-side έλεγχος membership και catalog.
- User-bound, Data Protection encrypted, Secure HttpOnly SameSite=Strict workspace cookie, διάρκειας 30 λεπτών.
- Διατήρηση επιλογής σε Sidebar/refresh/Alt+D, όχι σιωπηλό fallback σε ξένους πόρους.
- Διαγραφή workspace preference στο Login/Logout (αφορά και την JSON session ροή).
- HTTP regression tests για persistence, CSRF, ξένο Tenant/Store, user menu και logout.
- Development-only, χωρίς migrations ή νέα εξωτερικά NuGet packages.


## Βήμα 8.4.8 — Visual Consistency Polish

- Κανονικοποίηση αυθεντικών light/dark MoonHard logo PNG σε δύο WebP ίδιου 960×960 transparent canvas.
- Διατήρηση αναλογιών, οπτικού κέντρου και παρόμοιου οπτικού εμβαδού χωρίς stretching του artwork.
- Μικρότερη σκιά User Menu στο Light Mode και σωστή ένταση στο Dark Mode.
- Βελτίωση μικρών labels σε Sidebar, footer, store badge και account menu.
- HTTP regression test που ελέγχει τις πραγματικές διαστάσεις των δυαδικών branding assets (θα αποτύχει μέχρι να ανέβουν στο feature branch).
- Ελληνικό GitBook manual, binary asset ZIP και χειροκίνητος έλεγχος σε Light/Dark.
- Χωρίς νέες migrations, NuGet packages ή αλλαγές authentication / Tenant / Store.

## Βήμα 9.1 — Product Catalog Domain Foundation

- Ξεχωριστό Domain namespace `Aenaon.Domain.Catalog`, χωρίς SQL, HTTP ή εξωτερικές βιβλιοθήκες.
- `ProductCatalog` με αμετάβλητη ιδιοκτησία `TenantId` και προστατευμένες συλλογές κατηγοριών/ειδών.
- Είδη τύπου `Product` ή `Service`, με κοινή μοναδικότητα κωδικού **ανά επιχείρηση** και ανεξάρτητη από κατηγορία/τύπο.
- Ενεργές/ανενεργές κατηγορίες και είδη, αλλαγές μέσω του catalog και έλεγχοι εγκυρότητας πριν από κάθε μετάβαση.
- Υποχρεωτική έγκυρη ενεργή κατηγορία για νέο είδος ή μετακίνηση· αποτροπή χρήσης κατηγορίας ξένης επιχείρησης.
- Υπηρεσίες χωρίς `TracksStock`. Τα προϊόντα μπορούν να δηλωθούν stock-managed, χωρίς ακόμη πραγματικές κινήσεις αποθήκης.
- MSTest για tenant isolation, validations, διπλούς κωδικούς, μη έγκυρους τύπους, ασφαλείς αλλαγές και read-only collections.
- Καμία migration, αλλαγή στην `AenaonERP_Dev`, UI/API endpoints ή νέες NuGet εξαρτήσεις.

Η μοναδικότητα ισχύει προς το παρόν ανά in-memory catalog και **δεν** υποκαθιστά SQL constraints σε ταυτόχρονες εγγραφές. Η αποθήκευση SQL, οι foreign keys, η σύγκριση σύμφωνα με την πραγματική collation και η ασφαλής πρόσβαση Owner/Member προβλέπονται για το Βήμα 9.2. Τιμές, ΦΠΑ και διαθεσιμότητα ανά κατάστημα θα ακολουθήσουν σε μεταγενέστερα στάδια.

## Βήμα 9.2 — Product Catalog SQL Server Persistence

- Επέκταση του AenaonDbContext με DbSet για CatalogCategories και CatalogItems.
- Τρίτη forward-only EF Core migration με δύο νέους πίνακες, χωρίς μεταβολή ή διαγραφή υφιστάμενων δεδομένων.
- Unique index ανά (TenantId, Code), με Greek_CI_AS στους κωδικούς.
- Αμετάβλητη ιδιοκτησία κατηγορίας και είδους, foreign keys προς Tenants χωρίς cascade.
- Σύνθετο FK (TenantId, CategoryId) των ειδών προς (TenantId, Id) των κατηγοριών, ώστε να απαγορεύονται cross-tenant references.
- SQL CHECK για αναγνωριστικά, κωδικούς, ονόματα, Kind και αποτροπή stock tracking υπηρεσιών.
- Διατήρηση υπάρχοντος Development CLI --migrate-database --dry-run / --apply και ρητής επιβεβαίωσης βάσης.
- Αναβάθμιση CI SQL tests για τρεις migrations, μη καταστροφική αναβάθμιση, SQL constraints και πραγματική επαναφόρτωση.
- Καμία αυτόματη migration κατά την εκκίνηση, νέα NuGet εξάρτηση, νέο UI/API ή πρόσβαση στη βάση του χρήστη από CI.
- CRUD εφαρμογής, authorization και οθόνες καταλόγου θα προστεθούν σε επόμενο στάδιο πριν δοθεί πρόσβαση σε πραγματικούς χρήστες.

## Βήμα 9.3 — Fiscal Catalog Design Gate

- Υποχρεωτικός τεκμηριωμένος σχεδιασμός φορολογικής πληροφορίας **πριν** από οποιαδήποτε επίδραση σε παραστατικά, ΦΠΑ ή IMPACT requests.
- Το Domain `Product/Service` δεν αρκεί για φορολογική ταξινόμηση ή αυτόματη επιλογή ΦΠΑ.
- Χωριστά reference systems για VAT category, VAT exemption, income classification, document type και IAPR / EN16931 measurement units, με εκδόσεις/ημερομηνίες ισχύος.
- Επίλυση φορολογικής μεταχείρισης με βάση tenant, store, ημερομηνία, κανάλι, τύπο παραστατικού και συναλλαγή, με ρητή αποτυχία όταν δεν υπάρχει έγκυρος κανόνας.
- Διακριτό snapshot φορολογικών γραμμών και σαφής διάκριση FNB παραγγελίας από φορολογικό παραστατικό.
- Επιβεβαίωση επίσημων προδιαγραφών myDATA 2.0.2 και δημόσιου IMPACT OpenAPI κατά τον σχεδιασμό, με επανέλεγχο πριν από implementation.
- Το παρόν είναι design-only PR, χωρίς αλλαγές σε SQL, API, UI ή φορολογικές αποστολές. Η υλοποίηση ακολουθεί σε επόμενη ελεγχόμενη φάση.

Αναλυτική περιγραφή: [Βήμα 9.3 — Fiscal Catalog Design Gate](19-fiscal-catalog-design.md).

## Βήμα 9.3A — Fiscal Domain Contracts & Tests

- Αμετάβλητο `FiscalReferenceCode` με ρητά χωριστά `FiscalCodeSystem`, `FiscalReferenceSource`, `SourceVersion` και χρονική ισχύ.
- Αμετάβλητο `FiscalTransactionContext` για Tenant/Store/Item, τύπο είδους, ημερομηνία, B2C/B2B/B2G και διάκριση απλής παραγγελίας από φορολογικό έγγραφο.
- `FiscalProfileCandidate`: **προσωρινή, μη εγκεκριμένη** αντιστοίχιση VAT category, income category/type, προαιρετικής απαλλαγής και μονάδων μέτρησης.
- Validation δομής/ταυτοποίησης/χρονικής ισχύος με σταθερά domain error codes, αποτροπή χρήσης προφίλ ξένου Tenant/Item και προστασία από χρήση σε παραγγελία.
- Αποφυγή προεπιλεγμένου ΦΠΑ, hardcoded έγκυρων συνδυασμών ή αυτόματης μετατροπής Product/Service σε χαρακτηρισμό.
- Χωρίς SQL migration, HTTP endpoints, αποθήκευση στοιχείων, διαβίβαση σε IMPACT ή αλλαγή του `AenaonERP_Dev`.
- MSTest για εγκυρότητα codes, διαχωρισμό namespaces, περιόδους, tenant scope, order-vs-invoice.
- Συμβατότητα: σχεδιάστηκε βάσει myDATA v2.0.2 και επίσημων IMPACT VAT/classification/measurement unit/conditional field pages, με επανέλεγχο κατά την υλοποίηση.

**Οι συμβάσεις δεν αποτελούν φορολογική πιστοποίηση ούτε έλεγχο επιτρεπόμενων κωδικών ή συνδυασμών.** Αυτά απαιτούν versioned reference catalog και ειδική policy/contract tests στα επόμενα βήματα.

## Επικαιροποίηση κατάστασης — 14/11/2026 (μετά τα PR #48/#49)

Οι παραπάνω αρχικές απαιτήσεις έως το 9.3A διατηρούνται **ως ιστορικό σχεδιασμού**. Η [σημερινή κατάσταση](00-current-state-and-roadmap.md) και τα εγχειρίδια των μεταγενέστερων βημάτων υπερισχύουν για διαθέσιμες δυνατότητες.

| Βήματα | Σημερινή υλοποίηση | Όρια |
| --- | --- | --- |
| 9.3B–9.3C | Fiscal staging, offline XLSX/myDATA/IMPACT diagnostics | 254 review observations παραμένουν Pending, καμία φορολογική έγκριση |
| 9.4A–9.4C.4 | Κατάλογος: ανάγνωση, CRUD, κατηγορίες/ιεραρχία, αρίθμηση, sorting | Development-only, Tenant/Store isolation |
| 9.5A–9.5A.2 | Μονάδες μέτρησης, λογιστικοί τύποι, ενιαία dropdowns, UI | Όχι αυτόματο ΦΠΑ/Ε3/myDATA |
| 9.5B.1–9.5B.2 | Πολλαπλοί τιμοκατάλογοι, SQL, GUI και CLI | Όχι χρήση σε παραστατικά |
| 9.5B.3A–9.5B.3B | Read-only PriceResolutionService και Blazor price preview | Ισοβαθμίες: Ambiguous, χωρίς SQL writes, taxes ή price snapshots |

**Σχήμα:** Στο `main` υπάρχουν **8 migrations** μέχρι `20261113150000_PriceListsFoundation`. Εφαρμογή όλων στη βάση `AenaonERP_Dev` δεν έχει επιβεβαιωθεί μέσω του GitHub — απαιτείται πραγματικός SQL/CLI έλεγχος.

### Ανοιχτές απαιτήσεις / μελλοντικός σχεδιασμός

- **9.5C.1 — Catalog & Pricing Gap Analysis:** [Manual 51](51-catalog-pricing-gap-analysis.md), τεκμηριώνει πραγματικά πεδία και κενά χωρίς μεταβολές SQL/κώδικα. 
- **Πριν από το 9.6A:** ολοκλήρωση 9.5C.2–9.5C.5 (barcode, long description, τιμολογιακό ιστορικό, έλεγχοι επικαλύψεων και readiness).
- **Μεταγενέστερο 9.6A:** Μενού εστίασης, ομάδες, παραλλαγές, πρόσθετα, επιλογές και διαθεσιμότητα κατά store/channel. **Δεν έχει υλοποιηθεί**· απαιτεί προηγούμενο design review.
- **Παραγγελιοληψία / POS / Android APK / offline:** απαιτούν μοντέλα παραγγελίας, immutable εμπορικά snapshots, συγχρονισμό, προστασία επανάληψης και σχεδιασμό συσκευών.
- **Φορολογικά:** επικαιροποίηση και έλεγχος επίσημων myDATA/IMPACT πηγών, νόμιμων συμβολαίων, ΦΠΑ, παραστατικών και τελών **πριν από έκδοση ή διαβίβαση**. Το [compliance gate](18-mydata-impact-compatibility.md) παραμένει υποχρεωτικό.
- **Περιβαλλοντικά τέλη:** μόνο [design](47-environmental-fees-fiscal-design.md), χωρίς αυτόματους κανόνες ή SQL changes.
- **Excel και μαζικές εργασίες:** [αναβλήθηκαν](48-bulk-actions-and-excel-import-plan.md), μέχρι να ολοκληρωθούν οι βασικές δυνατότητες.
- **Παραγωγική εγκατάσταση:** δεν έχει εγκριθεί production hosting, SLAs ή πλήρης production security readiness.

**Κανόνας διατήρησης documentation:** Κάθε merged feature επικαιροποιεί το `README.md` και τον [χάρτη](00-current-state-and-roadmap.md). Τα step-by-step manuals παραμένουν ιστορικά τεκμήρια. Τα GitBook starter templates δεν θεωρούνται τεκμηρίωση AENAON πριν από επιβεβαίωση του ενεργού site.
