# AENAON ERP — 9.5C.3: Ακεραιότητα τιμών και ιστορικό μεταβολών

## Σκοπός
Μόνιμη, εξουσιοδοτημένη αποτύπωση κάθε πραγματικής αλλαγής εμπορικής τιμής ή πολιτικής τιμοκαταλόγου, προστασία από νέες συγκρούσεις ισόβαθμων ενεργών τιμών και ασφαλής διαχείριση ταυτόχρονων ενημερώσεων σε SQL Server.

{% hint style="warning" %}
**Development-only.** Δεν υλοποιεί έκδοση παραστατικών, υπολογισμό ΦΠΑ, διαβίβαση myDATA/IMPACT, POS, Android εφαρμογή ή αυτόματη φορολογική αντιστοίχιση. Οι ποσότητες παραμένουν εμπορικές τιμές με έως τέσσερα δεκαδικά και σε EUR. Τα ιστορικά γεγονότα αρχίζουν **μετά** την εφαρμογή της νέας migration· δεν δημιουργούνται αναδρομικά ψευδή συμβάντα για προηγούμενες αλλαγές.
{% endhint %}

## Σύντομη περιγραφή
- **Append-only audit:** δημιουργία/αλλαγή εμπορικής πολιτικής, ενεργοποίηση/απενεργοποίηση, προσθήκη/αλλαγή/αφαίρεση τιμής. Καταγράφονται Tenant, τιμοκατάλογος, προαιρετικό είδος, actor GUID, UTC timestamp, προέλευση (Web/CLI/System) και πριν/μετά. Δεν καταγράφονται κωδικοί πρόσβασης ή session tokens.
- **Ατομικότητα:** η εγγραφή ιστορικού και η επιχειρησιακή αλλαγή αποθηκεύονται μαζί στο ίδιο `Serializable` SQL transaction. Αν αποτύχει η ενέργεια, δεν παραμένει μερικό audit.
- **Ιστορικό:** ασφαλής ανάγνωση από τη φόρμα τιμοκαταλόγου και από CLI, με σελιδοποίηση, μόνο για τον επιλεγμένο Tenant.
- **Πρόληψη overlaps:** πριν από εφαρμογή αλλαγής σε ενεργό τιμοκατάλογο ή τιμή εντοπίζονται άλλοι ενεργοί τιμοκατάλογοι με **ίδιο κατάστημα (ή αμφότεροι γενικοί), ίδιο κανάλι, κοινό τιμολογημένο είδος και έστω μία κοινή ημερομηνία**. Η νέα καταχώρηση απορρίπτεται με `pricelist.overlap.conflict` και παραμένει η προηγούμενη κατάσταση. Τα νόμιμα fallback διαφορετικού scope/rank δεν αποκλείονται.
- **Concurrency:** διατηρούνται η σύγκριση αναμενόμενης αρχικής τιμής και εμπορικού προφίλ, το `Serializable` transaction και επιπλέον ανά Tenant αποκλειστικό application lock για συγχρονισμό τιμολογιακών εγγραφών.

## Προαπαιτούμενα
Εγκατεστημένο .NET SDK σύμφωνα με `global.json`, SQL Server 2022, λειτουργικό Development περιβάλλον AENAON, backup της `AenaonERP_Dev`, εξουσιοδοτημένος Owner για εγγραφή ή χρήστης με Read για εμφάνιση ιστορικού. Η προηγούμενη migration `20261114151000_CatalogItemDetails` πρέπει ήδη να έχει εφαρμοστεί.

## Εγκατάσταση και έλεγχος migration

```powershell
git switch main
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Αν ο χρήστης έχει τοπικά **9 εφαρμοσμένες migrations**, αναμένονται **9 applied / 1 pending**, η `20261114163000_PriceListAuditHistory`. Ελέγξτε πρώτα το παραγόμενο SQL script και επαληθεύστε το τελευταίο backup. Εφαρμόστε **μόνο μετά από review/merge και έγκριση**:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --apply --confirm-database AenaonERP_Dev
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Μετά την επιτυχή εφαρμογή αναμένονται **10 applied / 0 pending**. Το SQL schema προσθέτει μόνο τον πίνακα `dbo.PriceListAuditEntries`, ευρετήριο Tenant/τιμοκαταλόγου/UTC και trigger αποτροπής UPDATE/DELETE. **Καμία παλιά τιμή δεν αλλάζει**.

{% hint style="danger" %}
Το `Down` της migration διαγράφει οριστικά το νέο ιστορικό. Μην εφαρμόζετε rollback migration σε βάση που έχει ήδη καταγράψει επιχειρησιακές αλλαγές. Για ανάκτηση αξιοποιήστε δοκιμασμένο backup και συντονισμένη διαδικασία DBA· μην απενεργοποιείτε το audit trigger.
{% endhint %}

## Χρήση στο Blazor
1. Συνδεθείτε από το HTTPS Back Office και ανοίξτε **Τιμοκατάλογοι**.
2. Ανοίξτε υπάρχοντα τιμοκατάλογο από **Επεξεργασία**. Στην ίδια σελίδα υπάρχει ενότητα **Ιστορικό αλλαγών τιμοκαταλόγου**, μόνο για ανάγνωση, με UTC ώρα, γεγονός, είδος όπου υπάρχει, παλιά/νέα τιμή ή πολιτική, χρήστη και προέλευση.
3. Χρησιμοποιήστε τους συνδέσμους **Προηγούμενη / Επόμενη** για αλλαγή σελίδας ιστορικού. Η προβολή τιμών εξακολουθεί να έχει ανεξάρτητη σελιδοποίηση.
4. Καταχωρίστε/αλλάξτε μία τιμή. Εφόσον επιτύχει, θα εμφανιστεί νέα εγγραφή ιστορικού. Η επανάληψη **ίδιου ποσού** δεν δημιουργεί ψεύτικο audit event.
5. Αν εμφανιστεί μήνυμα **επικάλυψης**, ελέγξτε τις ημερομηνίες ισχύος, το κατάστημα, το κανάλι και τα κοινά είδη των ενεργών τιμοκαταλόγων. **Μη** δημιουργείτε νέους τιμοκαταλόγους με διαφορετικό κωδικό για παράκαμψη της προειδοποίησης.

## Χρήση σε CLI / PowerShell
Η ανάγνωση ιστορικού είναι `--dry-run`, όπως και οι άλλες read-only εντολές:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --manage-price-lists --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --action history --id "PRICE_LIST_GUID" --page 1 --dry-run
```

Οι υφιστάμενες λειτουργίες `create`, `update`, `activate`, `deactivate`, `prices`, `remove-price` παραμένουν διαθέσιμες με `--apply` για εγγραφή ή `--dry-run` για προεπισκόπηση. Στην παραγωγή γεγονότων το CLI καταγράφει προέλευση `CLI`; η φόρμα `Web`.

## Επεξήγηση πεδίων ιστορικού
| Πεδίο | Έννοια |
| --- | --- |
| Χρόνος UTC | Η ακριβής στιγμή καταγραφής στον server. Δεν υποκαθιστά την ημερομηνία εμπορικής ισχύος. |
| Action | CreateList, UpdateList, ActivateList, DeactivateList, AddPrice, UpdatePrice, RemovePrice. |
| Είδος | Στα συμβάντα τιμών: αναγνωριστικό και κωδικός προϊόντος/υπηρεσίας, όπου υπάρχει ακόμη. |
| Before/After Price | Ποσό πριν και μετά, με τετραψήφια ακρίβεια· κενό σε προσθήκη ή αφαίρεση αντίστοιχα. |
| Before/After Policy | Στιγμιότυπο εμπορικού προφίλ: κωδικός, όνομα, κατάστημα, κανάλι, βάση ΦΠΑ, ισχύς και ενεργή κατάσταση. |
| ActorUserId / Source | Αναγνωριστικό του λογαριασμού και προέλευση Web, CLI ή System. |

## Πότε χρησιμοποιείται / δεν χρησιμοποιείται
Χρησιμοποιείται για εσωτερικό έλεγχο τιμών, διαγνωστική έρευνα αλλαγών, QA και επιβεβαίωση εμπορικής πολιτικής. Δεν αποτελεί λογιστικό καθολικό, πιστοποιημένο φορολογικό ημερολόγιο ή ιστορικό τελικών τιμών παραγγελιών. Το αμετάβλητο snapshot γραμμής παραγγελίας είναι **επόμενο ανεξάρτητο contract** του 9.5C.4.

## Έλεγχοι πριν και μετά
**Πριν:** τρέχον Tenant/Store, σωστά δικαιώματα, ενεργοί/ανενεργοί τιμοκατάλογοι, αρχικές τιμές, πραγματική ημερομηνία ισχύος, αξιόπιστο backup. **Μετά:** επιβεβαιώστε saved price/profile, μία σχετική νέα εγγραφή audit, μηδενικές εκκρεμείς migrations και ανεπηρέαστους άλλους Tenants. Επαναλάβετε δοκιμή με απορριφθείσα τιμή: δεν πρέπει να αυξηθεί το audit.

## Troubleshooting
| Σύμπτωμα | Ενέργεια |
| --- | --- |
| `overlap.conflict` | Βρείτε άλλο ενεργό price list ίδιου scope/channel που τιμολογεί κοινό είδος σε επικαλυπτόμενη ημερομηνία. Χρησιμοποιήστε διακριτές περιόδους ή scope και επανελέγξτε από Price Preview. |
| `price.conflict` ή `profile.conflict` | Επαναφορτώστε την καρτέλα. Μην αντικαθιστάτε την τιμή από παλιά φόρμα. |
| Κενό ιστορικό | Η παλιά περίοδος δεν έγινε backfill. Βεβαιωθείτε ότι εφαρμόστηκε η 10η migration και ότι η ενέργεια ήταν πραγματική μεταβολή. |
| SQL error στην εφαρμογή migration | Ελέγξτε permissions και SQL logs, χωρίς επανάληψη καταστροφικών εντολών ή επέμβαση στο audit trigger. |
| Πρόσβαση denied | Επιβεβαιώστε membership και tenant context. Ο Member μπορεί να διαβάζει, όχι να αλλάζει τιμές. |

## Ασφάλεια / συντήρηση / best practices
- Μη δίνετε γενική δυνατότητα UPDATE/DELETE στο `dbo.PriceListAuditEntries`. Το trigger προστατεύει από τυχαίες μεταβολές αλλά DBA με DDL δικαιώματα μπορεί να το αλλάξει.
- Η αναζήτηση και όλες οι ενέργειες πρέπει να περιορίζονται σε `TenantId`. Η ταυτότητα χρήστη παράγεται από έγκυρη session/CLI επαλήθευση και **όχι** από μη έμπιστο form field.
- Διατηρήστε backup/retention plan για τον αυξανόμενο ιστορικό πίνακα, αλλά **μην αυτοματοποιήσετε deletion** χωρίς εγκεκριμένη πολιτική διατήρησης και νομικό έλεγχο.
- Ελέγχετε την προτεραιότητα επιλογής τιμής (Store + Channel → Store + All → All + Channel → All + All) και τη fail-closed ένδειξη `Ambiguous`. Η ανίχνευση overlaps **δεν** τροποποιεί το read-time resolution.

## Checklist αποδοχής
- [ ] 10 migrations applied, 0 pending και νέο table+trigger με επαληθευμένο backup.
- [ ] Audit για δημιουργία/μεταβολή/απενεργοποίηση/ενεργοποίηση λίστας και προσθήκη/αλλαγή/αφαίρεση τιμής.
- [ ] Μηδενικό audit σε αποτυχία/duplicate/conflict/ίδια τιμή και ατομικές εγγραφές.
- [ ] Ασφαλής ανάγνωση για Owner/Member και αδυναμία cross-tenant leakage.
- [ ] Αποτροπή overlapping τιμών του ίδιου rank, επιτρεπτό fallback διαφορετικής βαθμίδας.
- [ ] Δύο ταυτόχρονες αλλαγές δεν προκαλούν διπλή/σιωπηρή αποθήκευση.
- [ ] Blazor, CLI, desktop/mobile, light/dark, keyboard controls και automated CI green.

## Επόμενο βήμα
**9.5C.4 — Pricing Scopes & Commercial Readiness:** κανόνες customer groups, τεκμηριωμένη προτεραιότητα και ανεξάρτητο immutable order-line price snapshot contract. Οι εισαγωγές Excel και οι γενικές μαζικές εργασίες παραμένουν αναβλημένες.

## Ανεξάρτητη ανασκόπηση αποδοχής — 15/11/2026

Η ανασκόπηση βασίζεται στο GitHub `main` commit `724bf341020f107fa66e497c55b55f0b560ad245`, μετά τα merged PR #53–#56. Ελέγχθηκαν οι πραγματικές κλάσεις Domain/Application/Infrastructure, η 10η EF migration, το SQL audit trigger, τα UI/CLI boundaries και τα υπάρχοντα integration tests. **Ο έλεγχος κώδικα δεν αποδεικνύει τη σημερινή κατάσταση της τοπικής `AenaonERP_Dev`.**

| Acceptance gate | Διασταύρωση / εκκρεμότητα |
| --- | --- |
| Αρχιτεκτονική / απομόνωση | Tenant-scoped ιστορικό και σύνθετο FK `(TenantId, PriceListId)`. Απαιτούνται πραγματικά role tests στην τοπική εγκατάσταση. |
| SQL / ιστορικό | Additive `PriceListAuditEntries`, immutable trigger και transactional writes ελέγχθηκαν σε source review. Το `Down` καταστρέφει το ιστορικό και **δεν** εγκρίνεται ως ασφαλές rollback. |
| Overlap / concurrency | `Serializable` + tenant SQL application lock + optimistic price/profile checks. Προστέθηκε regression test με **δύο ανεξάρτητα DbContexts/SQL connections** που γράφουν ταυτόχρονα τιμή ίδιου rank, καθώς και έλεγχος απαγόρευσης SQL `DELETE` στο audit. Η επιτυχία τους πρέπει να επιβεβαιωθεί από το CI του νέου PR. |
| Αυτόματοι έλεγχοι | Το GitHub `main` CI [#43617602547](https://github.com/MoonHard13/AENAON-ERP/actions/runs/43617602547) ολοκληρώθηκε με επιτυχία σε Ubuntu/Windows και με isolated SQL Server 2022. Δεν αντικαθιστά τους νέους regression tests ή τοπικό acceptance. |
| Εγκατάσταση | Ο χειριστής επιβεβαίωσε με SSMS 10 applied / 0 pending στην `AenaonERP_Dev`, ενεργό trigger, 4 trusted constraints και index. Δεν απαιτείται επανάληψη migration. Για άλλη βάση: preview, script review, backup και έγκριση πριν από apply. |
| Λειτουργικός έλεγχος | Επιβεβαιώθηκαν από πραγματικά Web audit rows create/add/update/deactivate/activate/remove. Ο χειριστής αναφέρει επιτυχές human-style testing· η αναλυτική αναφορά/screenshots των δύο καρτελών, Owner/Member, CLI, responsive, Light/Dark και keyboard-only δεν είναι διαθέσιμα για ανεξάρτητη επαλήθευση εδώ. |

### Πρωτόκολλο αποδοχής χωρίς αυτόματες εγγραφές

1. Συγχρονίστε το εγκεκριμένο `main` και ελέγξτε `dotnet --list-sdks`, `git status`, `dotnet restore AenaonERP.slnx --locked-mode`.
2. Ελέγξτε `dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run`. Επιβεβαιώστε τη **σωστή** βάση και τις πραγματικές εφαρμοσμένες/εκκρεμείς migrations. Το preview δεν πραγματοποιεί αλλαγές.
3. Ελέγξτε τον πραγματικό SQL script της 10ης migration, τον στόχο `dbo.PriceListAuditEntries`, σύνθετο FK, index, checks, `CREATE TRIGGER` και `Down`, καθώς και permissions/backup. Απαγορεύεται να θεωρηθεί το static source review ισοδύναμο με έλεγχο του παραγόμενου script.
4. Για περιβάλλον με εκκρεμή migration, μόνο με **ξεχωριστή ρητή έγκριση** και επιβεβαιωμένο backup επιτρέπεται το documented `--migrate-database --apply --confirm-database TARGET_DB`. Η `AenaonERP_Dev` έχει ήδη 10 applied / 0 pending, επομένως **δεν** εκτελούμε ξανά apply ως μέρος του review.
5. Επιβεβαιώστε μετά το apply `10 applied / 0 pending`, table, index, trigger και συνέπεια προηγούμενων τιμών. Χρησιμοποιήστε **απομονωμένα δοκιμαστικά** δεδομένα και αποδεκτό tenant για τις λειτουργικές δοκιμές, όχι παραγωγικά δεδομένα.
6. Ελέγξτε ένα νέο event για κάθε επιτυχή αλλαγή, κανένα event για no-op/απόρριψη, σωστό actor/source/UTC, rollback ολόκληρης αποτυχημένης συναλλαγής και αδυναμία cross-tenant ανάγνωσης/εγγραφής.
7. Επαληθεύστε οπτικά το πραγματικό Blazor και CLI. Κρατήστε screenshots/αποτέλεσμα/ημερομηνία ανά viewport και keyboard-only σενάριο. Ο έλεγχος με συνθετικά HTML/CSS fixtures **δεν** αντικαθιστά το πραγματικό UI.
8. Καταγράψτε επιτυχία ή blocker σε κάθε gate. Η τελική σήμανση είναι **«υλοποιημένο, όχι πλήρως αποδεκτό»** μέχρι να κλείσουν όλα τα παραπάνω.

{% hint style="warning" %}
Η αλλαγή μόνο του κώδικα εφαρμογής **δεν επαναφέρει** τη SQL migration. Μετά από apply, ο πίνακας ιστορικού περιέχει πραγματικές εγγραφές που θα χαθούν με `Down`. Για rollback απαιτείται εγκεκριμένο σχέδιο αποκατάστασης και δοκιμασμένο backup· δεν εγκρίνεται αυτόματη διαγραφή του audit.
{% endhint %}
