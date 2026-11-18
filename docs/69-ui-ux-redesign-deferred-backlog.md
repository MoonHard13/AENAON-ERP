# AENAON ERP — Αναβλημένο συνολικό UI/UX Redesign

**Απόφαση ιδιοκτήτη:** 18/11/2026. Η εφαρμογή λειτουργεί αλλά η συνολική εμφάνιση του AENAON ERP εξακολουθεί να χρειάζεται σημαντική επαγγελματική αναβάθμιση. Για να μην καθυστερήσουν τα business features, το **συνολικό UI/UX redesign μεταφέρεται σε ανεξάρτητη μελλοντική φάση**. Η απόφαση **δεν** σημαίνει πλήρες UI acceptance ή άρση των ελέγχων λειτουργικότητας, responsive και accessibility.

**Βάση:** Draft PR #62 (menu builder, owner-reviewed πραγματικό Development video 18/11, ορατό θετικό bulk add, διορθωμένο stretch ανοικτής ERP category form στο source, after-fix video NOT VERIFIED). Stacked Draft PR #63 αρχίζει το 9.6A.2 preview, χωρίς απόφαση merge. Το live Development και η προώθηση Production παραμένουν ανεξάρτητα.

## Σκοπός

Μία συνολική, συνεκτική αναθεώρηση UI/UX σε **όλο το AENAON** αντί για αλλεπάλληλες τοπικές cosmetic παρεμβάσεις σε κάθε feature. Προτεραιότητα σε ενιαίο Design System, επαγγελματική εμφάνιση, ταχύτητα λειτουργίας ταμείου/back office, προσβασιμότητα, σωστό mobile, Light/Dark και keyboard.

## Πεδία προγραμματισμένης επαναξιολόγησης

| Πεδίο | Αναμενόμενη εργασία |
| --- | --- |
| Design System | Τυπογραφία, χρώματα/tokens, spacing, buttons, panels, badges, επιλογές πεδίων, table density, dialogs, responsive breakpoints |
| Πλοήγηση | Sidebar, Topbar, breadcrumbs, active states, επικέντρωση, μηνύματα, desktop/mobile context |
| Κατάλογος & Τιμοκατάλογοι | Κοινή μορφή λιστών, ταξινόμηση, αναζήτηση, inline editing, χρονολογικά δεδομένα, φίλτρα |
| Menu Builder | Ιεραρχία, κάρτες, προεπισκόπηση, επιλογή ERP κατηγοριών/ειδών, bulk flow, confirmation, φόρμες χωρίς stretch/overflow |
| Διαγνωστικά και μεταγενέστερα Features | Κοινά status/empty/loading/error, μη τεχνική ορολογία, display confidence, όχι ανεπαρκής προεπισκόπηση σαν έτοιμη πώληση |
| Accessibility | WCAG 2.2 AA στόχοι, zoom, focus, contrast, tab order, ARIA, screen reader, touch targets |
| Responsive | Πραγματικά viewport 320/375/390/768/1366/1920 και landscape, χωρίς οριζόντιο overflow ή κόψιμο ενεργειών |

## Πολιτική έως τη φάση redesign

- **Διορθώνουμε αμέσως blockers** που καθιστούν αδύνατη μια επιχειρησιακή λειτουργία, αποκρύπτουν κρίσιμα δεδομένα, προκαλούν λάθος αποθήκευση, security/accessibility defect ή μη προσβάσιμο mobile flow.
- **Αναβάλλουμε** καθαρά αισθητικά ζητήματα (χρωματική παλέτα, card density, decorative icons, visual restyling, γενική αισθητική) για τη συνεκτική φάση.
- Καμία νέα δυνατότητα δεν θεωρείται πλήρως αποδεκτή όταν δεν έχουν γίνει οι υποχρεωτικοί λειτουργικοί/ασφαλείας έλεγχοι. Τα γνωστά UI/UX findings και οι ανεξάρτητες visual εκκρεμότητες αναφέρονται **NOT VERIFIED**.
- Διατηρούνται οι σχεδιαστικές αποφάσεις των εγκεκριμένων Manual 58/60 και η απομόνωση Tenant/Store· η συνολική UI αναθεώρηση δεν επηρεάζει SQL contracts/φορολογική πολιτική χωρίς ξεχωριστό review.

## Κριτήρια έναρξης και ολοκλήρωσης της μελλοντικής εργασίας

1. Καθορισμός κύριων οθονών, πραγματικών ροών και αντιπροσωπευτικών dataset.
2. Full component inventory και προτάσεις οριζόντιων διορθώσεων, όχι απλή αύξηση CSS overrides.
3. Εγκεκριμένο visual system light/dark και responsive navigation prototype.
4. Υλοποίηση ανά πλήρη business screen με πραγματική δοκιμή πληκτρολογίου, mobile, zoom, screen reader όπου εφικτό.
5. Πραγματικές εικόνες/screenshots πριν/μετά από την Development εφαρμογή, ξεχωριστά από fixture-based SSR/browser tests.
6. Re-run όλων των κρίσιμων workflows χωρίς regression σε stores/tenants, data integrity, pricing, menu flows.
7. Ρητή owner acceptance. Καμία αυθαίρετη συγχώνευση ή Production promotion.

**Κατάσταση:** BACKLOG, NOT IMPLEMENTED ως συνολικό redesign. Δεν αποδίδεται ψευδές συνολικό UI PASS.
