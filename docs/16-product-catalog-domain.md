# Aenaon ERP — Βήμα 9.1: Product Catalog Foundation

## 1. Σκοπός

Ορίζεται το πρώτο **επιχειρησιακό Domain** του Aenaon ERP: κατηγορίες, εμπορεύσιμα προϊόντα και υπηρεσίες. Κάθε κατάλογος ανήκει **αποκλειστικά σε μία επιχείρηση** (Tenant).

{% hint style="info" %}
Το Βήμα 9.1 περιλαμβάνει μοντέλα και αυτοματοποιημένα tests. **Δεν δημιουργεί πίνακες SQL, δεν εμφανίζει ακόμη νέα οθόνη και δεν διαθέτει CLI καταχώρησης ειδών**. Η πραγματική αποθήκευση θα υλοποιηθεί στο Βήμα 9.2.
{% endhint %}

## 2. Προϋποθέσεις

- Ενημερωμένο Git repository Aenaon ERP στο αντίστοιχο feature branch.
- Συμβατό .NET 10 SDK σύμφωνα με το `global.json`.
- VS Code ή Visual Studio με πρόσβαση στη solution `AenaonERP.slnx`.
- Δεν απαιτείται SQL Server για τα tests του συγκεκριμένου βήματος.

## 3. Δομή

| Αρχείο | Ευθύνη |
| --- | --- |
| `src/Aenaon.Domain/Catalog/ProductCatalog.cs` | Όριο επιχείρησης, συλλογές, μοναδικότητα, συντονισμός αλλαγών. |
| `CatalogCategory.cs` | Κατηγορία, κωδικός, περιγραφή, ενεργή/ανενεργή κατάσταση. |
| `CatalogItem.cs` | Προϊόν/υπηρεσία, κωδικός, περιγραφή, κατηγορία, απόθεμα, κατάσταση. |
| `CatalogItemKind.cs` | Σταθεροί τύποι `Product` και `Service`. |
| `CatalogText.cs` | Κοινοί κανόνες επικύρωσης κωδικών και ονομάτων. |
| `tests/Aenaon.Domain.Tests/ProductCatalogTests.cs` | Έλεγχοι δημιουργίας, τροποποίησης, απομόνωσης και αποτυχιών. |

## 4. Επιχειρησιακοί κανόνες

1. Το `ProductCatalog` δημιουργείται με μη κενό `TenantId`. Δεν επιτρέπεται αλλαγή ιδιοκτησίας.
2. Κάθε κατηγορία έχει μοναδικό κωδικό **μέσα στον ίδιο κατάλογο**. Οι κωδικοί κανονικοποιούνται σε κεφαλαία χωρίς περιττά κενά.
3. Κάθε είδος ή υπηρεσία έχει μοναδικό κωδικό **ανά επιχείρηση**, ανεξάρτητα από κατηγορία και τύπο.
4. Κατά τη δημιουργία είδους απαιτείται ενεργή κατηγορία του ίδιου καταλόγου. Άγνωστη ή ξένη κατηγορία απορρίπτεται.
5. Οι υπηρεσίες (`Service`) **δεν μπορούν** να έχουν `TracksStock = true`.
6. Τα προϊόντα (`Product`) μπορούν να έχουν `TracksStock = true` ή `false`. Η πραγματική κίνηση αποθήκης δεν υλοποιείται ακόμη.
7. Τα νέα είδη και οι νέες κατηγορίες ξεκινούν ενεργά.
8. Η απενεργοποίηση κατηγορίας **δεν αλλάζει αυτόματα** την κατάσταση υφιστάμενων ειδών. Απλώς αποτρέπει νέες καταχωρήσεις και μετακινήσεις σε αυτήν.
9. Οι συλλογές εκτίθενται μόνο για ανάγνωση. Όλες οι αλλαγές εκτελούνται μέσω μεθόδων του `ProductCatalog`.
10. Αποτυχημένες προσπάθειες προσθήκης ή αλλαγής δεν τροποποιούν τις συλλογές.

### Όρια πεδίων

| Πεδίο | Όριο | Περιεχόμενο |
| --- | --- | --- |
| Κωδικός κατηγορίας | 1–30 | Γράμματα, αριθμοί, `-`, `_`. |
| Όνομα κατηγορίας | 1–160 | Κείμενο χωρίς χαρακτήρες ελέγχου. |
| Κωδικός είδους | 1–40 | Γράμματα, αριθμοί, `-`, `_`. |
| Περιγραφή είδους | 1–200 | Κείμενο χωρίς χαρακτήρες ελέγχου. |

## 5. Παράδειγμα χρήσης στον κώδικα

```csharp
using Aenaon.Domain.Catalog;

Guid tenantId = Guid.NewGuid();
var catalog = ProductCatalog.Create(tenantId);

var food = catalog.AddCategory("FOOD", "Φαγητά");
var services = catalog.AddCategory("SERVICES", "Υπηρεσίες");

var pizza = catalog.AddItem(food.Id, "PIZZA01", "Μαργαρίτα",
    CatalogItemKind.Product, tracksStock: false);

var catering = catalog.AddItem(services.Id, "CATERING", "Υπηρεσία Catering",
    CatalogItemKind.Service);

catalog.RenameItem(pizza.Id, "Πίτσα Μαργαρίτα");
catalog.SetItemActive(catering.Id, false);
```

Τα παραπάνω αντικείμενα υπάρχουν **μόνο στη μνήμη**. Το παράδειγμα δεν αποθηκεύει δεδομένα.

## 6. Εκτέλεση ελέγχων

Από τη ρίζα του repository:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test tests/Aenaon.Domain.Tests --configuration Release --no-build --no-restore
```

Για πλήρη έλεγχο εφαρμογής χωρίς εξωτερική SQL:

```powershell
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Δεν απαιτείται νέα migration, αλλαγή του `appsettings.json` ή εκκίνηση του API. Τα GitHub Actions εκτελούν αυτομάτως τους καθιερωμένους ελέγχους σε Windows και Ubuntu.

## 7. Πότε χρησιμοποιείται / πότε όχι

**Χρησιμοποιείται:** για σχεδιασμό και tests της επιχειρησιακής λογικής του μελλοντικού Back Office, πριν συνδεθεί με SQL ή HTTP.

**Δεν χρησιμοποιείται ακόμη:** για καταχώρηση ειδών από χρήστες, διαχείριση πραγματικών τιμών και ΦΠΑ, πώληση στο POS, υπολογισμό αποθέματος ή παραγωγή παραστατικών.

## 8. Ασφάλεια και σχεδίαση SQL του επόμενου βήματος

{% hint style="warning" %}
Η μοναδικότητα κωδικών αυτού του βήματος ισχύει **μέσα σε ένα στιγμιότυπο του ProductCatalog στη μνήμη**. Για πολλούς χρήστες/servers απαιτούνται unique constraints στη SQL Server και ασφαλής χειρισμός ταυτόχρονων εγγραφών στο Βήμα 9.2.
{% endhint %}

Για το Βήμα 9.2 προβλέπονται:

- `TenantId` σε κατηγορίες και είδη με foreign keys προς την επιχείρηση.
- Σύνθετα keys/constraints για αποφυγή σύνδεσης είδους με κατηγορία άλλης επιχείρησης.
- Unique index `(TenantId, Code)` χωριστά για categories και items, με την πραγματική SQL collation.
- EF Core migrations με `--dry-run`, επιθεώρηση SQL, backup και ρητή επιβεβαίωση πριν εφαρμοστούν.
- Tenant-aware εξουσιοδότηση (Owner/Member), async αποθήκευση, structured logging χωρίς ευαίσθητα δεδομένα.
- Πραγματικά SQL Server integration tests σε προσωρινή βάση.

Το Domain δεν περιέχει κώδικα SQL, HTTP, UI, logging ή μυστικά.

## 9. Troubleshooting

| Πρόβλημα | Ερμηνεία / ενέργεια |
| --- | --- |
| `catalog.tenant.required` | Λείπει έγκυρο TenantId. |
| `catalog.category.code.duplicate` | Υπάρχει ήδη κατηγορία με ίδιο κωδικό στην επιχείρηση. |
| `catalog.item.code.duplicate` | Υπάρχει ήδη είδος ή υπηρεσία με ίδιο κωδικό, ανεξάρτητα από κατηγορία. |
| `catalog.category.not_found` | Η κατηγορία δεν ανήκει στον συγκεκριμένο κατάλογο. |
| `catalog.category.inactive` | Δεν επιτρέπεται νέο είδος ή μετακίνηση στην ανενεργή κατηγορία. |
| `catalog.item.service.stock_forbidden` | Δεν επιτρέπεται παρακολούθηση αποθέματος για υπηρεσία. |
| `catalog.item.kind.invalid` | Ο τύπος δεν είναι Product ή Service. |
| `catalog.item.code.invalid` / `catalog.item.name.invalid` | Διόρθωσε το αντίστοιχο πεδίο με βάση τα όρια. |

## 10. Checklist ολοκλήρωσης

- [ ] Κατηγορίες/είδη απομονωμένα ανά TenantId.
- [ ] Δεν γίνεται χρήση ξένης κατηγορίας.
- [ ] Μοναδικοί κωδικοί ανεξάρτητα από πεζά/κεφαλαία.
- [ ] Οι υπηρεσίες δεν παρακολουθούν απόθεμα.
- [ ] Οι συλλογές προστατεύονται από άμεση τροποποίηση.
- [ ] Οι αποτυχημένες αλλαγές δεν αφήνουν μερική κατάσταση.
- [ ] Όλα τα tests/format/build/CI περάσουν.
- [ ] Καμία αλλαγή στη βάση `AenaonERP_Dev`.

## 11. Επόμενο στάδιο

**Βήμα 9.2 — SQL Server Persistence:** EF Core mappings, migrations, Tenant-safe constraints, Application interfaces, CLI dry-run και πραγματικές SQL integration δοκιμές. Η οθόνη Back Office θα ακολουθήσει στο Βήμα 9.3.
