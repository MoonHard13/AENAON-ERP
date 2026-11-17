# AENAON ERP — Manual 61: 9.6A.1 Draft Menu Management

**Κατάσταση:** Προτεινόμενη υλοποίηση σε **Draft PR #62**, όχι στο `main`. Δεν αποτελεί επιβεβαιωμένο διαθέσιμο χαρακτηριστικό ούτε οδηγία εγκατάστασης σε production.  
**Έκδοση baseline:** `main` μετά το merged PR #61, commit `374b04b6f67ae34d98ac03ddab7be85ff9d460d9`.  
**Πηγή απαιτήσεων:** [Manual 58 — Menu Architecture](58-menu-management-architecture.md), [Manual 60 — Approved Design](60-menu-management-final-design-review.md).

{% hint style="warning" %}
Το 9.6A.1 είναι **Draft-only**. Δεν προσφέρει Publish, Preview, modifiers, διαθεσιμότητα, POS, KDS, φορολογική έκδοση, IMPACT transmission, offline ή εκτύπωση. Η προτεινόμενη ενδέκατη migration βρίσκεται στον κώδικα του PR #62 αλλά **δεν εφαρμόστηκε στη δική σας SQL Server**. Μη χρησιμοποιήσετε `--apply` πριν από SQL review, επιβεβαίωση στόχου, backup και χωριστή έγκριση.
{% endhint %}

## 1. Σκοπός

Το **Draft Menu Management** επιτρέπει στον Owner μίας επιχείρησης να συνθέτει ανεξάρτητα μενού από ενεργά είδη του ήδη υπάρχοντος ERP Catalog. Υποστηρίζει πολλαπλά μενού σε ίδια επιχείρηση, κατηγορίες παρουσίασης και σύνδεση ειδών χωρίς νέα τιμή πώλησης. Ο Member μπορεί μόνο να διαβάζει.

Το `Menu` δεν είναι `CatalogCategory`, ενώ η αφαίρεση ενός `MenuDraftItem` **δεν** αφαιρεί το `CatalogItem` της επιχείρησης.

## 2. Προϋποθέσεις

- Απομονωμένο περιβάλλον Development με .NET SDK από `global.json`, SQL Server 2022, GitHub `main` ενημερωμένο και έγκυρη συνεδρία Owner/Member.
- Εξουσιοδοτημένος Tenant και το επιλεγμένο Store του ίδιου Tenant.
- Επιτυχημένο review του PR #62 και **εγκεκριμένη** αλλαγή SQL πριν από οποιαδήποτε χρήση write forms. Χωρίς την ενδέκατη migration οι σελίδες μπορεί να εμφανίζουν ασφαλές σφάλμα βάσης.
- Ανενεργά είδη ERP δεν μπορούν να προστεθούν σε νέο προσχέδιο. Αν είδος απενεργοποιηθεί μεταγενέστερα, εμφανίζεται σχετική προειδοποίηση έως να αφαιρεθεί.

## 3. Διαθέσιμες στην προτεινόμενη υλοποίηση λειτουργίες

| Ενέργεια | Owner | Member | Σημειώσεις |
| --- | --- | --- | --- |
| Λίστα/επιλογή μενού | Ναι | Ναι | Tenant scoped |
| Δημιουργία νέου μενού | Ναι | Όχι | Μοναδικός κωδικός ανά Tenant |
| Μετονομασία | Ναι | Όχι | Δεν αλλάζει code ή Tenant |
| Προσθήκη/επεξεργασία/αφαίρεση άδειας κατηγορίας | Ναι | Όχι | Δεν επιτρέπεται διαγραφή κατηγορίας με είδη |
| Αλλαγή θέσης κατηγορίας | Ναι | Όχι | Κουμπιά ↑/↓ και αναθεώρηση |
| Αναζήτηση ενεργών ERP ειδών | Ναι | Ναι | Prefix code/name, έως 50 αποτελέσματα |
| Προσθήκη/αφαίρεση συνδεδεμένου είδους | Ναι | Όχι | FK στο ήδη υπάρχον CatalogItem |
| Προαιρετικό όνομα εμφάνισης | Ναι | Όχι | Δεν αλλάζει ERP περιγραφή |
| Αλλαγή θέσης είδους | Ναι | Όχι | Αναδιάταξη μέσα στην ίδια κατηγορία |
| Publish/Preview/Modifiers/Price override | Όχι | Όχι | **Εκτός 9.6A.1** |

## 4. Οθόνη Desktop και Mobile

Desktop: sidebar και topbar AENAON, επιλογή μενού και τρεις στήλες: **Κατηγορίες μενού**, **Είδη μενού**, **Κατάλογος ERP**. Εμφανίζεται ένδειξη `DRAFT ONLY`.

Mobile κάτω από 1024px: τρεις **εναλλασσόμενες καρτέλες** αντί να συμπιέζονται οι στήλες. Η επιλεγμένη κατηγορία παραμένει στο URL. Απαιτείται πραγματικός οπτικός έλεγχος στα 320, 375, 390, 768, 1366 και 1920px, Light/Dark και 200% zoom πριν από acceptance.

Οι συντομεύσεις που προτείνονται: `Alt+M` για επιλογή μενού (ανάλογα με OS/browser), `Alt+F` για αναζήτηση είδους, `Ctrl+S` για υποβολή έγκυρης φόρμας μόνο όταν το focus βρίσκεται ήδη μέσα σε αυτή. ↑/↓ στα εμφανή κουμπιά λειτουργούν και με keyboard. Το drag-and-drop **δεν** απαιτείται για το πρώτο slice.

## 5. Βήματα εργασίας (μόνο μετά το approved Dev setup)

1. Συνδεθείτε με Owner, επιλέξτε σωστό Tenant/Store και μεταβείτε στην επιλογή **Μενού εστίασης**.
2. Πατήστε **Νέο μενού**, συμπληρώστε Code και Name, δημιουργήστε draft.
3. Επιλέξτε το μενού, προσθέστε παρουσίασης κατηγορίες όπως «Ορεκτικά», «Κυρίως», «Ποτά».
4. Επιλέξτε κατηγορία και στο **Κατάλογος ERP** αναζητήστε ενεργό είδος με code ή όνομα. Πατήστε **+**.
5. Αλλάξτε θέση με ↑/↓, ορίστε εναλλακτικό όνομα εμφάνισης ή αφαιρέστε είδος από το μενού.
6. Για αλλαγή κατηγορίας, χρησιμοποιήστε την **Επεξεργασία**. Για αφαίρεση, αφαιρέστε πρώτα όλα τα συνδεδεμένα είδη.
7. Σε περίπτωση **Conflict** από δεύτερο Owner, κάντε refresh, ελέγξτε την τρέχουσα revision και ξαναδοκιμάστε. Δεν αντικαθιστούμε τις αλλαγές άλλου χρήστη.

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

**Χρησιμοποιείται** σε Development από εκπαιδευμένους τεχνικούς για review του μελλοντικού Menu Builder. **Δεν χρησιμοποιείται** για production service, online ordering, χρεώσεις, διαβίβαση φορολογικών παραστατικών ή αυτόματη καταχώρηση τιμών.

**Best practices:** κλείδωμα scope στη server ταυτότητα· atomic revision και idempotency· μη διαγραφή ERP master data· μικρή εξάρτηση UI από Application· αναλυτικό audit πριν από περισσότερους publishers· ξεχωριστή αποδοχή για κάθε future phase.
