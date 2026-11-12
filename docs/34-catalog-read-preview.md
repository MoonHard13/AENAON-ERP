# Aenaon ERP — Βήμα 9.4A: Ασφαλής Προεπισκόπηση Καταλόγου (Read-only)

## 1. Σκοπός και σύντομη περιγραφή

Το Aenaon ERP διαθέτει ήδη το Domain `ProductCatalog` (κατηγορίες, προϊόντα, υπηρεσίες) και τους SQL πίνακες `dbo.CatalogCategories`, `dbo.CatalogItems`. Το βήμα 9.4A προσθέτει **την πρώτη πραγματική ανάγνωσή τους**, μέσα από κοινό Application service και ένα αποκλειστικά Development CLI.

Ο στόχος είναι η ασφαλής προετοιμασία για το Blazor Back Office του επόμενου βήματος, χωρίς ακατάλληλα δημόσια API και χωρίς μεταβολή της φορολογικής περιμέτρου. Η προεπισκόπηση:

- Επαληθεύει **πραγματικό username/password** με υπάρχοντα Identity μηχανισμό.
- Ελέγχει **TenantMembership** (δικαίωμα `Read` για Owner ή Member) **πριν** από κάθε SQL query του καταλόγου.
- Υποχρεώνει κάθε SQL query να φιλτράρει βάσει `TenantId`, ανεξάρτητα από την έγκριση του χρήστη.
- Παρέχει **σελίδωση**, **αναζήτηση προθέματος** κωδικού/περιγραφής, και επιλογή εμφάνισης ανενεργών εγγραφών.
- Διακρίνει `Product` από `Service` και εμφανίζει εάν έχει ενεργοποιηθεί η παρακολούθηση αποθέματος.
- Δεν εμφανίζει τιμές, συντελεστές ΦΠΑ, φορολογικές ταξινομήσεις ή στοιχεία συναλλαγών.

{% hint style="warning" %}
**Δεν δημιουργεί / τροποποιεί / διαγράφει είδη ή κατηγορίες.** Δεν περιλαμβάνει ακόμη Blazor CRUD/UI ή δημόσιο API. Οι εργασίες αυτές θα ακολουθήσουν μόνο με πλήρη authorization και validation. Η διάκριση `Product` / `Service` είναι επιχειρησιακή, **όχι** αναγνώριση νόμιμου φορολογικού τύπου.
{% endhint %}

## 2. Αρχιτεκτονική και επαληθεύσιμες εγγυήσεις

| Επίπεδο | Κλάση | Αρμοδιότητα |
| --- | --- | --- |
| Application | `CatalogReadService` | Deny-by-default μέσω `TenantAuthorizationService`, έλεγχος bounds |
| Application | `ICatalogReadStore`, `CatalogReadQuery` | Κοινό συμβόλαιο CLI/μελλοντικού GUI, page 1–1000, page-size 1–100 |
| Infrastructure | `SqlCatalogReadStore` | EF Core `AsNoTracking`, tenant-scoped, parameterized `StartsWith`, deterministic order |
| API CLI | `CatalogPreviewCommand` | Development-only με διαδραστικό password, επιβεβαίωση βάσης και μη ευαίσθητο logging |
| Tests | Application, API, SQL integration | Άρνηση ξένου tenant, κενά GUID, όρια, real SQL paging και αναζήτηση |

**Fail-closed:** Εάν ο χρήστης δεν είναι Owner/Member, το αποτέλεσμα είναι `Denied` και **δεν εκτελείται query καταλόγου**. Το TenantId επιβάλλεται εκ νέου στην SQL. Το password δεν εμφανίζεται στην έξοδο, δεν περνά ως CLI argument και δεν αποθηκεύεται σε logs. Η εφαρμογή δεν χρησιμοποιεί raw SQL string interpolation.

### Συμβατότητα ΑΑΔΕ / IMPACT

Η λειτουργία είναι **μόνο ανάγνωση βασικών στοιχείων καταλόγου**, χωρίς ΦΠΑ, ποσά, πληρωμές, παροχή υπηρεσιών, αποθέματα, παραγγελίες ή παραστατικά. Δεν τροποποιεί τους ήδη ορισμένους φορολογικούς περιορισμούς.

- [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata)
- [IMPACT — Minimum & Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0)
- [Aenaon — Fiscal Compatibility Gate](18-mydata-impact-compatibility.md)

**Καμία** από τις 254 φορολογικές εκκρεμότητες δεν έχει εγκριθεί και κανένα πεδίο τιμολόγησης δεν έχει ενεργοποιηθεί. Η ανάγνωση `TracksStock` **δεν** ενημερώνει την αποθήκη.

## 3. Προϋποθέσεις

1. Windows και .NET SDK όπως ορίζεται στο `global.json`.
2. Repository με το συγχρονισμένο `main` μετά το Squash and merge.
3. SQL Server 2022 Development βάση `AenaonERP_Dev` με όλες τις υπάρχουσες migrations.
4. Πραγματικός χρήστης Identity με `Owner` ή `Member` συμμετοχή στην επιλεγμένη επιχείρηση.
5. Διαδραστικό PowerShell/VS Code terminal, χωρίς redirection του stdin.
6. Ενεργά User Secrets σύνδεσης SQL. **Μην βάζεις κωδικούς στο Git**.

## 4. Εγκατάσταση και επιβεβαίωση

Από PowerShell:

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git switch main
git pull --ff-only origin main

dotnet --version
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

{% hint style="info" %}
Δεν απαιτείται **νέα SQL migration**. Οι πίνακες προϋπήρχαν ήδη και δεν δημιουργούνται seed δεδομένα αυτόματα. Εάν δεν έχεις καταχωρίσει κατηγορίες/είδη, η προεπισκόπηση θα δείξει σωστά **0 αποτελέσματα**.
{% endhint %}

## 5. CLI — κατηγορίες και είδη

Αντικατάστησε το `TENANT_GUID` με το πραγματικό αναγνωριστικό της δικής σου επιχείρησης.

**Κατηγορίες:**

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --preview-catalog --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --entity categories
```

**Είδη και υπηρεσίες:**

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --preview-catalog --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --entity items
```

Η εφαρμογή εμφανίζει `Κωδικός λογαριασμού:` και διαβάζει τον κωδικό με ασφάλεια, χωρίς echo ή καταχώρισή του στην εντολή. Ο λογαριασμός πρέπει να είναι μέλος του συγκεκριμένου Tenant.

### Αναζήτηση, σελίδωση και ανενεργά

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --preview-catalog --username admin --tenant-id "TENANT_GUID" --confirm-database AenaonERP_Dev --entity items --search SERV --page 1 --page-size 25 --include-inactive
```

| Παράμετρος | Επεξήγηση |
| --- | --- |
| `--username` | Όνομα επαληθευμένου χρήστη Identity |
| `--tenant-id` | Επιλεγμένη επιχείρηση (GUID) |
| `--confirm-database` | Ακριβές όνομα βάσης, π.χ. `AenaonERP_Dev` |
| `--entity` | `categories` ή `items` |
| `--search` | Προαιρετικό **πρόθεμα** κωδικού ή ονόματος, έως 80 χαρακτήρες |
| `--page` | Από 1 έως 1000, προεπιλογή 1 |
| `--page-size` | Από 1 έως 100, προεπιλογή 25 |
| `--include-inactive` | Προαιρετική σημαία εμφάνισης και ανενεργών εγγραφών |

Το `HasMore` εκτιμάται με ανάκτηση **μίας επιπλέον εγγραφής** στη σελίδα, χωρίς δεύτερο `COUNT(*)`. Η ταξινόμηση γίνεται με `Code` και `Id` ώστε να έχει σταθερή σειρά μέσα στην ίδια στιγμή ανάγνωσης.

### Ενδεικτική έξοδος

```text
CATALOG PREVIEW — items
Σελίδα: 1 | Μέγεθος: 25 | Εγγραφές: 0 | Επόμενη σελίδα: Όχι
Δεν υπάρχουν καταχωρημένα δεδομένα με αυτά τα φίλτρα.
Μόνο προβολή. Δεν έγιναν αλλαγές σε είδη, κατηγορίες, ΦΠΑ ή παραστατικά.
```

## 6. Δικαιώματα και περιορισμοί

- `Owner`: προεπισκόπηση του δικού του Tenant.
- `Member`: προεπισκόπηση του δικού του Tenant (δικαίωμα `Read`).
- Άγνωστος χρήστης ή μη μέλος: **απόρριψη** χωρίς ανάγνωση καταλόγου.
- Ξένο TenantId: **απόρριψη** ανεξάρτητα από το αν γνωρίζεις κάποιο αναγνωριστικό.
- Η εισαγωγή `--username` **δεν αποδεικνύει από μόνη της** ταυτότητα· απαιτείται επαλήθευση password και membership.
- Η ανάγνωση δεν κάνει συναλλαγή αποθήκευσης ειδών. Ο έλεγχος λογαριασμού Identity **μπορεί να ενημερώσει μετρητές lockout** σε αποτυχημένες προσπάθειες· γι' αυτό δεν ισχυριζόμαστε ότι η συνολική CLI λειτουργία δεν έχει καμία εγγραφή στη SQL.

Η υλοποίηση δεν χρησιμοποιεί store-specific άδειες, επειδή το υπάρχον domain έχει μόνο Tenant memberships. Η προεπισκόπηση αφορά **ολόκληρο τον κατάλογο του Tenant**, όχι ξεχωριστό κατάστημα.

## 7. Troubleshooting

| Σύμπτωμα | Αντιμετώπιση |
| --- | --- |
| `Η βάση δεν είναι ενημερωμένη...` | Έλεγξε database name και `--migrate-database --dry-run`. Μην κάνεις migrate αυτόματα |
| `Η επαλήθευση λογαριασμού απέτυχε` | Έλεγξε το username/password στο Development. Μην επαναλαμβάνεις συνεχώς εσφαλμένους κωδικούς λόγω lockout |
| `Δεν επιτρέπεται πρόσβαση...` | Εξέτασε το TenantMembership. Δεν υποστηρίζεται προσπέλαση ξένου Tenant |
| `Δεν υπάρχουν καταχωρημένα δεδομένα...` | Οι πίνακες ίσως είναι κενoί ή το `--search` δεν ταιριάζει στο πρόθεμα |
| `Μη έγκυρη σελίδα...` | Έλεγξε page 1–1000, size 1–100 και αναζήτηση έως 80 |
| `Απαιτείται διαδραστικό τερματικό` | Εκτέλεσε την εντολή κανονικά στο PowerShell, όχι ως redirected CI process |

Οι κωδικοί εξόδου είναι `0` επιτυχής προεπισκόπηση (ακόμη και κενή), `2` άκυρα ορίσματα/βάση/κωδικός, `3` άρνηση πρόσβασης, `130` ακύρωση.

## 8. Logs, αντίγραφα ασφαλείας και rollback

Τα structured logs περιέχουν μόνο το είδος προβολής (`categories/items`), page και page-size ή γενικά συμβάντα απόρριψης, **όχι** password ή στοιχεία σύνδεσης SQL.

- Χωρίς νέα migration, seed, καταχώρηση τιμών ή CRUD.
- Δεν απαιτείται backup **για αυτήν την ανάγνωση**, όμως η υπάρχουσα βάση Development πρέπει να καλύπτεται από τη συνήθη πολιτική backup πριν από οποιαδήποτε μελλοντική εγγραφή/αναβάθμιση.
- Για rollback: Git revert του PR και νέο build. Δεν υπάρχει schema migration προς αναστροφή.

## 9. Checklist αποδοχής

- [ ] Επιτυχής locked restore, formatting και Release build.
- [ ] Πέρασαν Unit tests Application και API (authentication, deny-by-default, όρια).
- [ ] Πέρασαν SQL Server 2022 integration tests με 2 ξεχωριστούς Tenants.
- [ ] Κανένα query catalog δεν εκτελείται πριν από επιτυχή authorization.
- [ ] Δεν επιστρέφονται εγγραφές άλλης επιχείρησης, ακόμη και με ίδιο κωδικό.
- [ ] Η σελίδωση, το `HasMore`, το search και το `IncludeInactive` λειτουργούν.
- [ ] Δεν άλλαξαν στοιχεία ΦΠΑ, φορολογικά contracts ή οι 254 Pending παρατηρήσεις.
- [ ] Χωρίς migrations, φορολογική έκδοση, Provider ή ΑΑΔΕ request.

## 10. Επόμενο στάδιο

**9.4B — Blazor Catalog Read-only Back Office:** θα χρησιμοποιήσει το **ίδιο `CatalogReadService`** με secure cookie session και active Tenant, για ολοκληρωμένο responsive interface κατηγοριών/ειδών, φίλτρα, σελίδωση, κατάσταση loading/empty/error, δικαιώματα Owner/Member και keyboard accessibility. Η δυνατότητα δημιουργίας/επεξεργασίας θα ακολουθήσει αργότερα σε ξεχωριστό ασφαλές στάδιο, με επιβεβαίωση myDATA/IMPACT όπου απαιτείται.
