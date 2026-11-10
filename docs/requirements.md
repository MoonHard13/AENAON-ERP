# Aenaon ERP — Απαιτήσεις και αρχική κατεύθυνση

## Σκοπός

Ανάπτυξη από το μηδέν, βήμα βήμα, ανεξάρτητα από προηγούμενα έργα ή υλοποιήσεις.

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
