# AENAON ERP — Manual 62: Αυτόματο προεπιλεγμένο μενού καταστήματος και συγχρονισμός ERP Catalog

**Κατάσταση:** Υλοποίηση σε **Draft PR #62**, όχι στο `main`. **Δεν εγκρίθηκε ούτε εφαρμόστηκε η νέα 12η migration** `20261117130000_DefaultStoreMenuCatalogSync` σε καμία πραγματική βάση. Το πραγματικό UI/SQL acceptance παραμένει `NOT VERIFIED`.  
**Σχετικό manual:** [61 — Draft Menu Management](61-draft-menu-management.md).  
**Προέλευση απαίτησης:** Κατά τη δημιουργία καταστήματος να παράγεται προεπιλεγμένο μενού. Κατά τη δημιουργία νέας κατηγορίας ERP να εισάγεται χωρίς χειροκίνητο «Φόρτωση» στα μενού. Η προηγούμενη συμφωνία απαιτεί οι κατηγορίες να φέρνουν και τα είδη τους.

{% hint style="warning" %}
**ΜΗΝ εκτελέσετε `--migrate-database --apply` χωρίς νέο, ανεξάρτητο SQL script review, backup/restore plan, dry-run στην ακριβή βάση και ρητή εξουσιοδότηση της migration #12.** Η επιτυχημένη εφαρμογή της 11ης migration στις 17/11/2026 **δεν αποτελεί έγκριση** για τη 12η.
{% endhint %}

## 1. Σκοπός

Να αποφεύγεται η χειροκίνητη αρχική κατασκευή μενού. Η επιχείρηση διατηρεί **έναν ERP Catalog ως πηγή αλήθειας**, πολλά μενού ανά Tenant, αλλά κάθε Store αποκτά **δική του προεπιλεγμένη επιλογή μενού**. Δεν δημιουργούνται διπλά ERP είδη ή τιμές.

## 2. Πώς λειτουργεί

### Αρχική δημιουργία επιχείρησης και καταστήματος

Η υπάρχουσα Development εντολή `--initialize-business --apply`, όταν δημιουργεί **την πρώτη επιχείρηση και το αρχικό Store σε νέα άδεια βάση**, καταχωρίζει **στην ίδια SQL transaction** ένα `RestaurantMenu` με κωδικό `DEFAULT-{StoreCode}`, όνομα «Προεπιλεγμένο Μενού», ένα `MenuDraft` (revision 1) και τη σύνδεση `StoreDefaultMenus` (TenantId, StoreId, MenuId). Εφόσον το ERP Catalog είναι αρχικά κενό, το μενού αρχίζει κενό και **γεμίζει αργότερα αυτομάτως** όταν δημιουργηθούν κατηγορίες και είδη.

**Σημείωση:** Η σημερινή έκδοση διαθέτει εντολή για **αρχική** δημιουργία καταστήματος, όχι γενική διαχείριση πολλών νέων καταστημάτων. Στο μελλοντικό workflow «Νέο κατάστημα» πρέπει να επαναχρησιμοποιηθεί ο ίδιος `StoreDefaultMenuFactory` στην ίδια transaction, όχι ξεχωριστό ad-hoc insert.

### Νέα κατηγορία ERP Catalog

Με τη δημιουργία **νέας** `CatalogCategory` από το Back Office ή το CLI, ο server —εντός της ίδιας συναλλαγής με την εντολή του ERP Catalog— εισάγει μία **ξεχωριστή κατηγορία παρουσίασης** σε **κάθε μη αρχειοθετημένο πρόχειρο μενού** του Tenant. Η κατηγορία φέρει `CatalogCategoryId` που δείχνει στη μοναδική ERP πηγή της, ώστε οι μεταγενέστερες ενέργειες να αποφεύγουν τα διπλά. Δεν περνά κατηγορία από άλλο Tenant.

Η ιεραρχική διαδρομή ERP εμφανίζεται ως ετικέτα (π.χ. `Φαγητά / Κυρίως`) όπου χωρά στο μήκος της περιγραφής. **Δεν αλλάζει** η δομή ή γονική σχέση του ERP Catalog.

### Νέο είδος ERP Catalog

Κάθε **νέο ενεργό** `CatalogItem` (προϊόν ή υπηρεσία) εισάγεται αυτομάτως ως `MenuDraftItem` σε κάθε πρόχειρο μενού που περιλαμβάνει την **αντίστοιχη κατηγορία με πηγή `CatalogCategoryId`**. Εμφανίζεται με την ERP περιγραφή και κωδικό, χωρίς νέα τιμή πώλησης. Τα προϊόντα και οι τιμοκατάλογοι ERP **δεν τροποποιούνται**.

### Δημιουργία πρόσθετου μενού

Το νέο μενού πέρα από το default συνεχίζει να ξεκινά με **όλες τις ενεργές ERP κατηγορίες ΚΑΙ τα ενεργά είδη τους**. Δημιουργούνται ξεχωριστά draft objects με σύνδεση ERP category/item. Τα παλαιά χειροποίητα μενού δεν διαγράφονται και δεν μετατρέπονται αυθαίρετα.

## 3. Κρίσιμες εγγυήσεις

- **Tenant isolation:** σύνθετα tenant-scoped FKs και server-side Owner permission. Οι client-provided StoreId/TenantId τιμές επαληθεύονται στον server.
- **Καμία αλλαγή στην τιμή:** μόνο `CatalogItemId` σύνδεση, η τιμολόγηση συνεχίζει από ERP Pricing/PriceResolution.
- **Atomicity:** η δημιουργία ERP κατηγορίας/είδους και η ενημέρωση των draft menus συμμετέχουν στην ίδια SQL transaction. Αν δεν μπορεί να ολοκληρωθεί ο συγχρονισμός, η νέα καταχώριση δεν αποθηκεύεται μερικώς.
- **Concurrency:** κοινό `sp_getapplock` ανά Tenant στον Catalog και Menu draft μηχανισμό, και `MenuDraft.Revision` concurrency token.
- **Idempotency:** διατηρούνται τα request receipts του Catalog/menus, uniqueness στο Store default ανά (Tenant, Store), μοναδικό linked CatalogCategoryId ανά μενού και CatalogItemId ανά κατηγορία.
- **Manual customization:** μετονομασία/αφαίρεση ή reorder κατηγορίας μενού δεν αλλάζει την αντίστοιχη ERP κατηγορία. Η νέα δημιουργία ERP κατηγορίας προστίθεται σε όλα τα ενεργά draft menus. Όταν αφαιρείται μια κατηγορία από ένα μενού, η αναφορά της χάνεται εκεί και η μελλοντική **δημιουργία νέου είδους** στην ίδια ERP κατηγορία δεν την επαναφέρει αυθαίρετα.
- **Προστασία όγκου:** έως 1.000 μη αρχειοθετημένα μενού ανά Tenant για την κάθε αυτόματη καταχώριση, έως 10.000 θέσεις ανά κατηγορία/μενού. Υπέρβαση απορρίπτεται ατομικά, με σαφές σφάλμα. Μελλοντικός υψηλός φόρτος απαιτεί ορισμένο batch/outbox design.
- **Όχι ανεξέλεγκτος επανασυγχρονισμός:** η δημιουργία νέων ERP κατηγοριών/ειδών συγχρονίζεται. Η μετονομασία, μεταφορά, απενεργοποίηση και εκ νέου ενεργοποίηση ERP δεδομένων **δεν** αναδιατάσσουν αυτόματα ειδικά παραμετροποιημένα μενού· χρειάζεται ξεχωριστή πολιτική συγχρονισμού.
- **Όχι Publish/POS/fiscal:** εξακολουθούν να είναι ανενεργά.

## 4. Βάση δεδομένων — προτεινόμενη νέα migration #12

`20261117130000_DefaultStoreMenuCatalogSync` (πηγαίος κώδικας μόνο, όχι εφαρμοσμένη):

| Αντικείμενο | Μεταβολή | Σκοπός |
| --- | --- | --- |
| `dbo.StoreDefaultMenus` | Νέος πίνακας PK (TenantId, StoreId) και FKs σε Store/Menu του ίδιου Tenant | Ένα default ανά Store, πολλαπλά menus μπορούν ακόμη να στοχεύουν Stores σε μελλοντικό Publish |
| `dbo.MenuDraftCategories.CatalogCategoryId` | Νέο nullable GUID με σύνθετο FK σε CatalogCategories | Επίσημη ταυτότητα ERP προέλευσης για auto-sync |
| Unique filtered index | (TenantId, MenuId, CatalogCategoryId) για μη-null source | Αποκλεισμός διπλών ERP κατηγοριών στο ίδιο Menu |
| Υπάρχοντα MenuDrafts / MenuItems | **Καμία διαγραφή** | Προστασία των ήδη φτιαγμένων μενού και παραμετροποιήσεων |

**Πριν την έγκριση:** SQL diff, μη-idempotent/idempotent script, Tenant/Store composite FK checks, backup, restore rehearsal, πρόβλεψη rollback και έλεγχος μη καταστροφικών `Down()`. Η πτώση της migration αφαιρεί metadata και default-link πίνακα: **μην εκτελείται χωρίς χωριστή έγκριση**.

## 5. Υφιστάμενο Development κατάστημα

Η `AenaonERP_Dev` είχε ήδη κατάστημα πριν υλοποιηθεί η νέα λογική. Επομένως **η αρχική εντολή `--initialize-business` δεν επαναλαμβάνεται**. Μετά την έγκριση και εφαρμογή **της 12ης migration**, υπάρχει ειδική idempotent CLI λειτουργία που αναγνωρίζει υπάρχον default και δεν το διπλοδημιουργεί:

~~~powershell
# Μόνο προβολή (NO WRITE), με login Owner
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --store-id "STORE_GUID" --confirm-database AenaonERP_Dev --action ensure-store-default --dry-run

# ΜΟΝΟ ΑΦΟΥ εγκριθεί ρητά και η 12η migration και η εφάπαξ δημιουργία
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --store-id "STORE_GUID" --confirm-database AenaonERP_Dev --action ensure-store-default --apply
~~~

Η εφαρμογή δημιουργεί το προεπιλεγμένο μενού του συγκεκριμένου Store μαζί με τις ενεργές ERP κατηγορίες και τα ενεργά είδη της στιγμής εκείνης. **Δεν πειράζει παλιά QA/custom menus**.

Εάν κάποιο παλαιό μενού έχει ήδη χειροκίνητες κατηγορίες με `CatalogCategoryId = NULL`, οι νέες ERP κατηγορίες προστίθενται αυτόματα, αλλά νέα είδη σε παλιά, μη συσχετισμένη κατηγορία δεν αντιγράφονται χωρίς ασφαλή reconciliation. Αποφεύγουμε αυτόματο matching αποκλειστικά βάσει ονόματος.

## 6. Έλεγχοι πριν και μετά

1. **Pre-check:** ακριβής βάση, 11 applied / 1 pending για το feature branch (εφόσον μόνη νέα pending είναι η #12), αντίγραφο ασφαλείας, έλεγχος restore.
2. **SQL review:** νέος πίνακας και νέο nullable source FK. Δεν εγκρίνεται μόνο από επιτυχημένο `dotnet build`.
3. **Explicit apply approval:** ζητείται ξεχωριστά από τον Owner. **Δεν έχει δοθεί στην παρούσα φάση.**
4. **Post-check:** αναμενόμενο 12 applied / 0 pending μόνο **εάν** εγκριθεί και ολοκληρωθεί η migration.
5. **Real E2E:** αρχική νέα επιχείρηση/Store, αυτόματο default, νέα ERP category, νέα ERP item, άμεσο SSR/CLI/SQL persistence, idempotent retries, cross-Tenant, Owner/Member, concurrent changes, ανενεργά είδη, χειροκίνητες τροποποιήσεις και Store selection.
6. **Visual QA:** 320/375/390/768/1366/1920, light/dark, keyboard-only, zoom, genuine screenshots και owner acceptance.

## 7. Troubleshooting και Best Practices

| Πρόβλημα | Έλεγχος |
| --- | --- |
| Το default μενού δεν εμφανίζεται σε παλιό Store | Δεν εκτελέστηκε approved μία-φορά provisioning / νέα migration |
| Η κατηγορία δεν προστέθηκε | Έλεγχος Tenant, μη αρχειοθετημένων menu drafts, ορίου και SQL transaction |
| Ένα νέο είδος δεν μπήκε σε παλιά κατηγορία | Δεν υπάρχει ασφαλής ERP source association για παλαιά χειροποίητη κατηγορία |
| Menu revision conflict | Refresh και επανάληψη μετά τον έλεγχο αλλαγών |
| Το είδος έχει λάθος τιμή | Το μενού δεν αποθηκεύει τιμές — ελέγξτε τον ERP PriceResolution |
| Δημιουργήθηκε default δεύτερη φορά | Η βάση απαγορεύει δεύτερη εγγραφή (Tenant, Store), αναζητήστε το υπάρχον MenuId |
| Αποτυχία sync σε πολλές καρτέλες | Ελέγξτε το προβλεπόμενο όριο και τα ασφαλή server logs — όχι χειροκίνητο bulk SQL |

**Χρήση:** Αρχική παραμετροποίηση και σταδιακή ενημέρωση draft menu από το ERP Catalog.  
**Μη χρήση:** Παραγγελίες, ζωντανές τιμές, πληρωμές, αδειοδοτημένο fiscal/POS, παραγωγική δημοσίευση, αυθαίρετη επαναφορά παλαιού menu.
