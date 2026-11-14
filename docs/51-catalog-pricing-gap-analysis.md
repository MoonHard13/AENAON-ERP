# AENAON ERP — 9.5C.1: Catalog & Pricing Gap Analysis

**Ημερομηνία αποτύπωσης:** 14/11/2026  
**Βάση ελέγχου:** `main` μετά το merged [PR #50](https://github.com/MoonHard13/AENAON-ERP/pull/50), τελευταία υλοποιημένη λειτουργία 9.5B.3B.  
**Τύπος εργασίας:** read-only code/documentation audit και σχεδιασμός. **Δεν υλοποιείται** νέο feature, SQL migration, αλλαγή δεδομένων ή φορολογική ροή.  
**Απόφαση προϊόντος:** πρώτα ολοκλήρωση προϊόντων, υπηρεσιών και τιμοκαταλόγων· μετά το 9.6A (μενού εστίασης). Excel/imports και γενικές μαζικές εργασίες μετατίθενται για αργότερα.

{% hint style="warning" %}
Τα «Υπάρχει» παρακάτω τεκμηριώνονται από **πηγαίο κώδικα, SQL configurations, Blazor/CLI και tests**, όχι από επιβεβαιωμένο live deployment. Η κατάσταση εφαρμοσμένων migrations στην `AenaonERP_Dev`, η επιτυχία πρόσφατου build/CI και το οπτικό acceptance σε desktop/mobile **δεν ελέγχθηκαν** κατά το παρόν documentation-only audit.
{% endhint %}

## 1. Σκοπός και όρια

Απογραφή υπάρχουσας εμπορικής καρτέλας και τιμολόγησης στα πέντε επίπεδα: **Domain, SQL Server, Application, Blazor/CLI και αυτοματοποιημένα tests**. Καθορισμός ελλείψεων, προτεραιοτήτων και αντικειμενικών acceptance criteria χωρίς πρόωρη δημιουργία POS, παραγγελιών, φορολογικών παραστατικών ή Android APK.

Το σημερινό Back Office είναι Development-only και κάθε πρόσβαση οφείλει να παραμένει tenant-safe. Ο Owner/Manage μεταβάλλει δεδομένα, ο Member/Read μπορεί μόνο να τα βλέπει. Οι μονάδες μέτρησης και οι λογιστικοί τύποι **δεν** αποτελούν φορολογικούς κωδικούς.

## 2. Απογραφή υφιστάμενων οντοτήτων / πεδίων

| Οντότητα | Επιβεβαιωμένα πεδία | Περιορισμοί |
| --- | --- | --- |
| `CatalogItem` | `Id`, `TenantId`, `CategoryId`, `Code`, `Name`, `Kind` (Product/Service), `TracksStock`, `UnitOfMeasure`, `AccountingType`, `IsActive` | Κωδικός έως 40, όνομα έως 200 χαρακτήρες· κοινή μοναδικότητα κωδικού ανά tenant· αμετάβλητο `Kind` και `TenantId` |
| `CatalogCategory` | Κωδικός, όνομα, ενεργή κατάσταση, προαιρετική ιεραρχία `ParentCategoryId` | Tenant-safe κατηγορίες, αυτόματοι αριθμητικοί κωδικοί και πρόληψη ανεπίτρεπτων μετακινήσεων |
| `PriceList` | `Id`, `TenantId`, `StoreId` (nullable), `Code`, `Name`, `Channel`, `Currency`, `PricesIncludeVat`, `ValidFrom`, `ValidUntil`, `IsActive` | Κωδικός έως 30, όνομα έως 160, `EUR`, κλειστό διάστημα ημερομηνιών |
| `PriceListItem` | `Id`, `TenantId`, `PriceListId`, `CatalogItemId`, `UnitPrice` | Μία τιμή ανά τιμοκατάλογο+είδος, `decimal(18,4)`, τιμή ≥ 0 |
| `PriceResolutionCandidate` / `PriceSelectionResult` | Scope, κανάλι, ισχύς, price basis, ποσό, κατάσταση `Missing/Unique/Ambiguous` | **Read-only** εμπορική επιλογή, χωρίς snapshot ή υπολογισμό φόρου |

**Υφιστάμενες επιλογές μονάδας:** Piece, Kilogram, Gram, Liter, Milliliter, Portion, Hour, Service.  
**Λογιστικοί τύποι:** Merchandise, FinishedProduct, RawMaterial, Consumable, Service, FixedAsset.  
**Κανάλια:** All, DineIn, Takeaway, Delivery, Retail, Online.

Πηγές: [CatalogItem](../src/Aenaon.Domain/Catalog/CatalogItem.cs), [CatalogCommercialTypes](../src/Aenaon.Domain/Catalog/CatalogCommercialTypes.cs), [PriceList](../src/Aenaon.Domain/Catalog/PriceList.cs), [PriceListItem](../src/Aenaon.Domain/Catalog/PriceListItem.cs), [PriceResolutionPolicy](../src/Aenaon.Domain/Catalog/PriceResolutionPolicy.cs).

## 3. Έλεγχος ανά επίπεδο

| Επίπεδο | Υλοποιημένα | Κενά / προσοχή |
| --- | --- | --- |
| Domain | Validation Product/Service, μονάδα/λογιστικός τύπος, ενεργή κατάσταση, τιμή 4 δεκαδικών, σαφής πολιτική προτεραιότητας | Όχι barcode, αναλυτική περιγραφή, εκτεταμένα χαρακτηριστικά, διακριτή καταλληλότητα πώλησης, ιστορικό αναθεώρησης |
| SQL Server / EF Core | `CatalogItems`, `PriceLists`, `PriceListItems`, `TenantId` σε σύνθετα FK, unique keys, CHECK constraints, indexes· **8 migrations στον κώδικα** | Όχι στήλες barcode/description, price history/audit rows, customer-group scope, revision token/snapshot· όχι SQL constraint για επικαλυπτόμενα χρονικά διαστήματα |
| Application | `CatalogReadService`/`CatalogWriteService`, `PriceListManagementService`, `PriceResolutionService`, tenant authorization, read-only resolution | Όχι service εντοπισμού επικαλύψεων κατά τη συντήρηση, service εμπορικού ιστορικού, pricing contract για μεταγενέστερη immutable order line |
| Blazor | `/catalog`, `/catalog/manage`, `/price-lists`, `/price-lists/manage`, `/price-lists/preview`· search, sorting, status, επιλογείς, ημερομηνία, εμφάνιση τιμής και αμφισημίας | Δεν υπάρχουν φόρμες/στήλες νέων πεδίων, ιστορικό τιμών, διαγνωστικά προληπτικής επικάλυψης ή customer-group pricing· οπτικό acceptance preview ακόμη εκκρεμεί |
| CLI/CMD | `--manage-catalog`, `--manage-price-lists` και `--resolve-price --dry-run` χρησιμοποιούν κοινά application services· create/update/activate/deactivate, διαχείριση/αφαίρεση τιμών | Απουσιάζουν νέα πεδία, audit/history και overlap-check commands· νέο CLI πρέπει να έχει ίδιους κανόνες με το GUI |
| Tests | Domain, Application, API/SSR, isolated SQL Server: έλεγχοι tenant isolation, SQL constraints, δικαιωμάτων, τεσσάρων επιπέδων προτεραιότητας και `Ambiguous` | Δεν υπάρχουν σενάρια για πεδία που δεν έχουν υλοποιηθεί· απαιτούνται επιπλέον concurrency/overlap/history, schema migration και UI/CLI regression checks |

Πηγές: [Catalog read](../src/Aenaon.Application/Catalog/CatalogReadService.cs), [Catalog write](../src/Aenaon.Application/Catalog/CatalogWriteService.cs), [Price list management](../src/Aenaon.Application/Catalog/PriceListManagementService.cs), [SQL management store](../src/Aenaon.Infrastructure/Persistence/SqlPriceListManagementStore.cs), [SQL price resolution](../src/Aenaon.Infrastructure/Persistence/SqlPriceResolutionStore.cs), [Blazor preview](../src/Aenaon.Api/Ui/Pages/PricePreview.razor).

## 4. Catalog gaps — διαχωρισμός πραγματικών ελλείψεων

| Κωδικός | Πεδίο / λειτουργία | Σήμερα | Προτεραιότητα | Πρόταση |
| --- | --- | --- | --- | --- |
| C01 | Εσωτερικός κωδικός/SKU | **Υπάρχει** `Code` και μοναδικότητα ανά tenant | Διατήρηση | Δεν αντικαθίσταται από barcode |
| C02 | Barcode/EAN/GTIN | **Απουσιάζει** ως ανεξάρτητο πεδίο | **P0** | Προαιρετικό, με ξεκάθαρη πολιτική μορφών, checksum όπου εφαρμόζεται και uniqueness ανά tenant· μη αποκλεισμός ειδών χωρίς barcode |
| C03 | Αναλυτική περιγραφή | **Απουσιάζει**· το `Name` είναι σύντομο (200) | **P0** | Προαιρετικό `LongDescription`, όρια, safe HTML/text, read/write σε GUI/CLI |
| C04 | Βασική κατηγοριοποίηση / ιεραρχία | **Υπάρχει** | Διατήρηση | Regression σε μετακίνηση/ανενεργή κατηγορία |
| C05 | Μονάδα, λογιστικός τύπος | **Υπάρχουν** | Διατήρηση | Παραμένουν ανεξάρτητα από ΦΠΑ/myDATA |
| C06 | Ενεργό, Product/Service, stock-tracking flag | **Υπάρχουν** | Διατήρηση | `TracksStock` δεν σημαίνει πραγματική αποθήκη |
| C07 | Πωλείται στο POS / χρησιμοποιείται μόνο εσωτερικά | **Δεν υπάρχει χωριστό flag** | **P1** | Σχεδιασμός εμπορικής καταλληλότητας χωρίς να συγχέεται με accounting type |
| C08 | Επιπλέον χαρακτηριστικά (μάρκα, προμηθευτής, εικόνα, χαρακτηριστικά/ετικέτες) | **Απουσιάζουν** | **P2 / κατόπιν επιλογής** | Όχι τυφλή προσθήκη δεκάδων στηλών· πρώτα πραγματικά use cases |
| C09 | Ιστορικό μεταβολών καρτέλας | **Δεν υπάρχει durable entity-level history** | **P1** | Actor, UTC, αλλαγμένο πεδίο, πριν/μετά και correlation χωρίς ευαίσθητες τιμές |
| C10 | Ποσοτικές μετατροπές/συσκευασίες | **Δεν υπάρχει** | **P1 design** | Συμβόλαιο για μελλοντική αποθήκη/συνταγολόγιο, όχι αυτοσχέδιος υπολογισμός αποθέματος |

**Διευκρίνιση:** το σημερινό barcode δεν ταυτίζεται με το εσωτερικό `Code`. Προτού επιλεγεί SQL constraint, καθορίζουμε αν επιτρέπονται και εσωτερικοί μη-GTIN κωδικοί και πώς χειρίζονται πολλαπλά barcodes ανά είδος. Τα προαιρετικά εμπορικά χαρακτηριστικά της C08 δεν θεωρούνται ήδη εγκεκριμένο scope.

## 5. Pricing gaps — κανόνες που χρειάζονται οριστικοποίηση

| Κωδικός | Περιοχή | Σήμερα | Προτεραιότητα | Κριτήριο αναβάθμισης |
| --- | --- | --- | --- | --- |
| P01 | Πολλαπλοί τιμοκατάλογοι ανά είδος/υπηρεσία | **Υπάρχει**· ξεχωριστή τιμή ανά λίστα | Διατήρηση | Κάλυψη Product και Service |
| P02 | Κατάστημα, κανάλι, ημερομηνία | **Υπάρχουν** | Διατήρηση | Ρητή ημερομηνία συναλλαγής· store belongs to tenant |
| P03 | Κατάταξη τιμών | **Υπάρχει**: store+channel, store+All, All+channel, All+All | Διατήρηση | Καμία αλλαγή προτεραιοτήτων χωρίς policy version και tests |
| P04 | Ισοβάθμιες ενεργές τιμές | **Εντοπίζονται** από `Resolve` ως `Ambiguous` | **P0 βελτίωση** | Προληπτική διάγνωση πριν από save/activation ή αναφορά επιλυτέων overlaps, χωρίς αυθαίρετη επιλογή |
| P05 | Ιστορικό πραγματικών αλλαγών τιμής/πολιτικής | **Απουσιάζει** (τρέχουσες τιμές ενημερώνονται, επιτρεπτή αφαίρεση row) | **P0** | Durable, append-only audit για αλλαγή τιμής, actor/UTC/source, πριν/μετά· ασφαλής ανάγνωση Owner/Read |
| P06 | Περιοχές πελατών/ομάδες πελατών | **Απουσιάζουν** από οντότητα, query και precedence policy | **P1 design πριν τη γενικευμένη τιμολόγηση** | Ορισμός tenant-safe customer-group scope και προτεραιότητας, χωρίς silent fallback |
| P07 | Concurrency / ταυτόχρονη τροποποίηση | **Υπάρχει μερικώς**: expected price/profile και Serializable transactions | **P1** | Έλεγχος διπλών ενημερώσεων, πολιτική ABA/row version, idempotent writes όπου χρειάζεται |
| P08 | Ιστορική δέσμευση τιμής σε παραγγελία | **Δεν υπάρχει** (δεν υπάρχει Order ακόμη) | **P0 αρχιτεκτονικό contract / μεταγενέστερη υλοποίηση** | Immutable commercial snapshot ανά γραμμή κατά τη δημιουργία παραγγελιών, όχι ζωντανή επανεπίλυση |
| P09 | Ενιαίος χειρισμός ποσών/ΦΠΑ | **Μόνο `PricesIncludeVat` και `UnitPrice`**· δεν υπολογίζεται συντελεστής | **Fiscal gate** | Ξεχωριστά φορολογικά contracts και επίσημη έγκριση προτού εκδοθεί παραστατικό |
| P10 | Προβολή ιστορικού και warnings UI/CLI | **Μόνο η τρέχουσα προεπισκόπηση αναφέρει Ambiguous** | **P1** | Read-only ιστορικό, σύγκρουση με σαφή αιτία/πλαίσιο και ίδια συμπεριφορά GUI/CLI |

**Σημαντικές υφιστάμενες διασφαλίσεις:** η ποσότητα τιμής έχει έως 4 δεκαδικά, το 0 επιτρέπεται, αρνητική τιμή απορρίπτεται· υπάρχουν `TenantId` σε όλα τα price joins/FK, `ExpectedUnitPrice` / `CheckExpected` στις ενημερώσεις, `Expected...` στο policy update, `Serializable` transactions, όριο 50 τιμών ανά τρέχον batch και αποφυγή σιωπηρής αλλαγής ποσών όταν αλλάζει η ένδειξη με/χωρίς ΦΠΑ.

Η τωρινή εισαγωγή πολλών τιμών στην ίδια σελίδα **δεν** ισοδυναμεί με το αναβλημένο Excel import / γενικό bulk toolkit.

## 6. Πολιτική επίλυσης και εντοπισμού συγκρούσεων

Η σημερινή επιλογή τιμής διατηρείται ως έχει:

| Rank | Scope | Channel |
| --- | --- | --- |
| 1 (υψηλότερο) | Ακριβές κατάστημα | Ακριβές κανάλι |
| 2 | Ακριβές κατάστημα | All |
| 3 | All stores | Ακριβές κανάλι |
| 4 | All stores | All |

Σε **δύο υποψήφιες τιμές ίδιου υψηλότερου rank**, ακόμη και με *ίσο ποσό*, επιστρέφεται `Ambiguous`. Οι περίοδοι `ValidFrom`–`ValidUntil` περιλαμβάνουν και τις δύο ακραίες ημερομηνίες. Προτείνεται διαγνωστικό overlap σε ενεργοποίηση τιμοκαταλόγου, αλλαγή scope/channel/period και καταχώρηση τιμής, λαμβάνοντας υπόψη **κοινά είδη**, rank και πραγματική τομή ημερομηνιών· προτεραιότητα δεν σημαίνει υποχρεωτικά σύγκρουση όταν υπάρχει νόμιμο fallback διαφορετικού rank.

Απαιτείται απόφαση αν το overlap απλώς προειδοποιεί ή απορρίπτει συγκεκριμένες εγγραφές. Δεν υποκαθιστά τον read-time fail-closed κανόνα `Ambiguous`. Ο μελλοντικός customer-group κανόνας χρειάζεται ξεχωριστό precedence design και regression tests.

## 7. Προτεινόμενη σειρά υλοποίησης

| Βήμα | Παραδοτέο | Scope / όρια |
| --- | --- | --- |
| **9.5C.1 — τώρα** | Gap analysis, matrix, τεστ αποδοχής, roadmap | **Documentation-only**· κανένα SQL write ή feature |
| **9.5C.2** | Catalog Core Completeness | Barcode + αναλυτική περιγραφή, συμβάσεις/validation, additive SQL migration, GUI/CLI, tests και user manual |
| **9.5C.3** | Pricing Integrity & History | Durable audit, overlap detection, concurrency και ασφαλές browsing ιστορικού· χωρίς παράκαμψη της τωρινής resolution policy |
| **9.5C.4** | Pricing Scopes & Commercial Readiness | Απόφαση για customer groups, προσδιορισμός precedence, αρχιτεκτονικό immutable price snapshot contract και συμβατότητα με μελλοντικές παραγγελίες |
| **9.5C.5** | Τελικό Catalog/Pricing Acceptance | Functional, browser desktop/mobile/light/dark/keyboard, SQL, CLI, security και τελική ενημέρωση τεκμηρίωσης |
| **9.6A** | Μενού εστίασης | **Μόνο μετά την αποδοχή 9.5C**, όχι παράλληλα |

Οι C07/C08/C10 απαιτούν τελική επιχειρησιακή απόφαση ως προς το πότε είναι πραγματικά αναγκαίες. Δεν είναι λόγος να υλοποιηθούν άσχετες οντότητες πριν από το 9.6A, αλλά πρέπει να καταγραφούν ρητά ως covered/deferred/not-applicable.

## 8. Acceptance criteria — οδηγός για τα επόμενα PR

### 9.5C.2 — Είδη

- [ ] Barcode προαιρετικό και **διακριτό** από `Code`· τεκμηριωμένος κανόνας μορφών, κενών και uniqueness ανά tenant.
- [ ] LongDescription προαιρετική, με κοινή server-side validation, ασφαλή αποθήκευση/απεικόνιση.
- [ ] Εμφάνιση, αναζήτηση όπου δικαιολογείται, δημιουργία και επεξεργασία από Blazor και CLI.
- [ ] Additive migration, backfill/defaults μη καταστροφικά, unique index όπου απαιτείται· καμία τροποποίηση υπάρχοντος SKU.
- [ ] Owner/Manage write, Member/Read read, cross-tenant απομόνωση και προσοχή σε πολλούς stores.
- [ ] Domain/SQL/Application/API/CLI regression tests και ελληνικό manual.

### 9.5C.3 — Τιμές

- [ ] Για κάθε επιτυχημένη αλλαγή/αφαίρεση τιμής διατηρείται μη αλλοιώσιμη εγγραφή audit με tenant, list/item, actor, timestamp UTC, old/new και action.
- [ ] Καταγραφή και της αλλαγής pricing scope/channel/period/`PricesIncludeVat` και ενεργοποίησης.
- [ ] Δεν υπάρχει μερική εγγραφή audit/τιμής αν αποτύχει transaction.
- [ ] Εντοπίζονται περιπτώσεις ίδιας προτεραιότητας με κοινή ημέρα/είδος, όχι λανθασμένα τα νόμιμα fallbacks.
- [ ] `Ambiguous` παραμένει fail-closed στο UI και στο CLI· δεν επιλέγεται τιμή με random/latest/lowest ID.
- [ ] Concurrent writes, stale expected values, rollback και retries δοκιμάζονται σε isolated SQL Server.

### 9.5C.4 — Εμπορικές πολιτικές

- [ ] Το customer-group scope, αν εγκριθεί, έχει συγκεκριμένο μοντέλο, δικαιώματα, precedence και περίπτωση απουσίας ομάδας.
- [ ] Υπάρχει versioned συμβόλαιο future immutable snapshot: tenant/store/item, price list, business date, channel, price basis, currency, selected unit price και στοιχεία policy version.
- [ ] Κανένα live recalculation δεν αντικαθιστά ιστορική τιμή παραγγελίας όταν η λειτουργία παραγγελιών υλοποιηθεί.
- [ ] Καμία φορολογική χρέωση ή έκδοση δεν υπονοείται από εμπορικό snapshot.

### 9.5C.5 — Κλείσιμο ενότητας

- [ ] Διαδρομές Create/Edit/List/Search/Preview λειτουργούν για προϊόντα **και** υπηρεσίες.
- [ ] Tests Windows/Linux, isolated SQL Server 2022 migration/regression, business date boundaries, inactive item/list, tenant/store isolation και ambiguity.
- [ ] Πραγματικός browser έλεγχος desktop/mobile, light/dark, High DPI, keyboard-only, focus, empty/error/loading states.
- [ ] Αναγνωστικά endpoints και audit δεν εκθέτουν τιμές άλλου tenant ή προσωπικά στοιχεία.
- [ ] Ενημερωμένα README, roadmap, `docs/requirements.md` και GitBook-ready manuals.
- [ ] Στην `AenaonERP_Dev`, πριν από κάθε νέα migration: dry-run, backup, script review, ρητή έγκριση· έλεγχος applied/pending μετά την εφαρμογή.

## 9. myDATA / IMPACT — φορολογικό όριο

Η επίσημη [σελίδα εκδόσεων ΑΑΔΕ](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) εμφανίζει την **v2.0.2** κατά τον έλεγχο 14/11/2026. Η δημόσια [IMPACT τεκμηρίωση API](https://einvoiceapi.impact.gr/landing.html), το [IMPACT OpenAPI](https://einvoiceapi.impact.gr/swagger/coreapi/swagger.json) και το [compatibility gate](18-mydata-impact-compatibility.md) παραμένουν υποχρεωτικές πηγές **πριν από fiscal implementation**.

- Το `PricesIncludeVat` **δεν περιέχει** συντελεστή ΦΠΑ, απαλλαγή, myDATA classification ή εγκεκριμένο IMPACT payload.
- Οι εμπορικές `UnitOfMeasure` και `AccountingType` δεν μετατρέπονται αυτόματα σε φορολογικούς κωδικούς.
- Δεν ενεργοποιούνται έκδοση, διαβίβαση, πληρωμές ή περιβαλλοντικά τέλη. Τα **254 Pending** fiscal observations δεν αποτελούν εγκεκριμένα mappings.
- Αλλαγή σε τιμοκαταλόγους/προϊόντα απαιτεί τεκμηρίωση επίπτωσης στο μελλοντικό invoice line και ξεχωριστό fiscal review· το παρόν read-only audit δεν αποδεικνύει συμβατότητα τελικής τιμολόγησης.

## 10. Οδηγός χρήσης / επιθεώρησης για τεχνικούς (Development)

### Προαπαιτούμενα

- Repository AENAON ERP και `global.json` (.NET 10 SDK 10.0.404 ή συμβατό patch).
- SQL Server 2022 / **μόνο** Development `AenaonERP_Dev`, έγκυρο HTTPS session.
- Εγγεγραμμένος χρήστης με Tenant Read για προεπισκόπηση· Owner/Manage για επιτρεπόμενες εμπορικές μεταβολές.

### Βήματα read-only επιβεβαίωσης

1. Ενημερώστε το `main` και ελέγξτε SHA/PR:
   ```powershell
   git switch main
   git pull --ff-only origin main
   git log -1 --oneline
   dotnet --list-sdks
   ```
2. Ελέγξτε την κατάσταση migrations **χωρίς apply**:
   ```powershell
   dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
   ```
3. Με ήδη εκκινημένο Development API, ανοίξτε `https://localhost:7140/catalog` για είδη/υπηρεσίες και `https://localhost:7140/price-lists` για τιμοκαταλόγους. Η προβολή `/price-lists/preview` είναι **GET/read-only**.
4. Συγκρίνετε τις πραγματικές φόρμες με τα «Υπάρχει»/«Απουσιάζει» των πινάκων. Μην καταχωρίσετε δοκιμαστικές αλλαγές σε δεδομένα πελάτη.
5. Για unit/integration tests σε κατάλληλο περιβάλλον, μετά από restore/build, εκτελέστε:
   ```powershell
   dotnet restore AenaonERP.slnx --locked-mode
   dotnet build AenaonERP.slnx -c Release --no-restore
   dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
   ```
6. Για SQL integration tests χρησιμοποιήστε **μόνο** τη διαδικασία isolated SQL Server CI ή άλλη απομονωμένη βάση, όχι `AenaonERP_Dev` χωρίς ξεχωριστή έγκριση.

### Πότε χρησιμοποιείται / πότε όχι

**Χρησιμοποιείται** για design review, εκτίμηση αλλαγών και καθορισμό επόμενων PR. **Δεν χρησιμοποιείται** ως οδηγία παραγωγικής έκδοσης POS/παραστατικών, ως τεκμήριο επιτυχούς deployment ή ως εντολή μαζικής εισαγωγής.

### Troubleshooting

| Σύμπτωμα | Έλεγχος |
| --- | --- |
| Δεν εμφανίζονται οι τιμοκατάλογοι | Ελέγξτε ενεργό Tenant, Read permission, migrations με dry-run και εφαρμογή που εκκινεί |
| `Ambiguous` | Διαβάστε τα conflict list IDs, ημερομηνία, store/channel και συγκρουόμενες τιμές· μη διαλέγετε αυθαίρετο ποσό |
| `NotPriced` | Ελέγξτε ενεργό είδος, λίστα, ημερομηνία και κανάλι |
| 403/άρνηση | Επαληθεύστε membership, Owner/Member, store ownership |
| UI διαφέρει από το manual | Ελέγξτε branch/commit, επανεκκίνηση server, cache των fingerprinted assets |
| Εκκρεμεί migration | Μη συνεχίσετε σε apply χωρίς backup, SQL script review και ρητή επιβεβαίωση Development βάσης |

### Logging, ασφάλεια και ανάκτηση

Οι υπάρχουσες ενέργειες περνούν από Tenant authorization, SQL constraints, HTTPS/anti-CSRF και όπου απαιτείται structured logging. **Structured logs δεν ισοδυναμούν με διατηρούμενο ιστορικό τιμών**. Η audit υλοποίηση προτείνεται ως χωριστό βήμα και πρέπει να αποφεύγει passwords, tokens και προσωπικά δεδομένα. Στο παρόν documentation-only PR δεν υπάρχει migration, επομένως rollback σημαίνει μόνο `git revert` του σχετικού documentation commit. Σε μελλοντική additive migration: backup, dry-run, restore rehearsal και ξεχωριστή απόφαση για rollback.

## 11. Έκβαση / τελικός έλεγχος 9.5C.1

- [x] Έλεγχος κώδικα Domain, EF Core SQL configurations, Application, Blazor, CLI και αντιπροσωπευτικών tests.
- [x] Διαχωρισμός υλοποιημένων, ελλειπόντων και σκόπιμα αναβλημένων λειτουργιών.
- [x] Προτεραιότητες και σαφής σειρά 9.5C.2–9.5C.5 πριν από 9.6A.
- [x] Υποχρεωτικό myDATA/IMPACT impact review και διατήρηση των απαγορεύσεων fiscal issuance.
- [ ] Live Windows/Linux tests, SQL status και οπτικό desktop/mobile acceptance **δεν εκτελέστηκαν** σε αυτό το audit.
- [ ] Αποδοχή της προτεινόμενης σειράς/κριτηρίων μέσω review PR.

**Συμπέρασμα:** ο υφιστάμενος κατάλογος και οι τιμοκατάλογοι αποτελούν λειτουργική Development βάση, **όχι πλήρως ολοκληρωμένο εμπορικό υποσύστημα**. Οι ενότητες 9.5C.2–9.5C.5 προτείνονται πριν από 9.6A. Excel και γενικές μαζικές εργασίες παραμένουν εκτός scope.
