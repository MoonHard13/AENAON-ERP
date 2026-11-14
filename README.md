# Aenaon ERP

Cloud ERP υπό ανάπτυξη για πολλές ανεξάρτητες επιχειρήσεις εστίασης. Διαθέτει Development Back Office και υποστήριξη προϊόντων/υπηρεσιών· το POS και η εφαρμογή σερβιτόρων είναι **μελλοντικές λειτουργίες**, όχι διαθέσιμο προϊόν.

## Υποχρεωτική συμβατότητα ΑΑΔΕ myDATA / IMPACT

Όλες οι σχετικές υλοποιήσεις του Aenaon ERP πρέπει να ελέγχονται ως προς τις ισχύουσες **επίσημες τεχνικές προδιαγραφές myDATA** και το **API/τεκμηρίωση του παρόχου IMPACT**. Αυτή είναι οριζόντια αρχιτεκτονική απαίτηση, όχι προαιρετική μεταγενέστερη ενσωμάτωση.

Δες το [myDATA & IMPACT Compatibility Gate](docs/18-mydata-impact-compatibility.md) και το υποχρεωτικό GitHub PR checklist. Οι εμπορικές λειτουργίες καταλόγου και τιμών δεν αποτελούν εγκεκριμένο φορολογικό mapping ή δυνατότητα έκδοσης παραστατικών. Οι fiscal έλεγχοι παραμένουν offline / υπό ανεξάρτητη έγκριση.

## Βήμα 9.3 — Fiscal Catalog Design Gate

Η [φορολογική θεμελίωση καταλόγου](docs/19-fiscal-catalog-design.md) καταγράφει το αποδεκτό μοντέλο χαρτογράφησης **myDATA / IMPACT** πριν από αλλαγές σε ΦΠΑ, χαρακτηρισμούς, μονάδες μέτρησης, SQL ή τιμολόγηση. **Design-only**: δεν υλοποιούνται ακόμη φορολογικοί υπολογισμοί ούτε εκδίδονται παραστατικά.

## Βήμα 9.3A — Fiscal Domain Contracts & Tests

Τα Domain contracts `FiscalReferenceCode`, `FiscalTransactionContext`, `FiscalProfileCandidate` διαχωρίζουν **συστήματα φορολογικών κωδικών**, πηγές/εκδόσεις/περιόδους, **παραγγελίες από φορολογικά παραστατικά** και φορολογικά προφίλ ανά επιχείρηση. Περιλαμβάνονται MSTest regression tests.

**Δεν υπολογίζουν νόμιμο ΦΠΑ ούτε επικυρώνουν επιτρεπόμενους φορολογικούς συνδυασμούς.** Δεν προστέθηκε migration, UI, HTTP ή IMPACT call. Ο έλεγχος `ValidateScope` εξασφαλίζει μόνο σωστή ταυτοποίηση/χρονική ισχύ, όχι έγκριση έκδοσης.

Οδηγίες: [Fiscal Domain Contracts](docs/20-fiscal-domain-contracts.md).

## Βήμα 9.3B — Fiscal Reference Data & Tax Mapping

Το [Fiscal Reference Data & Tax Mapping](docs/21-fiscal-reference-mapping.md) εισάγει versioned registry φορολογικών αναφορών και προσωρινά tenant-safe mappings με τέταρτη EF Core migration, περιορισμούς SQL και read-only preview. Καμία φορολογική πρόταση δεν επιτρέπεται να εκδώσει παραστατικό, δεν υπάρχουν πραγματικά seed δεδομένα ή κλήσεις σε ΑΑΔΕ/IMPACT.

## Βήμα 9.3C.1 — Offline Fiscal Reference Evidence Inspection

Η [τοπική προεπισκόπηση τεκμηρίων φορολογικών κωδικών](docs/22-fiscal-import-evidence-inspection.md) ελέγχει JSON manifests έναντι των πραγματικών bytes του πρωτοτύπου αρχείου με SHA-256, namespaces, πηγές, εκδόσεις και περιόδους, χωρίς εισαγωγή SQL, ενεργοποίηση mapping ή δυνατότητα τιμολόγησης. Δεν υπάρχει νέα migration και δεν θεωρείται φορολογική έγκριση.

## Βήμα 9.3C.2 — Fiscal Staging & Independent Source Review

Η [απομονωμένη προσωρινή καταχώριση και ανεξάρτητη επιθεώρηση πηγής](docs/23-fiscal-staging-review.md) εισάγει 5η SQL migration με batch, γραμμές και audit απόφασης. Η εξουσιοδότηση υποβολής/ελέγχου είναι **default deny**, χωρίς δημόσιο API ή CLI writes, και η αποδοχή τεκμηρίου δεν σημαίνει φορολογική έγκριση ή ενεργοποίηση τιμολόγησης.

## Βήμα 9.3C.3A — Candidate myDATA Classification Matrix Preview

Το [μη φορολογικά ενεργό, τοπικό inspector συνδυασμών](docs/24-classification-matrix-preview.md) ελέγχει JSON αποδελτίωση έναντι του SHA-256 του πρωτότυπου XLS της ΑΑΔΕ. Δεν αντιπαραβάλλει αυτόματα τα πραγματικά κελιά Excel, δεν εγκρίνει κανόνες, δεν γράφει στη SQL και δεν αλλάζει τις 5 εφαρμοσμένες migrations.

## Βήμα 9.3C.3B.1 — Official XLSX Structure Inspection

Το [read-only εργαλείο επιθεώρησης επίσημου XLSX](docs/25-official-xlsx-structure-inspection.md) διαβάζει πραγματική OOXML δομή, φύλλα, κελιά και SHA-256 χωρίς εξωτερικά πακέτα ή νέα migration. **Δεν επαληθεύει ακόμη φορολογικούς συνδυασμούς** και δεν επιτρέπει έκδοση.

## Βήμα 9.3C.3B.2 — Complete AADE XLSX Evidence Export

Το [εργαλείο πλήρους, read-only εξαγωγής XLSX σε JSON](docs/26-complete-xlsx-evidence-export.md) κρατά όλα τα μη κενά κελιά, συγχωνευμένες περιοχές και δείκτες μορφοποίησης, επιβάλλει πλήρη συμφωνία counts και διαχωρίζει υποψήφιες κατηγορίες εσόδων/εξόδων από ειδικές οδηγίες προς έλεγχο. Η προεπισκόπηση συνεχίζει να δείχνει 30 δείγματα ανά φύλλο, **με ρητή επισήμανση** της περικοπής· `--all` και `--export-json` εμφανίζουν/αποθηκεύουν ολόκληρα τα δεδομένα. Χωρίς migrations, SQL writes, tax approval ή έκδοση.

## Βήμα 9.3C.3B.3 — Original XLSX / JSON Evidence Reconciliation

Το [read-only εργαλείο ανεξάρτητης συμφωνίας](docs/27-xlsx-original-evidence-reconciliation.md) επανελέγχει το **πρωτότυπο XLSX** έναντι του πλήρους JSON κελί προς κελί, με SHA-256, συγχωνεύσεις, styles, υποψήφια ζεύγη και παρατηρήσεις. Η επαλήθευση αποδεικνύει μόνο **τεχνική συμφωνία**, ποτέ φορολογική έγκριση ή αυθεντικότητα πηγής. Δεν προστίθεται migration, SQL write ή provider call.

## Βήμα 9.3C.3B.4 — Semantic Review Triage

Ο [φάκελος ειδικών οδηγιών myDATA](docs/28-fiscal-semantic-review-triage.md) αναλύει αποκλειστικά **τεχνικά συμφωνημένα** XLSX/JSON, ταξινομεί κάθε εκκρεμότητα, διατηρεί SHA-256 + ακριβή κελιά και προτεραιότητες ανθρώπινου ελέγχου. Όλα τα review items παραμένουν **Pending**, χωρίς αυτοματοποιημένη φορολογική έγκριση, SQL migration, πάροχο ή έκδοση.

## Βήμα 9.3C.3B.5A — Read-only Fiscal Review Worklist

Ο [οδηγός Excel-compatible CSV για ανεξάρτητους reviewers](docs/29-fiscal-review-worklist-csv.md) περιγράφει πώς τα 254 μη εγκεκριμένα review items μετατρέπονται σε **Pending λίστα εργασίας** με ακριβές provenance, ελληνικό UTF-8, ασφαλές escaping Excel formulas και κενά πεδία ανθρώπινης τεκμηρίωσης. Κανένα CSV δεν εισάγεται ως έγκριση, καμία νέα migration, SQL write, IMPACT call ή φορολογική ενεργοποίηση.

## Βήμα 9.3C.3B.5B — IMPACT Contract Preflight

Ο [read-only κατάλογος συμβατότητας συμβολαίου IMPACT](docs/30-impact-contract-preflight.md) αποτυπώνει τις ρητές απαιτήσεις `EliseCoreDocument` από τα official docs για `base`, `invoice` και `b2g`, ξεχωρίζοντας **υποχρεωτικά**, **υπό όρους** πεδία και ανεξάρτητους manual-review ελέγχους. **Δεν πραγματοποιεί καμία πραγματική επικύρωση παραστατικού**, δεν κάνει SQL/API writes και δεν εγκρίνει φορολογική έκδοση.

## Βήμα 9.3C.3B.5C — Offline IMPACT JSON Structural Preflight

Ο [offline validator υποσυνόλου της δομής EliseCoreDocument](docs/31-impact-offline-payload-preflight.md) εξετάζει συνθετικά JSON μόνο σε Development, με explicit `base/invoice/b2g` και `unknown/business/retail` contexts. Ελέγχει υποχρεωτικά πεδία, βασικούς τύπους, B2G και πιθανό διπλό tax placement. **Πάντα απαιτείται ανεξάρτητος έλεγχος και έγκριση** ΑΑΔΕ/IMPACT· κανένα report, JSON ή exit code δεν δίνει δυνατότητα έκδοσης.

## Βήμα 9.3C.3B.5D.1 — IMPACT IncomeClassification Source Reconciliation

Η [σύγκριση επίσημων περιγραφών και παραδειγμάτων IMPACT](docs/32-impact-classification-source-reconciliation.md) επεκτείνει **μόνο** τα offline diagnostic findings για επίπεδο έναντι ένθετου `ClassificationTypeCode` και για την αμφισημία της υποχρεωτικότητας `Amount`. Οι εναλλακτικές μορφές **δεν εγκρίνονται αυτομάτως**. Παραμένουν 254 Pending myDATA observations, όλα τα flags έκδοσης `false`, χωρίς SQL ή provider calls.

## Βήμα 9.3C.3B.5D.2 — IMPACT Conditional Field Presence Evidence

Ο [οδηγός υπό όρους πεδίων IMPACT](docs/33-impact-conditional-field-evidence.md) τεκμηριώνει τη συμπληρωματική **μη δεσμευτική offline καταγραφή παρουσίας** 37 υπό όρους πεδίων στο InvoiceIapr (36 στο B2G), χωρίς ανάγνωση/αποθήκευση τιμών στην αναφορά. Οι 6 εκκρεμείς έλεγχοι, οι 254 μη εγκεκριμένες παρατηρήσεις myDATA και όλες οι απαγορεύσεις φορολογικής έκδοσης **παραμένουν αμετάβλητες**.

## Βήμα 9.4A — Tenant-safe Read-only Catalog Preview

Το [εγχειρίδιο προεπισκόπησης καταλόγου](docs/34-catalog-read-preview.md) περιγράφει το νέο `CatalogReadService` και το Development CLI `--preview-catalog`: πραγματικός Identity credential check, Tenant Read authorization **πριν** από την ανάγνωση, EF Core `AsNoTracking` με υποχρεωτικό φίλτρο `TenantId`, σελίδωση και αναζήτηση ειδών/υπηρεσιών/κατηγοριών. Δεν γίνεται καμία μεταβολή καταλόγου, SQL migration, ΦΠΑ ή έκδοση παραστατικού.

## Βήμα 9.4B — Blazor Back Office Catalog (Read-only)

Το [εγχειρίδιο του Blazor Back Office Catalog](docs/35-blazor-back-office-catalog.md) παρουσιάζει τη νέα προστατευμένη οθόνη **`/catalog`** με καρτέλες προϊόντων/υπηρεσιών/κατηγοριών, αναζήτηση προθέματος, ανενεργά, 25/50/100 εγγραφές ανά σελίδα, responsive πίνακες, loading/empty/error states και keyboard shortcuts. Το κοινό `CatalogReadService` κάνει εξουσιοδότηση ανά Tenant πριν από τη SQL· το νέο προαιρετικό `Kind` φιλτράρεται στον SQL Server πριν από το paging και εμφανίζεται το όνομα της κατηγορίας με tenant-safe join. **Χωρίς** νέα migration, νέο πακέτο, δημόσιο Catalog API, CRUD, τιμές/ΦΠΑ, φορολογική έγκριση ή διαβιβάσεις. Η οθόνη παραμένει Development-only.

## Βήμα 9.4B.2 — Back Office UI/UX Refinement

Το [εγχειρίδιο 9.4B.2](docs/36-backoffice-ui-ux-refinement.md) περιγράφει τη νέα συμπαγή εμφάνιση Dashboard/Καταλόγου, τον κοινό ασφαλή workspace indicator στο topbar, το προσβάσιμο focus, το sticky sidebar/topbar, τη σύμπτυξη μελλοντικών λειτουργιών και τη βελτίωση των κενών καταλόγων. Τα ειδικά CSS χωρίστηκαν στο hashed `aenaon-backoffice.css`. **Δεν προστέθηκαν** migrations, νέα NuGet packages, SQL writes, λειτουργίες CRUD ή φορολογικές αλλαγές.

## Βήμα 9.4C — Catalog Management (Owner-only)

Το [εγχειρίδιο διαχείρισης καταλόγου](docs/37-catalog-management.md) καλύπτει την προστατευμένη δημιουργία, επεξεργασία, ενεργοποίηση και απενεργοποίηση κατηγοριών, προϊόντων και υπηρεσιών από το **Blazor Back Office** και το κοινό **CLI**. Η εγγραφή απαιτεί **TenantPermission.Manage (Owner)**, HTTPS/CSRF και tenant-scoped SQL Server. Τα είδη διατηρούν αμετάβλητο Tenant/Kind, οι υπηρεσίες δεν παρακολουθούν απόθεμα και καμία φυσική διαγραφή δεν προσφέρεται. **Δεν υλοποιούνται** τιμές, ΦΠΑ, myDATA, IMPACT, εκδόσεις παραστατικών ή αποθέματα. Δεν υπάρχει νέα migration.

## Βήμα 9.4C.1 — Αύξοντες κωδικοί και υποκατηγορίες

Ο [οδηγός 9.4C.1](docs/38-catalog-hierarchy-auto-numbering.md) τεκμηριώνει προαιρετικούς **αυτόματους κωδικούς 1,2,3… χωρίς αρχικά μηδενικά**, χειροκίνητους κωδικούς, πραγματική tenant-safe **ιεραρχία κατηγοριών**, server-side αναζήτηση και «Αποθήκευση και νέο». Η 6η additive migration προσθέτει nullable `ParentCategoryId`, durable SQL counters και idempotency receipts, χωρίς μεταβολή των ήδη καταχωρισμένων ειδών/κωδικών. Απαιτείται έλεγχος `--migrate-database --dry-run`, backup και ξεχωριστή ρητή εφαρμογή στη Development βάση. Παραμένουν **Development-only** όλες οι φόρμες/λειτουργίες και δεν ενεργοποιούνται τιμές, ΦΠΑ, myDATA, IMPACT ή παραστατικά.

## Βήμα 9.4C.2 — Back Office UI Polish & Category Picker

Το [ελληνικό εγχειρίδιο του 9.4C.2](docs/39-backoffice-category-picker-polish.md) περιλαμβάνει το ενιαίο προσβάσιμο category picker (WAI-ARIA combobox με server-side αναζήτηση, ασφαλές keyboard navigation και native fallback), σωστές ελληνικές ενέργειες «Νέο προϊόν / Νέα υπηρεσία / Νέα κατηγορία» και βελτιωμένα light/dark και responsive styles. Το γρήγορο «Δημιουργία νέας κατηγορίας» διατηρεί τα στοιχεία του editor και το εγκεκριμένο πλαίσιο επιχείρησης/καταστήματος. Τα JS/CSS assets χρησιμοποιούν SHA-256 fingerprinted URLs για ανανέωση χωρίς cache προβλήματα.

Η έκδοση παραμένει **Development-only**, με Owner/Manage, HTTPS, anti-CSRF και tenant-scoped SQL. **Δεν** υπάρχουν νέες migrations, αλλαγές σε τιμές/ΦΠΑ, myDATA/IMPACT ή έκδοση παραστατικών.

## Βήμα 9.4C.3 — Category Picker Stability & UI Finalization

Το [ελληνικό εγχειρίδιο 9.4C.3](docs/40-category-picker-stability.md) περιγράφει τη διόρθωση του αυτόματου outline στους τίτλους, τη γεωμετρία του ενιαίου Category Picker μέσα στο viewport (άνω/κάτω με εσωτερικό scroll), το πάντοτε ορατό κουμπί **Νέα κατηγορία** και την αξιόπιστη επιστροφή στην αρχική φόρμα. Οι λειτουργίες περιλαμβάνουν ένα debounce ανά αναζήτηση, `AbortController`, πληκτρολόγιο και βελτιωμένο contrast, καθώς και ενιαία ελληνικά breadcrumbs.

Το CI ελέγχει και **πραγματικό Chrome smoke σε desktop/mobile μέσω CDP χωρίς npm εξαρτήσεις** (`scripts/Test-CategoryPickerBrowser.mjs`). Όλα παραμένουν **Development-only**, με Owner/Manage, HTTPS και anti-CSRF. **Δεν απαιτείται νέα migration** και δεν αλλάζουν myDATA/IMPACT ή παραστατικά.

## Βήμα 9.4C.4 — Server-side Catalog Sorting

Το [ελληνικό εγχειρίδιο 9.4C.4](docs/41-catalog-sorting.md) περιγράφει τη νέα δυνατότητα **ταξινόμησης προϊόντων, υπηρεσιών και κατηγοριών** πατώντας στις επικεφαλίδες του πίνακα. Επιλογές: κωδικός, περιγραφή, κατηγορία, τύπος, παρακολούθηση αποθέματος και κατάσταση. Δεύτερο πάτημα αντιστρέφει την κατεύθυνση. Η SQL ταξινομεί ολόκληρο το αποτέλεσμα **πριν από τη σελιδοποίηση**, διατηρώντας φίλτρα, Owner/Member read permissions, Tenant/Store και σταθερό tie-breaker.

Ακολουθείται φυσική σειρά κωδικών χωρίς αρχικά μηδενικά (1, 2, 10), χωρίς δυναμικά SQL strings ή migrations. Δεν αλλάζουν δεδομένα, myDATA/IMPACT ή λειτουργίες έκδοσης παραστατικών.

## Βήμα 9.5A — Μονάδες μέτρησης & λογιστικοί τύποι

Το [ελληνικό εγχειρίδιο 9.5A](docs/42-catalog-commercial-profile.md) περιγράφει τη νέα εμπορική καρτέλα καταλόγου με **μονάδα μέτρησης** και **λογιστικό τύπο** για προϊόντα/υπηρεσίες. Τα πεδία είναι διαθέσιμα στη δημιουργία, επεξεργασία, CLI και στις λίστες με SQL-side sorting. Η 7η migration `20261113100000_CatalogCommercialProfile` είναι **προσθετική**, διατηρεί τους παλιούς κωδικούς και κάνει backfill για τα ήδη καταχωρισμένα προϊόντα/υπηρεσίες.

**Δεν** υπάρχουν ακόμη τιμές/τιμοκατάλογοι, φορολογικοί χαρακτηρισμοί, myDATA/IMPACT ή έκδοση παραστατικών. Η migration απαιτεί ξεχωριστό preview, backup και ρητή επιβεβαίωση πριν εφαρμοστεί στη βάση.

## Βήμα 9.5A.1 — Ενοποιημένοι εμπορικοί επιλογείς

Οι επιλογείς μονάδας μέτρησης και λογιστικού τύπου έγιναν αναζητήσιμοι, προσβάσιμοι και κοινοί στις φόρμες. [Manual 43](docs/43-unified-commercial-dropdowns.md).

## Βήμα 9.5A.2 — Back Office layout

Βελτιώθηκαν η πυκνότητα πληροφοριών, το responsive UI και η σύμπτυξη sidebar. [Manual 44](docs/44-back-office-layout-refinement.md).

## Βήμα 9.5B.1 — Θεμελίωση τιμοκαταλόγων

Η **8η EF Core migration** `20261113150000_PriceListsFoundation` εισάγει tenant-safe πίνακες `PriceLists` και `PriceListItems`, με περιοχές ισχύος, κατάστημα/κανάλι και EUR. [Manual 45](docs/45-price-lists-foundation.md).

## Βήμα 9.5B.2 — Διαχείριση τιμοκαταλόγων

Διαχείριση καταλόγων/τιμών με Owner/Manage, Blazor UI και CLI, χωρίς νέα migration σε αυτό το βήμα. [Manual 46](docs/46-price-list-management.md).

## Βήμα 9.5B.3A — Read-only Price Resolution (PR #48 merged)

Η `PriceResolutionService` επιλέγει τιμή ανά κατάστημα, κανάλι και ημερομηνία, με σταθερή προτεραιότητα και `Ambiguous` σε ισοβαθμίες. Χωρίς writes, ΦΠΑ ή παραστατικό. [Manual 49](docs/49-price-resolution-foundation.md).

## Βήμα 9.5B.3B — Blazor Price Preview (PR #49 merged)

Η οθόνη `/price-lists/preview` διαβάζει ενεργά είδη και εξηγεί εμπορική τιμή, scope, κανάλι και προτεραιότητα. **Οπτική αποδοχή Light/Dark, mobile και πληκτρολογίου παραμένει εκκρεμής.** [Manual 50](docs/50-price-preview-blazor.md).

## Βήμα 9.5C.1 — Catalog & Pricing Gap Analysis

Μετά το PR #50 ολοκληρώθηκε έλεγχος των υφιστάμενων Domain, SQL/EF Core, Application, Blazor, CLI και tests για προϊόντα, υπηρεσίες και τιμοκαταλόγους. Το [Manual 51](docs/51-catalog-pricing-gap-analysis.md) ξεχωρίζει υπάρχοντα πεδία και ανοιχτά θέματα (barcode/EAN, αναλυτική περιγραφή, ιστορικό τιμών, προληπτική ανίχνευση επικαλύψεων, customer-group scope και μελλοντικό immutable snapshot). **Documentation-only**, χωρίς νέα migration ή λειτουργία.

Η προτεραιότητα είναι **9.5C.2–9.5C.5**, δηλαδή ολοκλήρωση του εμπορικού υποσυστήματος **πριν** από το 9.6A. Excel και γενικές μαζικές εργασίες παραμένουν αναβλημένα.

## Βήμα 9.5C.2 — Catalog Core Completeness

Το [Manual 52](docs/52-catalog-item-details.md) περιγράφει την προσθήκη **προαιρετικού barcode (GTIN/EAN ή εσωτερικού)** και **αναλυτικής περιγραφής** σε προϊόντα και υπηρεσίες. Υπάρχουν κοινοί κανόνες Domain/Application, tenant-scoped unique filtered SQL index, server-side αναζήτηση/sorting, Blazor και CLI με explicit clear flags και διατήρηση δεδομένων παλαιών callers. Η **9η additive EF Core migration** `20261114151000_CatalogItemDetails` προσθέτει δύο nullable στήλες. Απαιτείται ανεξάρτητο dry-run, έγκυρο backup και ρητό apply στην τοπική Development βάση· η κατάσταση εφαρμόσεώς της δεν τεκμαίρεται.

Η αλλαγή δεν ενεργοποιεί ΦΠΑ, provider payload, POS ή έκδοση παραστατικών. **Επόμενο βήμα 9.5C.3 — Pricing Integrity & History**. Excel και γενικά bulk tasks παραμένουν αναβλημένα.

## Μελλοντικά θέματα

Τα [περιβαλλοντικά τέλη](docs/47-environmental-fees-fiscal-design.md) παραμένουν design-only. Η [εισαγωγή Excel και οι μαζικές εργασίες](docs/48-bulk-actions-and-excel-import-plan.md) έχουν αναβληθεί κατόπιν απόφασης έως την ωρίμανση του μοντέλου.

## Βήμα 9.5C.3 — Pricing Integrity & History

Το [Manual 53](docs/53-pricing-integrity-history.md) παρουσιάζει το νέο **append-only audit** αλλαγών τιμοκαταλόγων/τιμών (actor, ώρα UTC, πριν/μετά, Web/CLI/System), τους transactional ελέγχους επικάλυψης ενεργών ισόβαθμων τιμών και τη διατήρηση των υπαρχόντων optimistic checks και της read-time πολιτικής `Ambiguous`. Υλοποιούνται προβολή ιστορικού Blazor και `--manage-price-lists --action history --dry-run`. Η **10η additive migration** `20261114163000_PriceListAuditHistory` απαιτεί έγκυρο backup και ρητή τοπική εφαρμογή **μόνο μετά το merge**. Δεν γίνεται εκ των υστέρων κατασκευή παλαιότερων συμβάντων.

## Βελτίωση — Φυσική και πολυκριτηριακή ταξινόμηση ειδών τιμοκαταλόγου

Το [Manual 54](docs/54-price-list-item-sorting.md) περιγράφει τις προσβάσιμες, αμφίδρομες επικεφαλίδες **Κωδικός / Περιγραφή / Είδος / Τιμή μονάδας / Διαχείριση** στη σελίδα επεξεργασίας τιμοκαταλόγου, φυσική αριθμητική σειρά (`1,2,…,10`) και SQL sorting πριν από την σελιδοποίηση. Η επιλογή διατηρείται σε αναζήτηση, σελίδες, αποθήκευση και αφαίρεση τιμής, και υποστηρίζεται από κοινό service και CLI (`--action items --sort ... [--descending]`). **Καμία νέα migration** και καμία αλλαγή στις τιμές, την επίλυση ή τις δημοσιονομικές/φορολογικές ροές.

## Τρέχον στάδιο — 14/11/2026, υλοποίηση 9.5C.3

**Το PR #53 (9.5C.3 — Pricing Integrity & History) έγινε merge στο `main`**, με επιτυχές Linux/Windows CI και απομονωμένο SQL Server 2022. Η τοπική εγκατάσταση της 10ης migration και τα λειτουργικά tests του ιστορικού παραμένουν εκκρεμή. Η read-only προεπισκόπηση τιμής παραμένει χωριστή λειτουργία από την επεξεργασία τιμοκαταλόγων. Το AENAON ERP υποστηρίζει σε **Development** HTTPS Login/Dashboard, tenant-scoped κατηγορίες/προϊόντα/υπηρεσίες, CRUD και sorting καταλόγου, εμπορικά πεδία, διαχείριση πολλαπλών τιμοκαταλόγων και read-only επίλυση/προεπισκόπηση τιμής.

- **Σχήμα:** Στο `main` υπάρχουν **10 EF Core migrations**, τελευταία `20261114163000_PriceListAuditHistory`. Ο χειριστής επιβεβαίωσε `AenaonERP_Dev`: **9 εφαρμοσμένες / 1 εκκρεμή** μέσω `--migrate-database --dry-run`, χωρίς αλλαγές SQL. Εξακολουθούν να απαιτούνται SQL review, επιβεβαιωμένο backup και ρητό apply μόνο μετά από ξεχωριστή έγκριση. Η βελτίωση sorting δεν προσθέτει migration.
- **Δικαιώματα:** Owner/Manage για μεταβολές, Owner/Member με Read για προβολή, πάντα με Tenant/Store έλεγχο.
- **Όχι ακόμη:** production POS, Android APK, offline συγχρονισμός, τιμολόγηση παραγγελιών, έκδοση/αποστολή παραστατικών, εγκεκριμένοι φορολογικοί κανόνες myDATA/IMPACT ή immutable price snapshots.
- **Επόμενο προτεινόμενο βήμα:** ολοκλήρωση review, CI και SQL/οπτικής αποδοχής του 9.5C.3, έπειτα 9.5C.4–9.5C.5. Το **9.6A — Μενού εστίασης** έπεται της ολοκλήρωσης/αποδοχής του catalog & pricing. Excel / bulk παραμένουν αναβλημένα.

Η πιο πρόσφατη σύνοψη και η διάκριση ιστορικών manuals: [Κατάσταση και χάρτης τεκμηρίωσης](docs/00-current-state-and-roadmap.md).

| Διαδρομή | Σκοπός |
| --- | --- |
| `GET /health` | Διαθεσιμότητα API, όχι πλήρης έλεγχος SQL ή παρόχου |
| `GET /api/info` | Στοιχεία εφαρμογής |
| `GET /health/database` | Development SQL connectivity, όχι πιστοποίηση migrations |
| `/catalog`, `/catalog/manage`, `/price-lists`, `/price-lists/preview` | Προστατευμένο Back Office σε Development |

## Γρήγορη εκκίνηση σε Windows / PowerShell

Προϋποθέσεις: Git και .NET SDK **10.0.404** ή νεότερο συμβατό stable patch της σειράς `10.0.4xx` (σύμφωνα με το `global.json`).

```powershell
git clone https://github.com/MoonHard13/AENAON-ERP.git
cd AENAON-ERP
dotnet dev-certs https --trust
dotnet restore AenaonERP.slnx --locked-mode
dotnet run --project src/Aenaon.Api --launch-profile https
```

Άνοιξε το [https://localhost:7140/health](https://localhost:7140/health). Αναμενόμενη απάντηση: `Healthy`. Η διακοπή του API γίνεται με `Ctrl+C` στο τερματικό.

## Έλεγχοι

Από τον κεντρικό φάκελο του repository:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet list AenaonERP.slnx package --outdated
dotnet list AenaonERP.slnx package --vulnerable --include-transitive
```

Οι ίδιες εντολές build και test εκτελούνται από το GitHub Actions σε Windows και Linux. Η επιτυχία κάθε εκτέλεσης φαίνεται στην καρτέλα **Actions**.

Το CI ελέγχει επιπλέον πραγματικό SQL Server 2022 σε απομονωμένο container Linux και τη ρύθμιση User Secrets μέσω του .NET SDK σε Windows και Linux. Δεν χρησιμοποιεί credentials ή βάσεις χρηστών.

## Ρύθμιση SQL σε περιβάλλον ανάπτυξης

Με υπάρχουσα ξεχωριστή βάση `AenaonERP_Dev`, από PowerShell στον κεντρικό φάκελο:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --configure-database --server 'localhost\SQL2022' --trust-server-certificate
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-database
```

Η πρώτη εντολή ζητά το SQL login και τον κωδικό τοπικά. Ο κωδικός δεν περιλαμβάνεται στη γραμμή εντολών. Το `--trust-server-certificate` επιτρέπεται από την εφαρμογή μόνο σε Development και διατηρεί την κρυπτογράφηση ενεργή.

Πλήρης οδηγός: [Σύνδεση SQL Server](docs/03-sql-server-connection.md).

## Πίνακες επιχειρήσεων και καταστημάτων

Πρώτα έλεγξε τον στόχο και την εκκρεμή migration:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Μετά την επιθεώρηση του SQL και την επιβεβαίωση της βάσης:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
```

Η εφαρμογή προσθέτει `dbo.Tenants`, `dbo.Stores` και το ιστορικό migrations, χωρίς δοκιμαστικά δεδομένα. Το CLI επιτρέπει τη λειτουργία μόνο στο Development και ελέγχει τη βάση πριν από την αρχική migration. Η εκκίνηση του API δεν εφαρμόζει migrations αυτόματα.

Πλήρης οδηγός και ανάκτηση: [Πίνακες επιχειρήσεων και καταστημάτων](docs/04-tenancy-database.md).

## Αρχική δοκιμαστική επιχείρηση

Σε ενημερωμένη βάση χωρίς επιχειρήσεις/καταστήματα:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-business --dry-run --tenant-name "Aenaon Demo" --store-code "MAIN" --store-name "Κεντρικό"
```

Μετά την επιθεώρηση της προεπισκόπησης, αντικατάστησε το `--dry-run` με `--apply --confirm-database AenaonERP_Dev`. Η εντολή καταχωρεί την επιχείρηση και το κατάστημα μαζί και απορρίπτει την επανάληψη όταν υπάρχουν ήδη δεδομένα.

Πλήρης οδηγός, έλεγχοι SQL και ανάκτηση: [Αρχική επιχείρηση](docs/05-initial-business.md).

## Σχήμα χρηστών και συμμετοχών

Με την πρώτη migration ήδη εφαρμοσμένη, έλεγξε τη νέα αναβάθμιση:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Αναμένονται μία εφαρμοσμένη και μία εκκρεμής migration. Η εφαρμογή γίνεται με τη γνωστή εντολή `--migrate-database --apply --confirm-database AenaonERP_Dev`, μετά την επιθεώρηση και τον έλεγχο backup. Η νέα migration προσθέτει πέντε πίνακες, χωρίς λογαριασμούς ή αλλαγή των business IDs.

Πλήρης οδηγός: [Χρήστες και συμμετοχές](docs/06-users-and-memberships.md).

## Πρώτος Administrator (Βήμα 7)

Με μηδέν χρήστες και memberships, στην υπάρχουσα βάση Development:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Αντικατάστησε το email με πραγματική διεύθυνση ανάπτυξης. Έπειτα, μόνο μετά από επιτυχή προεπισκόπηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --initialize-admin --apply --confirm-database AenaonERP_Dev --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --username admin --email owner@your-company.example
~~~

Ο κωδικός εισάγεται **δύο φορές στο διαδραστικό terminal**, ποτέ ως παράμετρος. Απαιτούνται τουλάχιστον 14 χαρακτήρες και συνδυασμός πεζών/κεφαλαίων/αριθμού/συμβόλου. Το CLI αρνείται επανάληψη, production χρήση ή λάθος επιχείρηση/βάση. Δεν δημιουργείται νέα migration.

Πλήρης οδηγός: [Πρώτος διαχειριστής](docs/07-first-administrator.md).

## Authentication Services (Βήμα 8.1)

Για διαδραστική επαλήθευση του πρώτου Administrator (χωρίς login/session):

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --verify-credentials --username admin --confirm-database AenaonERP_Dev
~~~

Για ελεγχόμενη διόρθωση του αρχικού email **your-email@example.com**, πρώτα προεπισκόπηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --fix-admin-email --dry-run --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --user-id 76d2c37f-2e26-4311-a129-e6073057c8dd --new-email new-address@your-domain.example
~~~

Μετά από έλεγχο και backup, αντικατάστησε το --dry-run με **--apply --confirm-database AenaonERP_Dev**. Ο υπάρχων κωδικός ζητείται διαδραστικά, όχι από ορίσματα. Η αλλαγή είναι εφάπαξ και δεν επιβεβαιώνει αυτόματα το νέο email.

Πλήρης οδηγός: [Authentication Services και διόρθωση email](docs/08-authentication-services.md).

## Tenant Authorization (Βήμα 8.2)

Πρώτα γίνεται πραγματική επαλήθευση κωδικού, έπειτα έλεγχος δικαιώματος Read για την ήδη δημιουργημένη επιχείρηση:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --permission Read --confirm-database AenaonERP_Dev
~~~

Για έλεγχο δικαιώματος Manage στο κεντρικό κατάστημα MAIN:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --check-tenant-access --username admin --tenant-id 9698df79-3c31-41c9-b292-b7240c7265fb --store-id c697b9c3-e221-4156-b533-f54dee27d979 --permission Manage --confirm-database AenaonERP_Dev
~~~

Το CLI επιτρέπεται μόνο σε Development, ζητά τον κωδικό διαδραστικά και δεν δημιουργεί session ή αλλαγή δεδομένων επιχειρήσεων. Τα memberships αφορούν προς το παρόν **ολόκληρη την επιχείρηση**, όχι διαφορετικά δικαιώματα ανά κατάστημα.

Πλήρης οδηγός: [Tenant Authorization](docs/09-tenant-authorization.md).

## HTTPS Login και Logout (Βήμα 8.3)

Σε Development, ξεκίνα το API με το υπάρχον HTTPS launch profile:

~~~powershell
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Είναι διαθέσιμα μόνο στο Development τα GET /api/auth/csrf, POST /api/auth/login, GET /api/auth/me, GET /api/auth/access και POST /api/auth/logout. Οι ενέργειες POST απαιτούν CSRF token στο header X-Aenaon-CSRF. Το session cookie είναι Secure, HttpOnly, SameSite=Strict, έχει διάρκεια 30 λεπτά και επανελέγχεται απέναντι σε Identity security stamp και membership.

Η ενσωματωμένη πολιτική προστασίας επιτρέπει έως 10 αιτήματα auth ανά λεπτό ανά IP, επιπλέον του Identity lockout. Δεν έχουν προστεθεί business endpoints, νέα migration ή public production login.

**Πλήρης οδηγός και ασφαλές PowerShell παράδειγμα:** [Secure Login Sessions](docs/10-secure-login-sessions.md).

## Blazor Login UI / Dashboard (Βήμα 8.4)

Το Blazor Static SSR UI εκτελείται από το ίδιο HTTPS API, χωρίς ξεχωριστό host ή τρίτες βιβλιοθήκες.

~~~powershell
dotnet dev-certs https --trust
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Άνοιξε τη διεύθυνση **https://localhost:7140/login** και συνδέσου με τον υπάρχοντα λογαριασμό admin. Μετά το login μεταφέρεσαι στο **/dashboard**, όπου προβάλλονται μόνο επιχειρήσεις στις οποίες ανήκεις και καταστήματα της επιλεγμένης επιχείρησης.

**Λογότυπα:** Τα επίσημα WebP (dark/light) και PNG favicon βρίσκονται ήδη στο `src/Aenaon.Api/wwwroot/images/` του `main`· δεν απαιτείται συμπληρωματικό ZIP.

Πλήρης οδηγός: [Blazor Login UI και Dashboard](docs/11-blazor-login-ui.md).

## UI/UX Hardening (Βήμα 8.4.5)

Το Blazor Dashboard προβάλλει το ενεργό πλαίσιο επιχείρησης/καταστήματος μετά από server-side έλεγχο συμμετοχής. Ο χρήστης αλλάζει κατάστημα μέσω του πίνακα καταστημάτων. Μη επιτρεπόμενο TenantId ή StoreId δεν προκαλεί σιωπηλή επιλογή διαφορετικού πόρου.

Στη νέα εμφάνιση διορθώνονται το Chrome autofill, η αντίθεση Light Theme, η πυκνότητα καρτών και τα κοινά SVG icons χωρίς third-party dependencies. Το Login εμφανίζει φιλικό μήνυμα rate limit. Το θέμα αποκαθίσταται πριν από τη φόρτωση CSS.

Οδηγός: [UI/UX Hardening](docs/12-ui-ux-hardening.md).

## Διόρθωση SVG & Browser Cache (Βήμα 8.4.6)

Τα SVG icons έχουν πλέον ρητές εγγενείς διαστάσεις 20×20, ακόμη κι αν το CSS του browser είναι παλιό. Τα CSS, JavaScript και favicon URLs αποκτούν fingerprint SHA-256 από το πραγματικό περιεχόμενο, ώστε μετά από update να φορτώνεται η σωστή έκδοση των static assets.

Μετά το merge, σταμάτησε το ενεργό API, ενημέρωσε το main και εκκίνησε ξανά το HTTPS Development API. Επανέλεγξε το Login και Dashboard σε Light/Dark· δεν χρειάζεται νέα migration.

Οδηγός: [SVG Icons & Static Asset Cache](docs/13-static-assets-svg-cache.md).

## Βήμα 8.4.7 — UI Refinement & Workspace Persistence

Το Light Mode χρησιμοποιεί και στις δύο πλευρές του Login ανοιχτά χρώματα και την επίσημη έκδοση logo για φωτεινό φόντο· το Dark Mode διατηρεί την αντίστοιχη σκοτεινή οπτική ταυτότητα.

Το Logout βρίσκεται στο User Menu επάνω δεξιά. Η πλοήγηση λειτουργεί και σε κινητά, με πλήκτρο «Μενού».

Η επιλογή Tenant/Store πραγματοποιείται με CSRF-protected POST και αποθηκεύεται σε κρυπτογραφημένο HttpOnly/Secure cookie δεμένο με τον χρήστη. Επιστροφή στο Dashboard και ανανέωση σελίδας διατηρούν την επιλογή, αλλά **δεν παρέχουν πρόσθετα δικαιώματα**. Το Login/Logout διαγράφει την προηγούμενη προτίμηση και κάθε ERP business operation πρέπει να κάνει δική του authorization.

Πλήρης ελληνικός οδηγός: [UI Refinement & Workspace Persistence](docs/14-ui-refinement-workspace-persistence.md).

## Βήμα 8.4.8 — Visual Consistency Polish

Τα δύο WebP λογότυπα αντικαθίστανται από ακριβώς ευθυγραμμισμένες εκδόσεις ίδιου καμβά 960×960, προερχόμενες από τα αυθεντικά MoonHard PNG. Διορθώνεται η έντονη σκιά του User Menu σε Light Mode και η αναγνωσιμότητα μικρών ετικετών, χωρίς λειτουργικές αλλαγές.

**Ιστορική σημείωση:** Τα απαραίτητα binary assets ενσωματώθηκαν στο `main` και οι διαστάσεις τους ελέγχονται από τα σχετικά HTTP tests. Δεν χρειάζεται ξεχωριστό ZIP.

Οδηγίες: [Visual Consistency Polish](docs/15-visual-consistency-polish.md).

## Βήμα 9.1 — Product Catalog Domain Foundation

Προστέθηκαν τα Domain μοντέλα `ProductCatalog`, `CatalogCategory`, `CatalogItem`, `CatalogItemKind` στο `src/Aenaon.Domain/Catalog/`, με αυστηρή απομόνωση ανά Tenant, κωδικούς μοναδικούς ανά επιχείρηση, διαχείριση ενεργής κατάστασης και διακριτό stock tracking (απαγορεύεται για υπηρεσίες).

**Ιστορική κατάσταση του 9.1:** Τότε λειτουργούσε μόνο στη μνήμη. Η SQL αποθήκευση προστέθηκε στο 9.2 και το διαχειριστικό UI/CLI στο 9.4C. Δεν αποτελεί τρέχον περιορισμό.

Εκτέλεση tests: `dotnet test tests/Aenaon.Domain.Tests --configuration Release --filter "FullyQualifiedName~ProductCatalogTests"`.

Αναλυτικές οδηγίες: [Product Catalog Domain Foundation](docs/16-product-catalog-domain.md).

## Βήμα 9.2 — SQL Server Catalog Persistence

- Τρίτη migration: `20261110123500_ProductCatalogPersistence`.
- Νέοι πίνακες: `dbo.CatalogCategories`, `dbo.CatalogItems`.
- Μοναδικοί κωδικοί ανά Tenant, composite category foreign key, υπηρεσίες χωρίς stock.
- Μη καταστροφική επέκταση schema: η εφαρμογή δεν εφαρμόζει migration αυτόματα.
- **Ιστορική κατάσταση του 9.2:** Δεν υπήρχε ακόμη CRUD, το οποίο προστέθηκε αργότερα στο 9.4C (Development-only).

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Η εφαρμογή της migration επιτρέπεται **μόνο αφού** δημιουργηθεί αντίγραφο ασφαλείας και επιθεωρηθεί το SQL script, σύμφωνα με το [SQL Catalog Manual](docs/17-product-catalog-sql.md).

## Δομή

| Διαδρομή | Περιεχόμενο |
| --- | --- |
| `src/Aenaon.Api` | API, controllers, συμβόλαια και κοινή υποδομή HTTP. |
| `src/Aenaon.Application` | Κοινές υπηρεσίες εφαρμογής και συμβόλαια αποθήκευσης. |
| `src/Aenaon.Domain` | Μοντέλα και επιχειρησιακοί κανόνες, χωρίς εξάρτηση από web, βάση ή επιπλέον packages. |
| `src/Aenaon.Infrastructure` | SQL probe, EF Core context/mapping και migrations με περιορισμένο logging. |
| `tests/Aenaon.Api.Tests` | Integration tests του HTTP pipeline με απομονωμένο test server. |
| `tests/Aenaon.Application.Tests` | Έλεγχοι επικύρωσης και συντονισμού πριν από πρόσβαση στη βάση. |
| `tests/Aenaon.Domain.Tests` | Unit tests ιδιοκτησίας, μοναδικότητας κωδικών και επικύρωσης πεδίων. |
| `tests/Aenaon.Infrastructure.Tests` | Έλεγχοι TLS, CLI προστασιών, model/snapshot/SQL και πραγματικής αποθήκευσης σε CI SQL. |
| `docs/01-getting-started.md` | Αναλυτικές οδηγίες εγκατάστασης, CLI, logs και αντιμετώπισης προβλημάτων. |
| `docs/02-tenants-and-stores.md` | Οδηγός του δεύτερου βήματος και παραδείγματα των νέων κανόνων. |
| `docs/03-sql-server-connection.md` | Ρύθμιση User Secrets και διάγνωση πραγματικής σύνδεσης. |
| `docs/04-tenancy-database.md` | Προεπισκόπηση/εφαρμογή migrations, έλεγχοι SSMS και ανάκτηση. |
| `database/001-initial-tenancy.sql` | Παραγόμενο SQL της πρώτης migration για επιθεώρηση. |
| `.config/dotnet-tools.json` | Τοπικό, σταθερό εργαλείο `dotnet-ef`. |
| `docs/05-initial-business.md` | Αρχική καταχώρηση, προεπισκόπηση, προστασίες και έλεγχοι SQL. |
| `docs/06-users-and-memberships.md` | Schema Identity/memberships, αναβάθμιση και διατήρηση δεδομένων. |
| `docs/07-first-administrator.md` | Ασφαλής δημιουργία πρώτου διαχειριστή, έλεγχοι και rollback. |
| `docs/08-authentication-services.md` | Έλεγχος κωδικών, lockout και ασφαλής διόρθωση αρχικού email. |
| `docs/09-tenant-authorization.md` | Κανόνες πρόσβασης Owner/Member, isolation και CLI έλεγχος. |
| `docs/10-secure-login-sessions.md` | HTTPS session cookies, CSRF, login/logout και χειροκίνητοι έλεγχοι. |
| `docs/11-blazor-login-ui.md` | Blazor SSR Login, Dashboard, branding και εγκατάσταση εικόνων. |
| `docs/12-ui-ux-hardening.md` | Ενεργό πλαίσιο Tenant/Store, UX διορθώσεις και ασφάλεια. |
| `docs/13-static-assets-svg-cache.md` | Διόρθωση υπερμεγέθων SVG, fingerprints CSS/JS και browser troubleshooting. |
| `docs/14-ui-refinement-workspace-persistence.md` | Full light/dark, User Menu, responsive πλοήγηση και ασφαλές workspace preference. |
| `database/002-identity-memberships.sql` | SQL αναβάθμισης από την πρώτη στη δεύτερη migration. |
| `docs/16-product-catalog-domain.md` | Domain κατηγοριών, ειδών και υπηρεσιών, κανόνες, tests και προετοιμασία SQL. |
| `docs/17-product-catalog-sql.md` | Migration, SQL constraints, έλεγχοι, preview/apply και rollback του καταλόγου. |
| `docs/18-mydata-impact-compatibility.md` | Υποχρεωτικό compliance gate με επίσημες πηγές, fiscal mappings και ελέγχους. |
| `docs/requirements.md` | Απαιτήσεις και αποφάσεις για τα επόμενα βήματα. |
| `global.json` | Σταθερή επιλογή SDK χωρίς preview εκδόσεις. |
| `Directory.Packages.props` | Κεντρικές εκδόσεις NuGet. |

Το Domain διατηρείται χωρίς εξωτερικά packages. Το Infrastructure χρησιμοποιεί τον επίσημο `Microsoft.Data.SqlClient`, EF Core SQL Server, Identity EF store και logging abstractions. Το EF Core Design είναι ιδιωτική εξάρτηση εργαλείων του API. Η ρητή έκδοση `System.Security.Cryptography.Pkcs` αντικαθιστά ευάλωτη μεταβατική έκδοση του driver. Τα MSTest και MVC Testing αφορούν τις δοκιμές.

## Ασφάλεια και όρια του σταδίου

Το API εκθέτει **Development-only** HTTPS sessions και εξουσιοδοτημένη ανάγνωση/επεξεργασία καταλόγου και τιμοκαταλόγων, με Tenant/Store isolation, Owner/Member permissions και CSRF όπου απαιτείται. Δεν έχει ενεργοποιηθεί παραγωγικό POS, τιμολόγηση, έκδοση παραστατικών ή αποστολή σε myDATA/IMPACT.

Τα διαπιστευτήρια ανάπτυξης θα αποθηκεύονται με .NET User Secrets. Στο production θα χρησιμοποιούνται ασφαλείς ρυθμίσεις του παρόχου φιλοξενίας. Μην ανεβάζεις κωδικούς, πραγματικά δεδομένα πελατών ή κλειδιά παρόχου στο repository.

## Τεκμηρίωση

- [Πρώτη εκκίνηση](docs/01-getting-started.md)
- [Επιχειρήσεις και καταστήματα](docs/02-tenants-and-stores.md)
- [Σύνδεση SQL Server](docs/03-sql-server-connection.md)
- [Πίνακες επιχειρήσεων και καταστημάτων](docs/04-tenancy-database.md)
- [Αρχική επιχείρηση και κατάστημα](docs/05-initial-business.md)
- [Χρήστες και συμμετοχές](docs/06-users-and-memberships.md)
- [Πρώτος διαχειριστής](docs/07-first-administrator.md)
- [Authentication Services](docs/08-authentication-services.md)
- [Tenant Authorization](docs/09-tenant-authorization.md)
- [HTTPS Login Sessions](docs/10-secure-login-sessions.md)
- [Blazor Login UI και Dashboard](docs/11-blazor-login-ui.md)
- [UI/UX Hardening](docs/12-ui-ux-hardening.md)
- [SVG Icons & Static Asset Cache](docs/13-static-assets-svg-cache.md)
- [UI Refinement & Workspace Persistence](docs/14-ui-refinement-workspace-persistence.md)
- [Visual Consistency Polish](docs/15-visual-consistency-polish.md)
- [Product Catalog Domain Foundation](docs/16-product-catalog-domain.md)
- [Product Catalog SQL Persistence](docs/17-product-catalog-sql.md)
- [myDATA & IMPACT Compatibility Gate](docs/18-mydata-impact-compatibility.md)
- [Fiscal Catalog Design Gate](docs/19-fiscal-catalog-design.md)
- [Fiscal Domain Contracts & Tests](docs/20-fiscal-domain-contracts.md)
- [Fiscal Reference Data & Tax Mapping](docs/21-fiscal-reference-mapping.md)
- [Offline Fiscal Reference Evidence Inspection](docs/22-fiscal-import-evidence-inspection.md)
- [Fiscal Staging & Independent Source Review](docs/23-fiscal-staging-review.md)
- [Candidate myDATA Classification Matrix Preview](docs/24-classification-matrix-preview.md)
- [Official XLSX Structure Inspection](docs/25-official-xlsx-structure-inspection.md)
- [Complete XLSX Evidence Export](docs/26-complete-xlsx-evidence-export.md)
- [Original XLSX / JSON Evidence Reconciliation](docs/27-xlsx-original-evidence-reconciliation.md)
- [Fiscal Semantic Review Triage](docs/28-fiscal-semantic-review-triage.md)
- [Fiscal Review Worklist CSV](docs/29-fiscal-review-worklist-csv.md)
- [IMPACT Contract Preflight](docs/30-impact-contract-preflight.md)
- [Offline IMPACT Payload Preflight](docs/31-impact-offline-payload-preflight.md)
- [IMPACT Classification Source Reconciliation](docs/32-impact-classification-source-reconciliation.md)
- [IMPACT Conditional Field Evidence](docs/33-impact-conditional-field-evidence.md)
- [Ενοποιημένα dropdowns](docs/43-unified-commercial-dropdowns.md)
- [Back Office layout](docs/44-back-office-layout-refinement.md)
- [Υποδομή τιμοκαταλόγων](docs/45-price-lists-foundation.md)
- [Διαχείριση τιμοκαταλόγων](docs/46-price-list-management.md)
- [Περιβαλλοντικά τέλη — σχεδιασμός](docs/47-environmental-fees-fiscal-design.md)
- [Excel / μαζικές εργασίες — αναβολή](docs/48-bulk-actions-and-excel-import-plan.md)
- [Read-only επίλυση τιμής](docs/49-price-resolution-foundation.md)
- [Blazor προεπισκόπηση τιμής](docs/50-price-preview-blazor.md)
- [Κατάσταση έργου και χάρτης τεκμηρίωσης](docs/00-current-state-and-roadmap.md)
- [Συμφωνημένες απαιτήσεις](docs/requirements.md)

### Σημείωση για GitBook starter περιεχόμενο

Οι φάκελοι `documentation/`, `home/`, `help-center/` και `changelog/` περιέχουν ακόμη **γενικά GitBook templates** («Developer Platform», «Feature description»), όχι εγκεκριμένα manuals AENAON ERP. Οι πραγματικοί τεχνικοί οδηγοί είναι στο `docs/`. Η αλλαγή ή απόσυρση των template pages απαιτεί ξεχωριστή επαλήθευση των ενεργών GitBook spaces και της σύνδεσής τους με αυτό το repository.
