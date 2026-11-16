# AENAON ERP — 9.6A.0: Αρχιτεκτονική και UX Διαχείρισης Μενού

**Ημερομηνία σχεδιασμού:** 16/11/2026  
**Βάση GitHub:** main @ 3af56b94d0758f59386f6437be5330ff681b0b53 (merged PR #60)  
**Κατάσταση:** DESIGN ONLY — καμία υλοποίηση, migration, API, POS ή δημοσίευση μενού.  
**Αποδοχή 9.5C.5:** IMPLEMENTED, NOT FULLY ACCEPTED· το πραγματικό Development browser/SQL acceptance παραμένει NOT VERIFIED. Βλέπε [Manual 57](57-catalog-pricing-final-acceptance.md) και [επανέλεγχο](59-acceptance-evidence-and-test-protocol.md).

{% hint style="warning" %}
Το παρόν είναι προτεινόμενη αρχιτεκτονική προς έγκριση, **όχι περιγραφή διαθέσιμων λειτουργιών**. Απαγορεύονται αλλαγές schema, ενεργοποίηση Menu/POS ή μεταφορά σε production πριν ολοκληρωθεί το 9.5C.5 και δοθεί ρητή έγκριση. Τα φορολογικά δεδομένα παραμένουν εκτός ενεργής λειτουργίας.
{% endhint %}

## Απόφαση owner για το προηγούμενο στάδιο

**Απόφαση ιδιοκτήτη (16/11/2026):** Ο ιδιοκτήτης δήλωσε ρητά **«accept 9.5C.5»**. Καταγράφεται ως **OWNER ACCEPTED — WITH UNVERIFIED E2E/SQL GATES**, όχι ως δήλωση ότι εκτελέστηκαν οι ελλείπουσες δοκιμές. Τα Browser/Development SQL/Owner–Member/Mobile/Keyboard/Zoom/Concurrency gates παραμένουν **NOT VERIFIED**· η πλήρης τεχνική αποδοχή και η υλοποίηση 9.6A.1 εξακολουθούν να απαιτούν τεκμήρια και ξεχωριστή ρητή έγκριση. Καμία εξουσιοδότηση για merge, migration ή Menu implementation δεν δόθηκε.

## Τελικό design review και αποδοχές — 16/11/2026

Ο ιδιοκτήτης **ενέκρινε τις έξι βασικές απαιτήσεις** (τριστήλο desktop builder, πολλαπλά ενεργά μενού, κοινόχρηστα Modifier Groups, Draft → Preview → Publish, ενιαίο ERP Catalog & Pricing, responsive mobile tabs) **και τις τέσσερις πολιτικές DEC-01..DEC-04** (Owner-only permissions, fail-closed prices, ERP-linked χρεώσιμα modifiers, explicit Store timezone/UTC/DST). Αναλυτική τελική απόφαση: [Manual 60 — Final Design Review](60-menu-management-final-design-review.md). **9.6A.0 DESIGN APPROVED (10/10)**, αλλά η έγκριση **δεν** επιτρέπει υλοποίηση, migration ή merge.

## 1. Σκοπός, αρχές και όρια

Το **Μενού Εστίασης** οργανώνει την παρουσίαση και διαθεσιμότητα *υπαρχόντων* ERP CatalogItems για συγκεκριμένα καταστήματα και κανάλια. Δεν είναι δεύτερος κατάλογος ούτε δεύτερος μηχανισμός τιμών. Στόχος: ασφαλής διαχείριση πολλών μενού, κοινόχρηστων modifiers, εκδόσεων και στοχευμένης δημοσίευσης, με μελλοντική συμβατότητα POS/Android/KDS.

**Εντός σχεδιασμού:** πολλαπλά μενού, κατηγορίες/θέσεις/χρώματα, σύνδεση Product/Service, περιγραφές/εικόνες, modifiers/variants, διαθεσιμότητα, ταυτόχρονα ενεργά μενού, Draft/Preview/Publish, ιστορικό, προγραμματισμός, rollback, παραμετρική εξουσιοδότηση και CLI.  
**Εκτός 9.6A.0:** SQL/EF migrations, κώδικας, πραγματικό UI, παραγγελίες, POS, KDS, print/offline, φορολογική έκδοση, Excel/bulk, παραγωγική χρήση, αυθαίρετες τιμές μενού.

## 2. Επιβεβαιωμένες εξαρτήσεις από main

- CatalogItem και CatalogCategory ανήκουν σε Tenant. Τα CatalogItems διαθέτουν Kind Product/Service, IsActive, Barcode, LongDescription, UnitOfMeasure και AccountingType. Υπάρχουν σύνθετα FK Tenant/Category και unique Tenant/Code, Tenant/Barcode.
- Τιμοκατάλογοι έχουν Tenant, προαιρετικό Store, Channel (All, DineIn, Takeaway, Delivery, Retail, Online), ημερομηνίες ισχύος, ενεργή κατάσταση, EUR και PricesIncludeVat.
- Ο PriceResolutionPolicy με έκδοση **store-channel-v1** κατατάσσει Store+Channel > Store+All > Tenant+Channel > Tenant+All. Ισοβαθμία, ακόμη με ίδιες τιμές, είναι Ambiguous· απουσία τιμής είναι Missing.
- Η PriceResolutionService ελέγχει membership/store και ενεργό είδος και παράγει μόνο σε-memory CommercialPriceSnapshot v1 σε επιτυχές Resolved. **Δεν** υπάρχει αποθηκευμένη γραμμή παραγγελίας, φορολογική πολιτική ή POS.
- Οι τωρινοί ρόλοι είναι Owner (Read/Manage σε Tenant) και Member (Read). **Δεν** υφίσταται λεπτομερές store-level membership ή granular άδεια Publish.
- Το Store **δεν διαθέτει timezone**. Το design δεν πρέπει να υποθέτει ότι υπάρχει. Προτείνεται ρητή time-zone πολιτική ανά Store πριν από χρονική ενεργοποίηση.
- Το Blazor χρησιμοποιεί Development-only SSR, κοινά Sidebar/Topbar, protected session, anti-CSRF στα POST, Application services, EF Core, SQL Server και CLI commands.

## 3. Λειτουργική προδιαγραφή

### 3.1 Πολλαπλά μενού, στόχευση και ταυτόχρονη παρουσία

Ένα Tenant έχει πολλά Menu, με μοναδικό Code ανά Tenant, Name, προαιρετικό Subtitle/Description, κατάσταση αρχείου και εμφανίσιμη σειρά. Το ίδιο Menu μπορεί να στοχεύει περισσότερα από ένα Store και Channel, αλλά **κάθε στόχος** επικυρώνεται ως μέλος του ίδιου Tenant και απαιτεί άδεια. Η στόχευση είναι ρητή: StoreId + συγκεκριμένο PriceListChannel, όχι ανεξέλεγκτο "All stores".

Δύο μενού μπορούν να είναι δημοσιευμένα και διαθέσιμα ταυτόχρονα στο ίδιο Store/Channel/ώρα (π.χ. Κυρίως και Ποτά). Δεν υπάρχει αυτόματη υπερίσχυση, συγχώνευση ή αναίρεση του ενός. Προβάλλονται διακριτά, ταξινομημένα με DisplayOrder και σταθερό MenuId. Ένα duplicate CatalogItem σε δύο μενού παραμένει **δύο διακριτές παρουσιάσεις**· δεν δημιουργείται δεύτερο ERP είδος.

### 3.2 Κατηγορίες και σύνδεση ειδών

Το MenuCategory είναι **ανεξάρτητο** από CatalogCategory: Name, OptionalColor, IconKey (whitelist), Position, IsVisible, OptionalImage. Επιτρέπεται nesting μόνο αν αποφασιστεί με explicit max depth· **πρόταση v1: επίπεδη δομή** για καθαρή touch χρήση. Η ανάθεση MenuItem απαιτεί CatalogItemId ίδιου Tenant· προαιρετικά NameOverride, ShortDescriptionOverride, image, Tags, DisplayOrder, Visibility, όρια διαθεσιμότητας. CatalogCode, Kind, μονάδα, λογιστικός τύπος, barcode και φορολογικά στοιχεία **δεν** αντιγράφονται ως ανεξάρτητη πηγή αλήθειας. Αφαίρεση από Menu αποσυνδέει μόνο τη θέση, δεν διαγράφει CatalogItem.

Θέσεις/σειρά: ακέραιες Position ανά γονικό container, επιτρεπτά duplicate positions μόνο μέσα σε transient draft edit και κανονικοποίηση σε σταθερή σειρά κατά το save· αναδιάταξη ατομικά με optimistic concurrency. Drag/drop, βελάκια και πληκτρολόγηση θέσης είναι ισότιμοι τρόποι έκφρασης της ίδιας Application command.

### 3.3 Modifier Groups, επιλογές, extras, variants

Υπάρχει κοινόχρηστη Tenant-scoped βιβλιοθήκη ModifierGroup: Name, SelectionMode (Single/Multiple), MinSelections, MaxSelections, AllowQuantity, DisplayOrder, Active, επιλογές με OptionId, Label, Position, IsDefault, MaxQuantity. Ο κανόνας 0 <= Min <= Max <= πλήθος ενεργών επιλογών ισχύει τόσο στο Domain όσο και στα μελλοντικά SQL constraints όπου εφικτό. Για Single, Max=1. Required σημαίνει Min >= 1. Οι πολλαπλές προεπιλογές δεν επιτρέπεται να υπερβαίνουν Max.

Σύνδεση Group σε MenuItem μέσω MenuItemModifierAssignment με Position και επιτρεπτές **τοπικές αποκλίσεις**: τίτλος, ορατότητα, όρια επιλογών μέσα σε λογικά όρια, υποσύνολο διαθέσιμων επιλογών, σειρά. Η κοινή βιβλιοθήκη παραμένει ανεπηρέαστη· το δημοσιευμένο MenuVersion περιέχει **αμετάβλητο resolved αντίγραφο** του Group και των overrides. Τροποποίηση βιβλιοθήκης δεν αλλοιώνει ήδη δημοσιευμένες εκδόσεις· το draft εμφανίζει ένδειξη νέας library revision και απαιτεί επανέλεγχο.

**Εμπορική/φορολογική ασφάλεια:** χρεώσιμη επιλογή ή variant πρέπει να αντιστοιχεί σε ενεργό CatalogItem (ή σε μελλοντικό εγκεκριμένο contract πώλησης) και η τιμή της επιλύεται από τον κοινό PriceResolutionService. Δεν επιτρέπεται αποθήκευση ad hoc τιμής σε MenuItem/ModifierOverride. Οι απλές λεκτικές οδηγίες (π.χ. "χωρίς πάγο") ορίζονται ως presentation-only, όχι ως υποκρυπτόμενη φορολογική γραμμή ή χρέωση. Variants μεγέθους με διαφορετική τιμή προτείνεται να χρησιμοποιούν ξεχωριστά CatalogItem IDs μέχρι να εγκριθεί ειδικό pricing contract. Οι οικονομικές ροές modifiers **δεν ενεργοποιούνται σε 9.6A.1**.

### 3.4 Διαθεσιμότητα και χρονικές ζώνες

Η ορατότητα είναι τομή των επιπέδων MenuDeployment, MenuCategory, MenuItem, linked CatalogItem και τυχόν ModifierOption. Ορίζονται Tenant, Store, Channel, day-of-week, local time windows και effective dates· δεν επιτρέπεται item να υπερβαίνει την περίοδο της κατηγορίας ή του μενού. Πολλαπλά επιτρεπτά windows στο ίδιο επίπεδο λειτουργούν ως **OR**, τα επίπεδα ως **AND**. Explicit blackout/deny υπερισχύει του allow. Κενή θετική λίστα σημαίνει «καμία επιπλέον χρονική απαίτηση» μόνο όταν το scope επιτρέπει ρητά all-day· ποτέ αμφίσημο κενό.

Ωράριο που περνά τα μεσάνυχτα αντιμετωπίζεται ως δύο συσχετισμένα segments με σαφή ημερολογιακή αναφορά. Τα windows αξιολογούνται στη **ζώνη ώρας του Store**· το deployment/scheduler αποθηκεύει UTC instants και αναγνωριστικό time zone. DST nonexistent/ambiguous local time δεν επιλύεται σιωπηλά: απαιτεί προεπισκόπηση UTC και ρητό κανόνα (πρόταση: reject αν αμφίσημο, ζητείται νέα ώρα). Η προσθήκη store timezone αποτελεί **ξεχωριστή αναγκαία εξάρτηση** πριν από scheduled publish.

### 3.5 Lifecycle, έκδοση, πρόβλεψη, rollback

- **Draft:** μεταβάλλεται μόνο με Manage, διατηρεί RowVersion/ETag. Δεν είναι ορατό σε καταναλωτές published menus.
- **Preview:** read-only υλικό του draft σε επιλεγμένο Store/Channel/DateTime, με εκτεταμένο preflight report, χωρίς δημιουργία εμπορικών πράξεων.
- **Publish:** server-side validation της **ολόκληρης** επιλεγμένης έκδοσης και των επιλεγμένων targets· σε SQL transaction δημιουργείται immutable MenuVersion με πλήρη resolved presentation/modifiers/availability, MenuAuditEntry και publication intent ανά target. Κάθε επιτυχής δημοσίευση αυξάνει μονοτονικά τον VersionNumber και δεν αλλάζει προηγούμενες εκδόσεις.
- **Schedule:** δημιουργεί προγραμματισμένη ενέργεια με idempotency key, author, UTC activation, target Store/Channel, expected draft revision. Ο worker πριν ενεργοποιήσει **επαναλαμβάνει** authorization policy, target/price/availability validation και CAS του publication pointer. Σε αστοχία στόχος παραμένει στην προηγούμενη ασφαλή έκδοση, audit με reason· δεν γίνεται σιωπηλή μερική ενεργοποίηση.
- **Selective publishing:** ο Publisher επιλέγει ρητά Store και Channel, με αναλυτική προεπισκόπηση ανά target. Το αποτέλεσμα είναι atomic *ανά target*· για multi-target αίτημα προτείνεται all-or-nothing στη φάση commit ή explicit per-target batch με εμφανή status, ποτέ σιωπηλή επιτυχία.
- **Rollback:** επιλέγεται προηγούμενη immutable MenuVersion, κλωνοποιείται ως **νέο Draft/Version N+1** και επαναπερνά validation/Publish. Δεν επιτρέπεται destructive update παλιάς έκδοσης ούτε "γυρισμός" παλαιού publication pointer χωρίς νέο audited publication event.
- **Unpublish:** αποσύρει μόνο συγκεκριμένο Store/Channel deployment με audit και expected version· το ιστορικό και άλλα ενεργά targets διατηρούνται.

Τυχόν παλαιά παραγγελία (όταν υλοποιηθεί) θα αναφέρεται σε MenuVersion και **δικό της order-line snapshot**. Η αλλαγή μενού/τιμοκαταλόγου δεν θα ανατιμολογεί ήδη αποδεκτή παραγγελία.

### 3.6 Τιμές και fail-closed πολιτική

Ο χρήστης επιλέγει συγκεκριμένο Store, Channel και business date κατά το Preview. Η Application καλεί την υπάρχουσα PriceResolutionService/Policy χωρίς νέα προτεραιότητα από το μενού. **Resolved**: εμφανίζεται τιμή EUR, VAT-inclusive flag και provenance (PriceListId/Code, policy version). **NotPriced/Missing**: σαφής προειδοποίηση και μη εμπορικά διαθέσιμη θέση. **Ambiguous**: μπλοκάρισμα θέσης και ορατά conflict IDs σε εξουσιοδοτημένο staff. **ItemUnavailable/Denied/Invalid**: μη προσβάσιμο προς πώληση, χωρίς fallback.

**Πρόταση publish gate:** αποτυχία για κάθε επιλεγμένο target που έχει ορατό, πωλήσιμο είδος με μη επιλύσιμη τιμή τη στιγμή έναρξης. Για μελλοντικό προγραμματισμό το preflight απαιτεί κατάλληλες ημερομηνίες ισχύος, επανελέγχεται υποχρεωτικά στην ενεργοποίηση και σε κάθε μελλοντική κατανάλωση. Η δημοσίευση **δεν παγώνει οικονομικές τιμές** και δεν δημιουργεί order snapshots· αν αλλάξει price list μετά το publish, το δημόσιο/read model δεν θα προβάλλει παλιά τιμή ως εγγυημένη ή θα αποσύρει προσωρινά τη μη ασφαλή θέση. Η αναθεώρηση της πολιτικής απαιτεί design approval.

## 4. Προτεινόμενα δεδομένα και σχέσεις — ΟΧΙ MIGRATION

| Οντότητα | Βασικά πεδία / σχέσεις | Ιδιότητα |
| --- | --- | --- |
| Menu | TenantId, MenuId, Code, Name, DisplayOrder, IsArchived, RowVersion | Aggregate root / draft identity |
| MenuDraft | TenantId, MenuId, DraftRevision, EditedUtc, RowVersion | Mutable authoring workspace |
| MenuCategoryDraft | TenantId, MenuId, CategoryId, Position, Name, Color, Visible | Ανήκει αποκλειστικά σε MenuDraft |
| MenuItemDraft | TenantId, MenuId, MenuCategoryId, MenuItemId, CatalogItemId, Position, overrides | Σύνθετο FK σε CatalogItem ίδιου Tenant |
| MenuAvailabilityRule | TenantId, MenuId, RuleId, MenuCategoryId?/MenuItemId?, StoreId, Channel, weekday/time/date, deny | Typed composite FK προς Menu/Category/Item (όχι polymorphic TargetType/TargetId) |
| ModifierGroup / ModifierOption | TenantId, GroupId, Revision, OptionId, limits, linked CatalogItemId όταν χρεώσιμο | Shared library |
| MenuItemModifierAssignment | TenantId, MenuItemId, GroupId, override subset/limits/order | Πλήρης snapshot κατά το publish |
| MenuVersion + VersionCategory/Item/Modifier | TenantId, MenuId, VersionId, VersionNumber, ContentHash, CapturedUtc, immutable resolved graph | Append-only published data |
| MenuDeployment / MenuPublication | TenantId, MenuId, StoreId, Channel, VersionId, effective UTC, State, RowVersion, OperationId | Μία ενεργή έκδοση *ανά Menu/Store/Channel*, όχι ένα Menu ανά Store/Channel |
| MenuAuditEntry | TenantId, EventId, actor, operation (draft edit/reorder/remove/publish/schedule/unpublish/rollback), target, before/after, time, correlation, outcome | Append-only audit όλων των μεταβολών |
| MenuOperationReceipt | TenantId, OperationId, RequestHash, Result, CapturedUtc | Duplicate request/idempotency protection |

### Ακεραιότητα SQL

1. Σύνθετα FK **(TenantId, MenuId)**, **(TenantId, CatalogItemId)**, **(TenantId, StoreId)**, όπως στο υπάρχον Catalog/Pricing. Όλα τα keys και unique constraints είναι Tenant-scoped. Καμία απλή FK που επιτρέπει cross-tenant αναφορά.
2. Unique Menu(TenantId, Code), MenuVersion(TenantId, MenuId, VersionNumber), current target key MenuDeployment(TenantId, MenuId, StoreId, Channel), MenuOperationReceipt(TenantId, OperationId), one item-position per draft container after save.
3. CHECK constraints για όχι κενά IDs, επιτρεπτούς enums/χρώματα (π.χ. safe HEX), position >= 0, Min/Max, date ordering, active channel value. Για διαθεσιμότητα, τα προαιρετικά MenuCategoryId/MenuItemId χρησιμοποιούν **typed composite FKs** με TenantId και MenuId, με CHECK ότι το scope είναι ακριβώς Menu ή Category ή Item· κανένα polymorphic unvalidated TargetId. Referential Restrict αντί cascade στο Catalog, PriceLists και immutable history.
4. RowVersion σε draft και deployment, expected-version CAS· transactional publish με σαφή isolation/locking strategy (ανά Menu+Target), bounded retries μόνο για αναγνωρισμένα transient deadlocks. Ισοτιμία και duplicate publish απαιτούν *ίδιο* OperationId/RequestHash, αλλιώς conflict.
5. SQL indexes για Menu(TenantId, Archived, DisplayOrder, MenuId), Category/Item(TenantId, MenuId, Position, Id), deployments(TenantId, StoreId, Channel, effective) και history(TenantId, MenuId, CreatedUtc). SQL-side deterministic paging/sorting πριν Skip/Take, όχι N+1.
6. Immutable versions δεν δέχονται UPDATE/DELETE από application roles. Audit append-only, ασφαλής retention policy· ποτέ destructive rollback σε αληθινό history. Row level scope ελέγχεται υποχρεωτικά στην Application/queries· το FK είναι πρόσθετη ασφάλεια, όχι υποκατάστατο authorization.

### Διαγραμματική σχέση

~~~text
Tenant ── CatalogCategory ── CatalogItem
   │                             ↑  (αναφορά, όχι αντιγραφή)
   ├── Store                      │
   ├── PriceList ── PriceListItem ┘
   ├── ModifierGroup ── ModifierOption
   └── Menu ── MenuDraft ── MenuCategoryDraft ── MenuItemDraft
          │                                  └── ModifierAssignment → Group
          ├── MenuVersion ── immutable version tree
          ├── MenuDeployment ── Store + Channel + Version
          └── MenuAuditEntry / OperationReceipt
~~~

## 5. Όρια ανά επίπεδο

| Layer | Ευθύνες |
| --- | --- |
| Domain | Menu/Version lifecycle, Position/Selection/Availability value objects, κανόνες modifiers, validation, state transitions χωρίς EF/Blazor/HTTP |
| Application | IMenus/IMenuDraftStore/IMenuPublishingStore, MenuManagementService, MenuPreviewService, PublishService, tenant authorization, pricing integration, idempotency, clock/time zone abstraction, CancellationToken |
| Infrastructure | EF configurations/migrations σε μελλοντικό PR, SQL repository/transaction/locks, upload image storage policy, indexes, projections, audit and scheduler execution |
| API / Blazor | Authorized SSR pages, CSRF-safe POST, DTO input parsing, error mapping, accessibly designed builder/dialogs, no business decisions in Razor |
| CLI | Τα ίδια Application commands με --dry-run, --apply, --confirm-database, --tenant-id, --store-id, --channel, --expected-version και σαφή exit codes |

Δεν επιτρέπονται απευθείας SQL writes από UI/CLI. Structured ILogger, correlation ID, actor/tenant/target/action/outcome και sanitized errors, χωρίς secrets ή PII σε logs.

## 6. Menu Builder — προδιαγραφή UI/UX

**Desktop ≥ 1024px:** σταθερό Back Office Sidebar/Topbar, toolbar επιλογής Menu + Tenant/Store/Channel, badge Draft/Published, Preview, Publish και audit. Τρεις ρυθμιζόμενες στήλες: **Κατηγορίες μενού** αριστερά (~23%), **Είδη μενού** στο κέντρο (~42%), **Κατάλογος ERP** δεξιά (~35%). Οι αναλογίες είναι προτεινόμενες· responsive min widths και scroll μέσα σε στήλες, χωρίς συνολικό horizontal page overflow.

~~~text
┌──────────────────────────────────────────────────────────────────────────┐
│ Back Office / Μενού  [Μενού: Κυρίως ▾] [Store ▾] [Channel ▾] [Draft]    │
│ [Undo] [Preview] [History]                     [Αποθήκευση] [Publish]   │
├───────────────────┬───────────────────────────────┬──────────────────────┤
│ ΚΑΤΗΓΟΡΙΕΣ ΜΕΝΟΥ │ ΕΙΔΗ ΣΤΗΝ ΚΑΤΗΓΟΡΙΑ          │ ΚΑΤΑΛΟΓΟΣ ERP        │
│ [+ Νέα]  Αναζήτ.  │ [+ από κατάλογο] [Sort]       │ Αναζήτηση / Φίλτρα  │
│ ≡ 1 Ορεκτικά  ⋯   │ ≡ 1 Χωριάτικη       ↑ ↓ ⋯    │ [+] Χωριάτικη        │
│ ≡ 2 Κυρίως    ⋯   │ ≡ 2 Μπιφτέκι        ↑ ↓ ⋯    │ [+] Μπιφτέκι         │
│ ≡ 3 Ποτά      ⋯   │ ≡ 3 Νερό            ↑ ↓ ⋯    │ [+] Νερό             │
│                   │                              │ Κατάλογος: 25 / 50  │
└───────────────────┴───────────────────────────────┴──────────────────────┘
       Side sheet: Επεξεργασία είδους / Modifier groups / Διαθεσιμότητα
~~~

**Τρόποι ενεργειών:** [+] ή drag ERP Catalog → συγκεκριμένη MenuCategory· reorder κατηγοριών/ειδών με drag handle, ↑/↓ ή numerical position· remove από μενού με ασφαλή confirmation (χωρίς DELETE CatalogItem). Inline validation, keyboard Ctrl+S όπου υποστηρίζεται από τη σελίδα, Esc για sheet/dialog, Alt+M για menu, Alt+P για preview όταν δεν συγκρούονται με browser shortcuts· τελικές συντομεύσεις θα ελεγχθούν στην πραγματική Blazor υλοποίηση.

**Side sheets:** επεξεργασία τίτλου/περιγραφής/εικόνας/χρώματος, toggle visibility, availability, modifier groups, επιλογή Store/Channel. Επιβεβαιώσεις για Remove/Unpublish/Publish/Schedule, με προβολή affected targets, pending changes και price alerts. Χρήστης Read έχει ίδια Preview αλλά χωρίς mutable controls· μη επιτρεπτές ενέργειες αποκλείονται πάντα και στον server.

**Mobile < 768px:** όχι στριμωγμένες 3 στήλες. Καρτέλες **Κατηγορίες | Είδη | Κατάλογος**, sticky τρέχον menu/category, sticky bottom action bar με Save/Preview και εμφανές validation state. Επιλογή ERP item σε queue προς συγκεκριμένη κατηγορία. Drag παρέχεται προαιρετικά, πάντα διαθέσιμα Move Up/Down/Position. Tablet 768–1023px: δύο στηλοθεσίες (items + slide-over catalog) ή καρτέλες ανά διαθέσιμο πλάτος. Μεγάλες touch targets (~44px), σωστό focus restore, captions, visible focus, ARIA live regions και ελάχιστη κίνηση.

**States:** skeleton loading, empty menu + CTA, no catalog matches, read-only, stale draft conflict, publish validation failure, offline/disconnected (read-only stale indicator, καμία τοπική ουρά), success receipt, loss of session/permission, file upload rejection. Η προεπισκόπηση έχει επιλεγμένο Store/Channel/ώρα, προβάλλει χρωματικά status και εξηγεί γιατί ένα είδος λείπει/δεν πωλείται. Οπτικός έλεγχος σε 320/375/390/768/1366/1920, Light/Dark, orientation, 200% zoom και keyboard-only είναι gate.

## 7. Ροές χρήστη και δύσκολες περιπτώσεις

1. **Create draft:** Owner → Μενού → νέο Code/Name → επιλέγει targets → δημιουργεί MenuCategory → βρίσκει υπάρχον CatalogItem → [+] ή drag → Save. Επανάληψη POST με ίδιο OperationId δεν δημιουργεί duplicate MenuItem.
2. **Reorder/remove:** μεταβολή positions και Remove φέρνει επιβεβαίωση «Αφαίρεση μόνο από το μενού». Απαγορεύεται τροποποίηση Catalog record. Ταυτόχρονο edit από άλλο tab → optimistic conflict με Reload/Review, ποτέ silent last-write-wins.
3. **Shared modifier:** χρήση library group σε πολλά menu items, local overrides στο draft. Αλλαγή library revision εμφανίζει diff, ανανέωση απαιτεί επιβεβαίωση· published versions ανεπηρέαστες.
4. **Preview:** store A/channel DineIn/ώρα → εφαρμόζονται menu/category/item windows → price resolution. Inactive/foreign CatalogItem, missing price και ambiguous price εμφανίζονται στο staff ως block, όχι ως αυθαίρετη τιμή.
5. **Publish:** target matrix A:DineIn, B:Delivery → preflight ανά target → expected revision και confirmation → immutable version + audit + activation intents. Αν χάθηκε εξουσιοδότηση ή άλλαξε draft, απόρριψη. Μία αποτυχία δεν μετατρέπεται σε επιτυχία άλλου target χωρίς σαφή partial-result πολιτική.
6. **Schedule/rollback:** local calendar + timezone + DST validation → UTC preview → activation-time recheck → audited new version. Αποτυχία scheduler κρατά προηγούμενο active version. Unpublish A:DineIn δεν επηρεάζει B:Delivery.
7. **Παράλληλα μενού:** Brunch και Drinks συνυπάρχουν στο ίδιο Store/Channel. Το read model παραδίδει *δύο μενού* και δεν αποσιωπά επικάλυψη. Κατηγορία κρυμμένη ή σε blackout αφαιρεί μόνο τις αντίστοιχες θέσεις.
8. **Price drift:** αλλαγή/απενεργοποίηση price list μετά publish → cache invalidation, re-resolve, fail closed. Παλαιό snapshot price preview δεν χρησιμοποιείται ως τιμή παραγγελίας.
9. **Bad inputs & attacks:** spoofed Tenant/Store/actor, cross-tenant modifier or CatalogItem IDs, malformed color/image, XSS in overrides, path traversal uploads, duplicate form, oversized payload, cancelled request και SQL deadlock απορρίπτονται με ασφαλή μηνύματα.

## 8. Έλεγχοι αποδοχής και αυτοματοποιημένα tests (ΠΡΟΤΑΣΗ)

| ID | Acceptance | Προτεινόμενη κάλυψη |
| --- | --- | --- |
| MENU-01 | Tenant-safe menu CRUD και stable ordering | Domain + isolated SQL + real browser |
| MENU-02 | Menu item αναφέρεται μόνο σε δικό του ενεργό CatalogItem | composite FK, Application/SQL cross-tenant negatives |
| MENU-03 | Remove/reorder δεν μεταβάλλει ERP Catalog | SQL before/after και browser |
| MENU-04 | Modifier group Min/Max/defaults, overrides, published pinning | Domain property tests, SQL concurrency, E2E |
| MENU-05 | Store/Channel/day/time windows, midnight/DST, blackout | fake clock/time zone + SQL + E2E |
| MENU-06 | Πολλά published menus στην ίδια ώρα και store/channel | SQL & browser, deterministic sorting |
| MENU-07 | Draft/Preview/Publish και immutable version history | unit/integration, repeat/read-only validation |
| MENU-08 | Scheduled activation, stale check, safe rollback as new version | isolated scheduler tests + E2E |
| MENU-09 | Owner/Member, malicious URLs/POST/CSRF, target permissions | HTTP/SSR, SQL isolation, browser |
| MENU-10 | Price Missing/Ambiguous/Inactive fail-closed, channel precedence | PriceResolutionService integration & E2E |
| MENU-11 | Duplicate publish, concurrent edits, retries, cancellation | parallel SQL DbContexts, idempotency |
| MENU-12 | Light/Dark, 320/375/390/768/1366/1920, zoom, keyboard | real authenticated browser, genuine screenshots |
| MENU-13 | CLI/UI κοινά commands, help, dry-run, exit codes, audit | CLI integration + logs |
| MENU-14 | No Order/POS/fiscal/print/offline writes | integration assertions + manual release review |

Test environments: synthetic Tenant A/Tenant B με τουλάχιστον δύο Stores, Owner και Member, 1/2/10 CatalogCodes, overlap price lists, χρονικά όρια, stale drafts. Browser automation τύπου Playwright μόνο σε **πραγματικό Development app**, όχι screenshot mock fixture. Απαιτείται SQL Server isolated integration με ρητό disposable DB. Κάθε FAIL: πραγματικό screenshot, reproducible steps, browser version, viewport, theme, correlation, SQL evidence. **Δεν έχουν εκτελεστεί** αυτά τα μελλοντικά tests.

## 9. Ασφάλεια, απόδοση και φορολογικό checkpoint

- Authorization: server-side membership, Tenant/Store ownership σε κάθε read/write. Ως έχει το main, **μόνο Owner** μπορεί να διαχειριστεί/δημοσιεύσει και Member μόνο να διαβάσει. Για delegated ανά Store publisher απαιτείται νέα, ρητά εγκεκριμένη granular policy· δεν επινοείται σήμερα Store role.
- Security: SSR same-origin, antiforgery, least privilege, output encoding, validated HEX/asset paths, upload limits/type scanning και isolated secure object storage (πριν υλοποιηθούν εικόνες), server-owned audit actor, secure exceptions and structured logs.
- SQL/performance: περιορισμένα projections, Set-based item lookup, index-aware search/paging, scoped version cache keyed by (Tenant, Store, Channel, MenuVersion, pricing revision) με invalidation· publish transaction bounded και όχι μεγάλες UI-waiting locks. Καμία επίδοση δεν δηλώνεται PASS χωρίς μετρήσεις.
- **Fiscal:** τα Menu/Modifier labels και οι εμπορικές τιμές δεν καθορίζουν αυτομάτως νόμιμο VAT, myDATA classification, περιβαλλοντικά τέλη, εκπτώσεις, rounding, ειδικούς συνδυασμούς και IMPACT Detail mapping. Τεκμηρίωση ΑΑΔΕ [myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) και [IMPACT Core API v2.8.12](https://einvoiceapi.impact.gr/swagger/Index.html), όπως εμφανίζονταν στις 16/11/2026. Οι fiscal reviewers πρέπει να εγκρίνουν χωριστά taxable modifier vs textual option, split/discount/bundle/variant, ποσότητες, tax rate και rounding **πριν από οποιοδήποτε Order/POS/issue**. Καμία φορολογική έγκριση δεν προκύπτει από τον σχεδιασμό.
- **Risk:** ακυρότητα snapshot από αλλαγή catalog/price list, cross-target partial publish, DST, stale revisions, ανεπάρκεια store-level permissions, caching παλιάς διαθεσιμότητας, περιεχόμενο εικόνων, migrations on populated DB. Όλα απαιτούν tests και ελεγχόμενη rollout διαδικασία.

## 10. Πιθανός αντίκτυπος migrations και ανάκτηση

**Σήμερα: μηδενικός.** Σε μεταγενέστερα PR προβλέπονται *additive* Menu/Version/Publication/Audit tables, composite FKs/indexes, ίσως Store timezone και scheduler/outbox. Πριν από apply: οριστική έγκριση schema και constraints, migration script review, backup+restore test, dry-run σε ακριβώς επιβεβαιωμένη Dev βάση, zero-downtime compatibility assessment και δικό του rollout plan. **Down migration που διαγράφει immutable version/audit είναι καταστροφική και δεν αποτελεί προεπιλεγμένο rollback.** Προτιμάται code rollback με backward-compatible schema και εγκεκριμένο recovery.

## 11. Προτεινόμενη σειρά υλοποίησης — ΟΧΙ ΕΓΚΕΚΡΙΜΕΝΗ

1. **9.6A.1 — Draft Menu vertical slice:** Domain + Application + migration μόνο μετά από έγκριση, Menu/Category/Item draft CRUD, linked Catalog item, position/reorder/remove, Owner/Member, CLI και πραγματικός responsive three-column builder. **Χωρίς Publish, Modifiers, image upload, POS.**
2. **9.6A.2 — Preview & Availability:** read-only preview ανά Store/Channel, item visibility, χρονικές ζώνες/ωράριο, fail-closed pricing και πραγματικά UI/SQL tests.
3. **9.6A.3 — Modifier library & overrides:** groups, options, required rules, shared/local version semantics, ασφαλές commercial linkage χωρίς φορολογική ενεργοποίηση.
4. **9.6A.4 — Immutable versions & Publish:** selective target publications, preflight, audit, authorization, concurrent edit guards, history.
5. **9.6A.5 — Scheduling/rollback & images:** resilient scheduler, version rollback-as-new, secure media, maintenance tests.
6. **9.6A.6 — Full UI/SQL/security acceptance:** cross-browser, all widths, dark/light, keyboard/zoom, load, failure/recovery, GitBook manuals και ρητή ανθρώπινη αποδοχή.

Η σειρά/όρια 9.6A.2–9.6A.5 μπορεί να αναθεωρηθούν μετά τα findings. Ολοκληρωμένες κάθετες ροές με δοκιμές υπερισχύουν πολλών ημιτελών PR.

## 12. Προϋποθέσεις πριν από το 9.6A.1

- Όλα τα υποχρεωτικά 9.5C.5 gates **PASS** με evidence: πραγματικός browser Owner/Member, Tenant/Store, SQL persistence, 320–1920, Light/Dark, keyboard/zoom, conflict/retry και μηδενικά σοβαρά defects· υπογραφή αποδοχής από τον ιδιοκτήτη.
- Επικαιροποίηση live GitBook τεκμηρίωσης (χωριστή change request και review· δεν αποτελεί SQL gate).
- Αποδοχή σχεδίου Menu boundaries, draft identity/ordering, per-store permissions και time zone dependency.
- Αποδοχή πρώτου implementation scope, change impact, backup/migration procedure. Κάθε migration απαιτεί **επιπλέον ρητή έγκριση εφαρμόγής** και σωστή βάση.
- Επιβεβαίωση official AADE/IMPACT docs εκ νέου αν επηρεάζεται fiscal concept, χωρίς ενεργοποίηση issuance.

{% hint style="info" %}
**Πότε χρησιμοποιείται:** για architecture/design review και σχεδιασμό tests. **Δεν χρησιμοποιείται:** ως manual τελικού χρήστη υπαρκτής οθόνης, ως SQL migration approval ή ως φορολογική πιστοποίηση. Επικαιροποιήστε τη σελίδα μόνο μαζί με επιβεβαιωμένο νέο GitHub baseline.
{% endhint %}

## 13. Checklist τεχνικού και troubleshooting σχεδιασμού

- [ ] Επιβεβαίωση merged PR #60, current main CI και 9.5C.5 E2E evidence.
- [ ] Επιβεβαίωση Tenant/Store/channel matching και υπάρχουσας PriceResolutionPolicy.
- [ ] Απόφαση timezone ανά Store, midnight/DST και target publication semantics.
- [ ] Κανένα μυστικό, οικονομική τιμή ή μη εγκεκριμένος VAT κανόνας σε menu overrides.
- [ ] Ενιαίο semantic operation για GUI, keyboard και CLI.
- [ ] Review σύνθετων FK, immutable history, retries, authorization και restore.
- [ ] UI design review όλων των viewport/focus/error states.
- [ ] Ρητή έγκριση 9.6A.1 πριν από δημιουργία production-related artifacts.

| Ερώτημα | Προτεινόμενη αντιμετώπιση |
| --- | --- |
| Το είδος φαίνεται στον Catalog αλλά όχι στο menu preview | Ελέγξτε category/item visibility, windows, Store/Channel, IsActive και price status |
| Δύο μενού είναι ενεργά μαζί | Επιτρεπτό· εμφανίζονται ξεχωριστά, όχι αυθαίρετη υπερίσχυση |
| Δεν βρίσκεται τιμή ή υπάρχει Ambiguous | Αποκλεισμός πώλησης, staff diagnostics, διόρθωση price lists, καμία αυτόματη τιμή |
| Αλλαγή modifier επηρεάζει ήδη published menu | Πρέπει να μην το επηρεάζει· η δημοσιευμένη έκδοση διατηρεί snapshot |
| Δύο Owner αποθηκεύουν το ίδιο draft | 409/conflict, Reload & Review, όχι τελευταίο overwrite |
| Προγραμματισμένο publish σε ώρα αλλαγής DST | Reject/explicit resolution πριν αποθηκευτεί UTC instant |
| Ζητείται rollback παλιού published menu | Νέα version και νέο audit· ποτέ DELETE/UPDATE του ιστορικού |
| Δεν υπάρχει granularity ανά Store για publish | Owner-only μέχρι να υλοποιηθούν και ελεγχθούν πραγματικά store grants |

**Best practices:** πρώτα σχεδιάστε ξεκάθαρη ιδιοκτησία δεδομένων, μετά πραγματικές CRUD/preview δοκιμές, τέλος publish. Μην συγχέετε display menu, master catalog, pricing, fiscal και order snapshots. Διατηρείτε τεχνικό design και πραγματική user τεκμηρίωση χωριστά.
