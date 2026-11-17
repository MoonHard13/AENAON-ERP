# AENAON ERP — Manual 61: 9.6A.1 Draft Menu Management

**Κατάσταση:** Προτεινόμενη υλοποίηση σε **Draft PR #62**, όχι στο `main`. Δεν αποτελεί επιβεβαιωμένο διαθέσιμο χαρακτηριστικό ούτε οδηγία εγκατάστασης σε production.  
**Έκδοση baseline:** `main` μετά το merged PR #61, commit `374b04b6f67ae34d98ac03ddab7be85ff9d460d9`.  
**Πηγή απαιτήσεων:** [Manual 58 — Menu Architecture](58-menu-management-architecture.md), [Manual 60 — Approved Design](60-menu-management-final-design-review.md).

{% hint style="warning" %}
Το 9.6A.1 είναι **Draft-only**. Δεν προσφέρει Publish, Preview, modifiers, διαθεσιμότητα, POS, KDS, φορολογική έκδοση, IMPACT transmission, offline ή εκτύπωση. Η ενδέκατη migration **εφαρμόστηκε αποκλειστικά στη Development `AenaonERP_Dev` από τον ιδιοκτήτη στις 17/11/2026**, κατόπιν SQL review, επιβεβαιωμένου από τον ίδιο backup/VERIFYONLY και ρητής έγκρισης. Το τελικό `--dry-run` εμφάνισε **11 applied / 0 pending**. **Δεν εφαρμόστηκε σε production**. Μην επανεκτελέσετε το παραγόμενο SQL script και μην υποθέσετε ότι το PR είναι αποδεκτό για merge.
{% endhint %}

## 1. Σκοπός

Το **Draft Menu Management** επιτρέπει στον Owner μίας επιχείρησης να συνθέτει ανεξάρτητα μενού από τον ήδη υπάρχοντα ERP Catalog. **Κατά τη δημιουργία νέου μενού εισάγονται αυτόματα όλες οι ενεργές κατηγορίες ΜΑΖΙ με τα ενεργά είδη τους**· δεν απαιτείται χειροκίνητη προσθήκη όλων των ειδών. Οι κατηγορίες και οι συνδέσεις είναι ανεξάρτητες ανά μενού, χωρίς νέα τιμή πώλησης. Ο Member μπορεί μόνο να διαβάζει.

Το `Menu` δεν είναι `CatalogCategory`, ενώ η αφαίρεση ενός `MenuDraftItem` **δεν** αφαιρεί το `CatalogItem` της επιχείρησης. Η αρχική φόρτωση είναι εφάπαξ snapshot, **αλλά η νέα επέκταση (Manual 62, Development migration #12 owner-reported applied 17/11/2026) προσθέτει αυτόματα ΝΕΕΣ ERP κατηγορίες σε όλα τα μη αρχειοθετημένα προσχέδια και ΝΕΑ ERP είδη σε μενού με αντίστοιχη συνδεδεμένη κατηγορία**. Δεν επαναδιατάσσονται αυτόματα ήδη προσαρμοσμένα μενού μετά από rename/move ή deactivate. Οι τιμές παραμένουν αποκλειστικά στον υφιστάμενο ERP μηχανισμό τιμολόγησης.

## 2. Προϋποθέσεις

- Απομονωμένο περιβάλλον Development με .NET SDK από `global.json`, SQL Server 2022, GitHub `main` ενημερωμένο και έγκυρη συνεδρία Owner/Member.
- Εξουσιοδοτημένος Tenant και το επιλεγμένο Store του ίδιου Tenant.
- Στη `AenaonERP_Dev` η ενδέκατη migration αναφέρεται εφαρμοσμένη (11/0, 17/11/2026). Σε κάθε **άλλο** περιβάλλον απαιτούνται ξεχωριστό review, backup, έγκριση και schema check. Το PR #62 παραμένει Draft και τα πραγματικά UI/SQL acceptance checks εκκρεμούν.
- Ανενεργά είδη ERP δεν μπορούν να προστεθούν σε νέο προσχέδιο. Αν είδος απενεργοποιηθεί μεταγενέστερα, εμφανίζεται σχετική προειδοποίηση έως να αφαιρεθεί.

## 3. Διαθέσιμες στην προτεινόμενη υλοποίηση λειτουργίες

| Ενέργεια | Owner | Member | Σημειώσεις |
| --- | --- | --- | --- |
| Λίστα/επιλογή μενού | Ναι | Ναι | Tenant scoped |
| Νέο μενού με όλες τις ενεργές ERP κατηγορίες και τα ενεργά είδη | Ναι | Όχι | Αυτόματο, ατομικό SQL snapshot, μόνο του ίδιου Tenant |
| Φόρτωση κατηγοριών και ειδών σε παλιό κενό προσχέδιο | Ναι | Όχι | Μόνο όταν δεν έχει ούτε κατηγορίες ούτε είδη, χωρίς διπλοεγγραφές |
| Δημιουργία νέου μενού | Ναι | Όχι | Μοναδικός κωδικός ανά Tenant |
| Μετονομασία | Ναι | Όχι | Δεν αλλάζει code ή Tenant |
| Προσθήκη/επεξεργασία/αφαίρεση άδειας κατηγορίας | Ναι | Όχι | Δεν επιτρέπεται διαγραφή κατηγορίας με είδη |
| Αλλαγή θέσης κατηγορίας | Ναι | Όχι | Κουμπιά ↑/↓ και αναθεώρηση |
| Αναζήτηση ενεργών ERP ειδών | Ναι | Ναι | Prefix code/name, έως 50 αποτελέσματα |
| Προσθήκη/αφαίρεση συνδεδεμένου είδους | Ναι | Όχι | FK στο ήδη υπάρχον CatalogItem |
| Προαιρετικό όνομα εμφάνισης | Ναι | Όχι | Δεν αλλάζει ERP περιγραφή |
| Αλλαγή θέσης είδους | Ναι | Όχι | Αναδιάταξη μέσα στην ίδια κατηγορία |
| Publish/Preview/Modifiers/Price override | Όχι | Όχι | **Εκτός 9.6A.1** |

## 4. Οθόνη Desktop, Tablet και Mobile — ανανεωμένο UI στο Draft PR #62

Η οθόνη **δεν έχει πλέον μόνιμες τρεις στήλες**. Εμφανίζει ένα ενιαίο responsive grid κατηγοριών. Η επιλογή πλακιδίου ανοίγει τα είδη της κατηγορίας σε δεύτερο grid, με breadcrumbs για επιστροφή. Οι δευτερεύουσες ενέργειες (μετακίνηση, επεξεργασία, ορατότητα, επιβεβαιωμένη αφαίρεση) βρίσκονται πίσω από διακριτικό γρανάζι ανά πλακίδιο. Ο ERP Catalog ανοίγει σε πτυσσόμενο πάνελ κάτω από τα είδη.

Το redesign είναι μόνο Razor/CSS/test/documentation και **δεν μεταβάλλει την ασφάλεια ή τη SQL δομή**. Παραμένουν η επιλογή μενού, το default ανά Store, ο αυτόματος συγχρονισμός νέων ERP κατηγοριών και ειδών, το Owner/Member, antiforgery, expected revision και idempotency. Η νέα αναλυτική ροή, τα πεδία και το checklist περιγράφονται στο [Manual 63 — Tile Menu Builder](63-menu-builder-tile-redesign.md).

Η προβλεπόμενη responsive διάταξη καλύπτει 320/375/390/768/1366/1920px, Light/Dark και keyboard-only πρόσβαση, **αλλά το πραγματικό Development browser/visual acceptance παραμένει NOT VERIFIED**. Το Web Estiasis χρησιμοποιείται μόνο ως οπτική αναφορά, χωρίς μεταφορά λειτουργικού ή αισθητικού branding.

## 5. Βήματα εργασίας (μόνο μετά το approved Dev setup)

1. Συνδεθείτε με Owner, επιλέξτε σωστό Tenant/Store και μεταβείτε στην επιλογή **Μενού εστίασης**.
2. Πατήστε **Νέο μενού**, συμπληρώστε Code και Name και πατήστε **Δημιουργία μενού με είδη ERP**.
3. Το νέο προσχέδιο φορτώνει αυτόματα τις **ενεργές** ERP κατηγορίες και **όλα τα ενεργά είδη τους**. Οι υποκατηγορίες εμφανίζουν διαδρομή ονόματος (π.χ. `Φαγητά / Κυρίως`) όπου χωράει στο όριο πεδίου. Οι κατηγορίες χωρίς ενεργά είδη εμφανίζονται κενές. Ανενεργές κατηγορίες/είδη δεν εισάγονται.
4. Για **παλιό κενό μενού** που δημιουργήθηκε πριν τη βελτίωση, επιλέξτε το και πατήστε **Φόρτωση κατηγοριών και ειδών ERP**. Η εισαγωγή απορρίπτεται εάν το μενού έχει ήδη κατηγορίες/είδη, ώστε να μη χαθούν προσαρμογές.
5. Προαιρετικά προσθέστε **δική σας κατηγορία παρουσίασης**, ή στον Κατάλογο ERP επιλέξτε επιπλέον ενεργό είδος προς σύνδεση. Οι αλλαγές αυτές αφορούν μόνο το μενού.
6. Αλλάξτε θέση με ↑/↓, ορίστε εναλλακτικό όνομα εμφάνισης ή αφαιρέστε είδος από το μενού.
7. Για αλλαγή κατηγορίας, χρησιμοποιήστε την **Επεξεργασία**. Για αφαίρεση, αφαιρέστε πρώτα όλα τα συνδεδεμένα είδη.
8. Σε περίπτωση **Conflict** από δεύτερο Owner, κάντε refresh, ελέγξτε την τρέχουσα revision και ξαναδοκιμάστε. Δεν αντικαθιστούμε τις αλλαγές άλλου χρήστη.

Κάθε φόρμα εκτελεί ανεξάρτητη atomic SQL εντολή με νέο `RequestId` και αναμενόμενη `Revision`. Δεν υπάρχει σιωπηλό autosave ή μαζική δημοσίευση.

## 6. Στοιχεία οντοτήτων

| Οντότητα | Πεδίο | Σκοπός |
| --- | --- | --- |
| RestaurantMenu | TenantId, Code, Name, IsArchived | Ταυτότητα μενού / μοναδικός code ανά Tenant |
| MenuDraft | MenuId, TenantId, Revision, EditedUtc | Μεταβαλλόμενο πρόχειρο, optimistic concurrency |
| MenuDraftCategory | Id, MenuId, Name, Position, IsVisible | Κατηγορία παρουσίασης |
| MenuDraftItem | Id, MenuCategoryId, CatalogItemId, NameOverride, Position | Σύνδεση με ERP είδος |
| MenuDraftOperationReceipt | TenantId, RequestId, RequestHash, Revision, ActorUserId | Idempotency και βασικό operational audit |

**Διατήρηση ακεραιότητας:** SQL composite tenant FKs, unique Tenant/Code και unique category/item, noncascade FK προς CatalogItems, transaction και revision. Οι συνθήκες authorization επαναξιολογούνται server-side. Η κρυφή HTML τιμή `tenantId` **δεν** είναι απόδειξη δικαιώματος.

## 7. CLI/CMD — προτεινόμενες εντολές

{% hint style="info" %}
Οι εντολές προϋποθέτουν λειτουργικό τοπικό clone στο branch που περιέχει το feature, απομονωμένη και εγκεκριμένη βάση, `--launch-profile https`, interactive terminal, και ολοκληρωμένο και ελεγμένο schema. Το `--dry-run` ελέγχει μόνο μορφή/πρόσβαση και **δεν υποκαθιστά** τη δοκιμή αλλαγών SQL.
{% endhint %}

~~~powershell
# Προβολή προσχεδίων χωρίς εγγραφή
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action list --dry-run

# Προεπισκόπηση δημιουργίας — δεν αποθηκεύει
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action create-menu --code MAIN --name "Κυρίως Μενού" --dry-run

# Εφαρμογή δημιουργίας (ΜΟΝΟ σε εγκεκριμένο περιβάλλον Development)
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action create-menu --code MAIN --name "Κυρίως Μενού" --apply

# Ενδεικτική προσθήκη κατηγορίας, με υποχρεωτική τρέχουσα revision
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-menus --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action add-category --menu-id "MENU_GUID" --expected-revision 1 --name "Ορεκτικά" --dry-run
~~~

Σημείωση: οι τιμές `TENANT_GUID` / `MENU_GUID` είναι υποδείγματα. Ο κωδικός χρήστη ζητείται διαδραστικά και δεν περνά ως command-line argument. Exit codes: `0` επιτυχία, `2` άκυρη εντολή, `3` άρνηση, `4` απουσία, `5` σύγκρουση, `6` duplicate, `130` ακύρωση.

## Καταγεγραμμένη εφαρμογή στη Development — 17/11/2026

Ο ιδιοκτήτης εκτέλεσε την εγκεκριμένη `--migrate-database --apply --confirm-database AenaonERP_Dev`. Το αποτέλεσμα έδειξε `11 applied / 0 pending`, καθώς και επιτυχή καταχώριση `20261117110000_MenuDraftManagement`. Η αμέσως επόμενη ανάγνωση `--dry-run` επιβεβαίωσε `11 applied / 0 pending` χωρίς αλλαγές. Τα στοιχεία είναι **user-provided console logs**, όχι ανεξάρτητη σύνδεση reviewer στη Development SQL. Η εφαρμογή schema **δεν αποτελεί UI/E2E acceptance** και δεν εξουσιοδοτεί merge του PR.

## Ασφάλεια αυτόματης φόρτωσης (9.6A.1)

**Συμπλήρωση:** Για την αλληλεπίδραση με την αρχική δημιουργία Store και τον μεταγενέστερο συγχρονισμό νέων κατηγοριών/ειδών ισχύει το [Manual 62](62-default-menu-auto-catalog-sync.md). Η 12η migration **αναφέρθηκε ότι εφαρμόστηκε επιτυχώς από τον χειριστή στη Development (12/0, 17/11/2026)**. Εκκρεμούν ξεχωριστό μετέπειτα dry-run, default menu provisioning και πραγματικά E2E tests· το PR δεν έχει εγκριθεί για merge.

Η εισαγωγή είναι **ατομική** μέσα στη συναλλαγή δημιουργίας/επιλογής κενής παλιάς καρτέλας. Ο server επανελέγχει `TenantPermission.Manage`, expected revision και RequestId, και διαβάζει μόνο `IsActive` κατηγορίες/είδη του αντίστοιχου Tenant. Δεν δημιουργούνται νέα ERP CatalogItems και δεν τροποποιούνται CatalogCategories, ERP τιμοκατάλογοι ή φορολογικά δεδομένα.

Ισχύει προληπτικό όριο 10.000 κατηγοριών και 20.000 συνολικά εισαγόμενων ειδών ανά μενού (έως 10.000 ανά κατηγορία). Υπέρβαση απορρίπτει την **ολόκληρη** συναλλαγή, όχι μερικό αποτέλεσμα. Η εισαγωγή παλιού κενού μενού αυξάνει revision μόνο εφόσον βρεθούν ενεργές κατηγορίες και γίνει επιτυχής αποθήκευση.

Για acceptance απαιτούνται πραγματικοί έλεγχοι: νέο μενού με κατηγορίες+είδη, ιεραρχία κατηγοριών, παράλειψη ανενεργών, υπάρχον κενό μενού, duplicate submit, stale revision, Member deny, cross-Tenant isolation, reload και SQL persistence. Το CI δεν υποκαθιστά authenticated Development UI testing.

## 8. Database review και ασφαλής εγκατάσταση

**Καμία αυτόματη SQL εφαρμογή.** Μελλοντική εφαρμογή στη `AenaonERP_Dev` απαιτεί διαδοχικά:

1. Έλεγχο GitHub PR diff, νέας migration και FK/index/constraints SQL script από ειδικό.
2. Επιβεβαίωση ότι αφορά την πραγματική εγκεκριμένη **Development** βάση (όχι παραγωγή).
3. Έγκυρο backup, έλεγχο restore και documented rollback.
4. `--migrate-database --dry-run` / εξέταση pending migration **χωρίς** write.
5. Ξεχωριστή έγκριση για `--migrate-database --apply --confirm-database AenaonERP_Dev`.
6. Έλεγχο αρχικού/τελικού schema, permissions, SQL persistent records και πραγματικό E2E με απομονωμένα δεδομένα.

Το downgrade της migration είναι ενδεχομένως **καταστροφικό** επειδή διαγράφει τους νέους draft tables. Μην το εκτελείτε χωρίς αναλυτικό rollback plan/backup και ειδική έγκριση.

## 9. QA / Acceptance checklist

- [ ] Build/format/restore/package audit μέσω GitHub CI μετά τις τελευταίες αλλαγές.
- [ ] Unit tests Domain/Application: scope, validation, revision, Owner/Member, cancellation.
- [ ] Απομονωμένο SQL Server 2022 integration: επιπλέον tables, σύνθετα FKs, duplicate, stale-edit, idempotent request, catalog unchanged.
- [ ] SSR: πραγματική HTTPS/antiforgery/authentication, μοναδικά form keys και συνεπείς redirects.
- [ ] Πραγματικό authenticated browser, 2 Tenants, 2 Stores, Owner/Member, duplicate submissions, refresh, 2 sessions.
- [ ] Πραγματική οπτική επιθεώρηση desktop/mobile, Light/Dark, keyboard focus, 200% zoom, scroll/modal/empty/error.
- [ ] Γνήσια screenshots για τα ελαττώματα, επανεκτέλεση fixes και signed acceptance.
- [ ] SQL backup/restore dry-run και ρητή άδεια apply πριν από οποιοδήποτε Development schema mutation.
- [ ] Ο τελικός GitBook οδηγός δημοσιεύεται μόνο μετά από approved PR/real UI screenshots.

## 10. Troubleshooting, πότε χρησιμοποιείται και Best Practices

| Πρόβλημα | Έλεγχος / ενέργεια |
| --- | --- |
| Δεν ανοίγει η οθόνη | Ελέγξτε αν έχετε το feature branch και τη σωστή Dev database migration |
| Κωδικός μενού ήδη υπάρχει | Ο κωδικός είναι μοναδικός μέσα στον Tenant, ανεξαρτήτως case |
| Δεν εμφανίζεται το ERP είδος | Ελέγξτε Tenant, `IsActive`, prefix search και limit 50 |
| Δεν αφαιρείται κατηγορία | Αφαιρέστε πρώτα τα συνδεδεμένα είδη μενού |
| Conflict | Refresh + έλεγχος άλλου Owner, ποτέ υποχρεωτικό overwrite |
| Member δεν βλέπει add/edit | Αναμενόμενο, read-only εξουσιοδότηση |
| UI σφάλμα βάσης | Schema/version mismatch ή σύνδεση SQL, όχι αυτόματο apply |
| Δεν υπάρχουν Publish/Modifiers | Σχεδιασμένα για επόμενα βήματα, όχι ελάττωμα 9.6A.1 |
| Το παλιό μενού έχει ήδη κατηγορίες | Η εφάπαξ εισαγωγή δεν επιτρέπεται: διατηρήστε τις αλλαγές σας και προσθέστε είδη μεμονωμένα |
| Λείπει είδος μετά την αυτόματη εισαγωγή | Ελέγξτε αν το είδος και η ERP κατηγορία του ήταν ενεργά κατά τη δημιουργία και αν ανήκουν στον σωστό Tenant |
| Δημιουργήθηκε νέο ERP είδος αργότερα | Μετά από έγκριση της migration #12, εισάγεται αυτόματα στη συνδεδεμένη ERP κατηγορία του μενού. Παλιές μη συνδεδεμένες κατηγορίες απαιτούν ασφαλές manual/reconciliation review |

**Χρησιμοποιείται** σε Development από εκπαιδευμένους τεχνικούς για review του μελλοντικού Menu Builder. **Δεν χρησιμοποιείται** για production service, online ordering, χρεώσεις, διαβίβαση φορολογικών παραστατικών ή αυτόματη καταχώρηση τιμών.

**Best practices:** κλείδωμα scope στη server ταυτότητα· atomic revision και idempotency· μη διαγραφή ERP master data· μικρή εξάρτηση UI από Application· αναλυτικό audit πριν από περισσότερους publishers· ξεχωριστή αποδοχή για κάθε future phase.
