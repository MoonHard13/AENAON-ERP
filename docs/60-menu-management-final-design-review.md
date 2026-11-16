# AENAON ERP — 9.6A.0: Τελικό Design Review και εγκεκριμένες UX αποφάσεις

**Ημερομηνία:** 16/11/2026  
**Source of truth:** GitHub `main` `3af56b94d0758f59386f6437be5330ff681b0b53` (merged PR #60), τεχνικό [Manual 58](58-menu-management-architecture.md) και [Manual 59](59-acceptance-evidence-and-test-protocol.md).  
**Κατάσταση:** **9.6A.0 DESIGN APPROVED — 10/10 αποφάσεις owner**, design-only. **Καμία άδεια υλοποίησης, SQL migration ή merge**.

{% hint style="warning" %}
Η ρητή δήλωση «accept 9.5C.5» αποτελεί owner acceptance με **NOT VERIFIED** πραγματικά Browser/Development SQL/Owner–Member/Mobile/Keyboard/Concurrency gates. Στις 16/11/2026 εγκρίθηκαν τόσο οι έξι UX/functional αποφάσεις όσο και οι τέσσερις πολιτικές DEC-01..DEC-04. Αυτές οι **10/10 σχεδιαστικές εγκρίσεις** δεν εγκρίνουν migration, ανάπτυξη 9.6A.1, δημοσίευση σε GitBook ή merge του PR #61.
{% endhint %}

## 1. Ρητώς εγκεκριμένες αποφάσεις του ιδιοκτήτη

Ο ιδιοκτήτης εγκρίνει χωρίς επιφύλαξη ως βασικές σχεδιαστικές απαιτήσεις τις εξής έξι αποφάσεις, οι οποίες αποτελούν πλέον **δεσμευτικά UX/functional requirements** του 9.6A:

| ID | Απόφαση | Κατάσταση |
| --- | --- | --- |
| UX-01 | Τρεις desktop στήλες — αριστερά Menu Categories, κέντρο Menu Items, δεξιά ERP Catalog | APPROVED |
| FUNC-01 | Πολλά ταυτόχρονα ενεργά μενού ανά επιχείρηση/κατάστημα/κανάλι | APPROVED |
| FUNC-02 | Κοινόχρηστη βιβλιοθήκη Modifier Groups με τοπικές per-menu overrides | APPROVED |
| FLOW-01 | Draft → Preview → Publish με ασφαλή δημοσίευση και ιστορικό | APPROVED |
| PRICING-01 | Κοινή υπάρχουσα ERP Catalog & Pricing source of truth, χωρίς παράλληλη μηχανή τιμών μενού | APPROVED |
| UX-02 | Responsive mobile Menu Builder με καρτέλες αντί στριμωγμένων desktop στηλών | APPROVED |

Η έγκριση αφορά **τον σχεδιασμό** και **δεν σημαίνει** ότι ολόκληρος ο κύκλος έχει ήδη υλοποιηθεί ή επαληθευτεί.

## 2. Αποτέλεσμα τελικού αρχιτεκτονικού ελέγχου

| Gate | Κατάσταση | Τεκμηρίωση/παρατήρηση |
| --- | --- | --- |
| A — Domain/Application boundaries | DESIGN REVIEWED | Διάκριση Tenant MenuDraft/Version/Deployment από Catalog/PriceList· οι επόμενες οντότητες είναι **προτεινόμενες**, όχι υπάρχουσες |
| B — SQL integrity | DESIGN REVIEWED, NOT TESTED | Απαιτούνται scoped composite FKs, tenant-safe unique keys, rowversion, audit, transaction και isolation tests |
| C — UI/Accessibility | CORE APPROVED, NOT TESTED | Τρεις στήλες, mobile tabs, side-sheet editor, μη-Drag-only actions, χωρίς inactive placeholders |
| D — Tests/Delivery | NOT VERIFIED | Το Menu δεν υπάρχει ακόμη· δεν είναι δυνατό E2E πραγματικής λειτουργίας. Σχεδιάστηκαν test IDs MENU-01..14 |
| Prior 9.5C.5 acceptance | OWNER ACCEPTED, TECHNICAL GATES NOT VERIFIED | Owner sign-off έχει καταγραφεί ξεχωριστά. Δεν γίνεται rewrite των ανεπιβεβαίωτων gates ως PASS |
| GitHub docs PR #61 | OPEN, NOT MERGED | Παραμένει documentation-only. Τελευταίο CI ελέγχεται σε αναφορά PR· όχι ισχυρισμός PASS για νέες αλλαγές χωρίς run |
| GitBook | CONTENT DRIFT DETECTED | Η τρέχουσα δημοσιευμένη εισαγωγή/Κατάλογος έχει παλαιότερα PR #50/#51. Καμία live δημοσίευση εδώ |

### Βασικές τεχνικές παρατηρήσεις

1. Η κεντρική τιμή αναζητείται μόνο μέσω της ήδη υπάρχουσας `PriceResolutionService`/`store-channel-v1` και ελέγχεται σε συγκεκριμένο Store + Channel + business date. Τα `Missing`, `Ambiguous`, `ItemUnavailable`, `Denied` δεν παράγουν ούτε ad hoc τιμή ούτε `CommercialPriceSnapshot`.
2. Η υπάρχουσα `CommercialPriceSnapshot` v1 είναι αμετάβλητη **in memory**, όχι durable Order/POS. Δεν επαναχρησιμοποιείται ως εγγυημένη μελλοντική τιμή σε published menu. Οι published MenuVersions είναι immutable **presentation/configuration** snapshots, όχι commercial/tax price snapshots.
3. Οι πραγματικοί ρόλοι στο `main` είναι Owner και Member σε επίπεδο Tenant, χωρίς per-Store grant. Οποιαδήποτε επιλεκτική έκδοση από delegated store publisher απαιτεί ρητή authorization επέκταση.
4. Το τωρινό `Store` δεν περιέχει timezone. Δεν επιτρέπεται σιωπηρή ζώνη ώρας κατά το scheduling.
5. Η υπάρχουσα Blazor υλοποίηση χρησιμοποιεί κυρίως Static SSR + antiforgery POST. Στο πρώτο Builder διατηρούνται τα υπάρχοντα conventions: server-validated writes, μικρό progressive-enhancement JavaScript μόνο όπου χρειάζεται για drag/drop, με πλήρη fallback σε βελάκια και αριθμητική θέση. Δεν απαιτείται νέο interactive framework ή εξάρτηση χωρίς λόγο.
6. Το MenuCategory δεν είναι CatalogCategory· η παρουσίαση διαχωρίζεται από τα ERP master records. Remove από Menu **δεν** διαγράφει CatalogItem. Οι referential constraints δεν αντικαθιστούν τη server-side authorization.

## 3. Εγκεκριμένες πολιτικές DEC-01 έως DEC-04 — owner sign-off 16/11/2026

Ο ιδιοκτήτης δήλωσε ρητά **«Εγκρίνω και τις 4»**, σε απάντηση στις συγκεκριμένες πολιτικές DEC-01..DEC-04 του final design review. Όλες είναι **APPROVED ως απαιτήσεις σχεδιασμού**. Δεν έχουν ακόμη υλοποιηθεί ή ελεγχθεί στην εφαρμογή.

| ID | Θέμα | Εγκεκριμένη πολιτική σχεδιασμού | Επίδραση |
| --- | --- | --- | --- |
| DEC-01 | Δικαιώματα Store/Channel publishing | Για v1 μόνο Tenant Owner κάνει create/edit/publish· Member μόνο read. Ο Owner επιλέγει ρητά και ελέγχεται σε κάθε Store/Channel target. Delegated publisher μετά από χωριστή policy design | Απαραίτητη πριν από δημοσίευση |
| DEC-02 | Αποτυχία τιμολόγησης στο Preview/Publish | Fail-closed: `Missing`/`Ambiguous`/`ItemUnavailable` επισημαίνονται και δεν επιτρέπουν δημοσίευση ενός **ορατού, πωλήσιμου** είδους στο συγκεκριμένο target. Καμία σιωπηλή παράλειψη χωρίς εμφανή preflight report | Απαραίτητη πριν από δημοσίευση |
| DEC-03 | Χρεώσιμα extras, variants και modifiers | Προσωρινά μόνο presentation-only λεκτικές επιλογές. Κάθε **χρεώσιμη** επιλογή/μέγεθος απαιτεί συνδεδεμένο επιτρεπόμενο CatalogItem και επίλυση τιμής από ERP Pricing· να μην υπάρξει ανεξάρτητο ποσό σε μενού. Δεν ενεργοποιείται POS/Tax | Απαραίτητη πριν από χρεώσιμους modifiers |
| DEC-04 | Ωράρια, timezone και scheduled publish | Κάθε Store έχει ρητή έγκυρη time zone, UTC instant για scheduling, έλεγχο DST, απόρριψη αμφίσημων ωρών. Το 9.6A.1 δεν υλοποιεί ωράρια ή scheduler, οπότε δεν προστίθεται τώρα Store migration | Απαραίτητη πριν από 9.6A.2/9.6A.5 |

### Τεχνικά defaults που προτείνονται για 9.6A.1

- **Ένα ανεξάρτητο MenuDraft ανά Menu μέσα στον Tenant**, με στόχευση πολλών Stores μέσω μελλοντικών explicit deployments. Δεν δημιουργούνται Store-specific αντίγραφα κάθε προϊόντος ή per-store overrides στο πρώτο implementation slice.
- **Επίπεδες MenuCategories** στην πρώτη έκδοση. Ανεξάρτητες από CatalogCategories, με σειρά, χρώμα και ορατότητα. Ενδεχόμενο nesting μόνο με νέα requirement και δοκιμές.
- Το ίδιο CatalogItem επιτρέπεται σε *διαφορετικά* μενού. Σε μία MenuCategory προτείνεται μία μόνο ενεργή θέση ανά CatalogItem, για αποτροπή ακούσιων duplicates.
- **Ταυτόχρονα μενού** δεν συγχωνεύονται και δεν υπονοείται menu-priority. Προβάλλονται ξεχωριστά με deterministic DisplayOrder + Id.
- **Selective publish transaction:** ένα αίτημα με πολλαπλά targets προτείνεται all-or-nothing σε επίπεδο καταχώρισης intent, με ρητό αποτέλεσμα ενεργοποίησης για κάθε target. Ο worker επαναελέγχει target/authorization/pricing και σε αποτυχία κρατά την προηγούμενη έγκυρη έκδοση (κανένα ψευδές atomicity across async workers).
- **Undo/Autosave:** το v1 υλοποιεί ρητή αποθήκευση με expected draft revision και σαφές dirty indicator, όχι μη εγκεκριμένο silent autosave/undo. Το `Ctrl+S` και τα Move Up/Down/Position μοιράζονται τον ίδιο Application handler.
- **Ανενεργές δυνατότητες:** μέχρι να υπάρχουν Preview/Publish/Modifiers, τα αντίστοιχα κουμπιά δεν εμφανίζονται ως δήθεν λειτουργικά· δεν υπάρχουν άδειες σελίδες που προσποιούνται ολοκλήρωση.
- **Εικόνες:** απομακρυσμένο upload/storage, moderation και ασφαλές serving αξιολογούνται αργότερα, όχι ως binary column στην πρώτη Menu migration.

## 4. Final wireframe review

### Desktop / large viewport — εγκεκριμένη διάταξη

~~~text
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ AENAON Sidebar │ Menu Builder · Tenant · Store · Channel · Menu · Draft                 │
│                │ [Save Draft] [History όταν υλοποιηθεί] [Preview / Publish αργότερα]    │
├────────────────┬────────────────────────┬─────────────────────────────┬────────────────────┤
│ Sidebar        │ Menu Categories        │ Menu Items                  │ ERP Catalog        │
│                │ + Νέα κατηγορία        │ + Προσθήκη                  │ Search / Filter    │
│                │ 1 Ορεκτικά  ↑ ↓        │ 1 Χωριάτικη  ↑ ↓            │ [+] Χωριάτικη      │
│                │ 2 Κυρίως    ↑ ↓        │ 2 Μπιφτέκι   ↑ ↓            │ [+] Μπιφτέκι       │
│                │ 3 Ποτά      ↑ ↓        │ 3 Νερό       ↑ ↓            │ [+] Νερό           │
│                │ Color / Visibility     │ Στοιχεία / Αφαίρεση        │ Paging / Sorting   │
└────────────────┴────────────────────────┴─────────────────────────────┴────────────────────┘
~~~

Το side sheet ανοίγει για επεξεργασία στοιχείων και δεν αντικαθιστά τις βασικές τρεις στήλες. Παραμένει ορατό το επιλεγμένο Tenant/Store context. Τα ποσοστά στηλών είναι ενδεικτικά και το layout προσαρμόζεται χωρίς clipped controls ή global overflow.

### Mobile / tablet — εγκεκριμένο μοντέλο

~~~text
[Menu: Κυρίως ▾]   [Draft]
[Κατηγορίες] [Είδη] [Κατάλογος]
--------------------------------
Κατηγορία: Ορεκτικά
1 Χωριάτικη        [↑] [↓] [...]
2 Τζατζίκι         [↑] [↓] [...]
--------------------------------
[Αποθήκευση προσχεδίου]
~~~

Οι επιλογές πρέπει να λειτουργούν με touch, keyboard, 200% zoom, Light/Dark, screen reader announcements και focus return από side sheet. Drag/drop είναι **βελτίωση**, όχι ο μοναδικός τρόπος αλλαγής σειράς.

## 5. Ακριβής προτεινόμενη πρώτη υλοποίηση 9.6A.1 — ΔΕΝ ΕΓΚΡΙΘΗΚΕ ΑΚΟΜΗ

Μόνο μια ολοκληρωμένη vertical slice για **Tenant Menu Draft**:
- Domain: `Menu`, `MenuDraft`, `MenuCategoryDraft`, `MenuItemDraft` με invariant, TenantId και position.
- Application: shared `MenuDraftManagementService`/interfaces, owner authorization, expected-revision/duplicate request handling, transactional add/remove/reorder.
- Infrastructure: αποκλειστικά additive draft tables, composite tenant FK προς CatalogItems και draft/category, indexes και optimistic concurrency, σε απομονωμένη βάση μετά από SQL review/backup/ρητή migration έγκριση.
- Blazor: πραγματικός τριστήλος builder σε desktop και mobile tabs, create/edit/reorder/remove μέσω +/- και drag με keyboard fallback, κατανοητά loading/empty/error/conflict/success states.
- CLI: τα **ίδια** commands Create/Update/List/Add/Reorder/Remove/DryRun, `--help`, `--tenant-id`, explicit DB confirmation, cancellation και ασφαλή exit codes.
- QA: domain/Application tests, πραγματικό isolated SQL 2022 και multi-tenant/concurrent tests, SSR/API security, πραγματικό authenticated browser σε 320/375/390/768/1366/1920, Light/Dark, 200% zoom και keyboard navigation.

**Εκτός 9.6A.1:** Publish, scheduling, modifiers, charged extras, price persistence/order snapshots, image uploads, KDS, POS, print agent, offline mode, fiscal issuance/transmission και Excel/bulk.

## 6. Risks, ελεγκτικά μέτρα και επόμενη ενέργεια

| Κίνδυνος | Αντιμετώπιση |
| --- | --- |
| Cross-tenant ή cross-store references | Composite scoped FK + Application authorization + real negative SQL/E2E tests |
| Stale editor/duplicate write | Expected revision + idempotency key + transactional DB constraints + audit |
| Catalog record removed/deactivated | Restrict reference, safe inactive display and publisher preflight. Καμία cascade διαγραφή |
| Price drift/ambiguity μετά από Publish | Re-resolve στο target runtime, fail closed. Versioned menu presentation ≠ frozen price |
| Mutable shared modifiers αλλάζουν published menu | Publish-time copy του resolved graph και immutable version; revision diff στο Draft |
| Late/partial scheduled activation | Idempotent operation record/outbox, explicit target status, ασφαλής προηγούμενη έκδοση |
| XSS, malicious images, oversized input | server validation, output encoding, ασφαλής πολιτική uploads σε μελλοντικό βήμα |
| Fiscal classification/ΦΠΑ extras | Καμία ενεργοποίηση από Menu· προέλεγχος [ΑΑΔΕ myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) και [IMPACT docs](https://developers.s1ecos.com/) πριν από χρεώσιμα features |

Επίσημη επιθεώρηση 16/11/2026: Η σελίδα της ΑΑΔΕ εξακολουθεί να αναφέρει v2.0.2. Το δημόσιο Swagger IMPACT δεν δίνει πάντοτε ευανάγνωστα contract details μέσω browser tools· δεν θεωρείται ότι επαληθεύτηκε κάθε JSON field ή version number. Η τελική φορολογική/IMPACT έγκριση δεν παρέχεται από το παρόν design review.

## 7. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

Χρησιμοποιείται για design sign-off και αρχιτεκτονική ανασκόπηση πριν από την προετοιμασία implementable 9.6A.1 PR. Δεν αποτελεί εγχειρίδιο ήδη υπάρχουσας λειτουργίας, εξουσιοδότηση βάσης, πλήρες acceptance των 9.5C.5 tests, φορολογική έγκριση ή άδεια έκδοσης παραστατικών.

### Checklist τελικής έγκρισης
- [x] Έξι βασικές UX/functional αποφάσεις εγκρίθηκαν από τον ιδιοκτήτη.
- [x] Επιβεβαιώθηκε το GitHub main και το ανοικτό docs-only PR.
- [x] Καταγράφηκαν οι εξαρτήσεις Catalog/Pricing, τα security risks, τα models και UX wireframes.
- [x] **Εγκρίθηκαν ρητά οι πολιτικές DEC-01..DEC-04** (Owner-only, fail-closed pricing, ERP-linked charged modifiers, explicit Store timezone/UTC/DST).
- [ ] Ολοκληρώθηκαν/τεκμηριώθηκαν τα εκκρεμή υποχρεωτικά πραγματικά 9.5C.5 E2E/SQL checks.
- [ ] Δόθηκε ξεχωριστή ρητή έγκριση έναρξης 9.6A.1 και σχετικού SQL migration plan.
- [ ] Αποφασίστηκε χωριστά το merge του docs PR #61.
- [ ] Δημοσιεύτηκε εγκεκριμένη, ενημερωμένη έκδοση GitBook με ανεξάρτητο content review.

**Troubleshooting για σχεδιασμό:** Αν ζητηθεί feature Publish/Modifiers στο 9.6A.1, ελέγξτε το scope και μεταφέρετέ το στο αντίστοιχο επόμενο βήμα· αν ζητηθεί τιμή μενού διαφορετική από ERP PriceList, απορρίψτε το ως μη εγκεκριμένη διπλή τιμολόγηση και ζητήστε νέα εμπορική/φορολογική προδιαγραφή.

**Συμπέρασμα:** Το **9.6A.0 — Menu Management Architecture & UX είναι DESIGN APPROVED (10/10 owner approvals)**. Η υλοποίηση δεν έχει ξεκινήσει και το 9.6A.1 implementation gate **παραμένει κλειστό** μέχρι τα απαραίτητα πραγματικά E2E/SQL τεκμήρια και ξεχωριστή ρητή έγκριση.
