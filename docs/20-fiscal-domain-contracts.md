# Aenaon ERP — Βήμα 9.3A: Fiscal Domain Contracts & Tests

## 1. Σκοπός

Υλοποιείται η **θεμελίωση** της φορολογικής πληροφορίας του καταλόγου ειδών ως μικρό, αυτόνομο Domain, χωρίς SQL, endpoints και εξωτερικές βιβλιοθήκες.

Ο στόχος είναι η αποφυγή λανθασμένων ταυτίσεων μεταξύ:
- Φορολογικής κατηγορίας ΦΠΑ, αιτίας απαλλαγής και ποσοστού ΦΠΑ.
- Κατηγορίας εσόδων και τύπου χαρακτηρισμού.
- Μονάδων μέτρησης myDATA και EN16931.
- Παραγγελίας F&B και φορολογικού παραστατικού.
- Κωδικού παρόχου και του ίδιου επιχειρησιακού στοιχείου Aenaon.

{% hint style="warning" %}
**Το Βήμα 9.3A ΔΕΝ υπολογίζει ΦΠΑ, δεν πιστοποιεί κωδικούς, δεν επικυρώνει φορολογικούς συνδυασμούς και δεν επιτρέπει έκδοση παραστατικών.** Το ότι ένα `FiscalProfileCandidate` είναι δομικά έγκυρο σημαίνει μόνο ότι οι κωδικοί βρίσκονται στα κατάλληλα namespaces και ότι τα αναγνωριστικά/ημερομηνίες είναι συνεπή.
{% endhint %}

## 2. Επίσημες πηγές (έλεγχος 10/11/2026)

| Πηγή | Στοιχείο |
| --- | --- |
| https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata | myDATA v2.0.2 / ERP, πάροχοι, XSD, χαρακτηρισμοί. |
| https://developers.s1ecos.com/vat-tax-codes-869826m0 | `VatCategoryCode` και `vatExemptionCategoryCode` είναι διαφορετικές έννοιες. |
| https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0 | `ClassificationCategoryCode`, `ClassificationTypeCode`. |
| https://developers.s1ecos.com/measurement-unit-codes-iapr-en16931-869829m0 | `measurementUnitCode`, `measurementUnitCodeEN`. |
| https://developers.s1ecos.com/minimum-conditional-fields-869822m0 | Conditional fields ανά παραστατικό, B2G κ.λπ. |
| https://einvoiceapi.impact.gr/swagger/coreapi/swagger.json | OpenAPI συμβόλαιο IMPACT, v2.8.12-build.b9a733 (στιγμιότυπο). |

Επανελέγχουμε την ισχύουσα έκδοση κατά την επόμενη φορολογική υλοποίηση. Βλέπε επίσης `docs/18-mydata-impact-compatibility.md` και `docs/19-fiscal-catalog-design.md`.

## 3. Υλοποιημένοι τύποι

| Τύπος | Σκοπός |
| --- | --- |
| `FiscalCodeSystem` | Ισχυρός διαχωρισμός VAT category / exemption / income category / income type / myDATA unit / EN16931 unit / IMPACT document / myDATA invoice type. |
| `FiscalReferenceSource` | Πηγή ΑΑΔΕ, IMPACT ή EN16931, ξεχωριστή από τον κωδικό και το namespace. |
| `FiscalReferenceCode` | Αμετάβλητη αναφορά με `SourceVersion` και `EffectiveFrom`/`EffectiveUntil`. |
| `FiscalTransactionContext` | Αμετάβλητο πλαίσιο Tenant/Store/Item/ημερομηνίας/καναλιού. |
| `FiscalCustomerChannel` | B2C, B2B, B2G χωρίς φορολογική υπόθεση. |
| `FiscalOperationKind` | Order vs FiscalDocument. |
| `FiscalProfileCandidate` | Μη εγκεκριμένη πρόταση με απαραίτητα VAT/income references, προαιρετικές μονάδες και απαλλαγή. |

### Πολιτική ανάθεσης

Το `FiscalProfileCandidate` διατηρεί αμετάβλητο `TenantId` και `CatalogItemId`. Η `ValidateScope` απορρίπτει ξένες επιχειρήσεις/είδη, απλές παραγγελίες και περιόδους χωρίς ισχύ.

**Δεν** ελέγχει εάν οι κωδικοί είναι επιτρεπόμενοι από τη σημερινή έκδοση ΑΑΔΕ/IMPACT, εάν οι χαρακτηρισμοί ταιριάζουν σε συγκεκριμένο παραστατικό ή εάν το προτεινόμενο ΦΠΑ είναι νόμιμο.

## 4. Παράδειγμα αποκλειστικά σε δοκιμές

Το ακόλουθο παράδειγμα χρησιμοποιεί **τεχνητούς κωδικούς** που δεν αποτελούν πραγματικές φορολογικές τιμές:

```csharp
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Fiscal;

var date = new DateOnly(2026, 1, 1);
var tenantId = Guid.NewGuid();
var itemId = Guid.NewGuid();

var vat = FiscalReferenceCode.Create(FiscalReferenceSource.Impact,
    FiscalCodeSystem.MyDataVatCategory, "SYNTHETIC-TEST-VAT", "TEST-ONLY", date);
var category = FiscalReferenceCode.Create(FiscalReferenceSource.Impact,
    FiscalCodeSystem.MyDataIncomeCategory, "SYNTHETIC-TEST-INCOME", "TEST-ONLY", date);
var type = FiscalReferenceCode.Create(FiscalReferenceSource.Impact,
    FiscalCodeSystem.MyDataIncomeType, "SYNTHETIC-TEST-TYPE", "TEST-ONLY", date);

var candidate = FiscalProfileCandidate.Create(tenantId, itemId, date, null, vat, category, type);

var context = FiscalTransactionContext.Create(tenantId, Guid.NewGuid(), itemId,
    CatalogItemKind.Service, date, FiscalCustomerChannel.B2B,
    FiscalOperationKind.FiscalDocument);

// Ελέγχεται μόνο το Tenant/Item και η χρονική ισχύς, όχι η φορολογική ορθότητα.
candidate.ValidateScope(context);
```

{% hint style="danger" %}
Μην χρησιμοποιείς τους τεχνητούς κωδικούς ή αυτά τα αντικείμενα για πραγματική έκδοση παραστατικού. Δεν υπάρχει ασφαλές production mapping ή δυνατότητα αποστολής από αυτό το βήμα.
{% endhint %}

## 5. Εγκατάσταση / έλεγχοι

Από τη ρίζα του repository:

```powershell
git status -sb
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test tests/Aenaon.Domain.Tests --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FiscalDomainTests"
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Τα GitHub Actions πραγματοποιούν τους υπάρχοντες Windows/Ubuntu ελέγχους και τα απομονωμένα SQL integration tests. Δεν απαιτούνται credentials ή νέα migration για το Βήμα 9.3A.

## 6. Προϋποθέσεις και όρια

- .NET SDK σύμφωνα με `global.json` και υπάρχον solution.
- Δεν απαιτείται SQL Server, IMPACT authentication ή κλειδί παρόχου.
- Δεν υπάρχει CLI ή GUI καταχώρησης φορολογικών προφίλ. Τα tests είναι ο προβλεπόμενος τρόπος επαλήθευσης.
- Δεν αποθηκεύονται τιμές ΦΠΑ ή εσόδων σε πίνακες.
- Δεν ενεργοποιείται καμία φορολογική επικοινωνία με ΑΑΔΕ ή IMPACT.

## 7. Troubleshooting

| Κωδικός | Ερμηνεία |
| --- | --- |
| `fiscal.reference.system.invalid` | Μη αναγνωρισμένη πηγή ή namespace κωδικού. |
| `fiscal.reference.code.invalid` | Κενός/άκυρος κωδικός ή μη επιτρεπτά κενά. |
| `fiscal.reference.version.invalid` | Απουσία έκδοσης ή χαρακτήρες ελέγχου. |
| `fiscal.reference.period.invalid` | Μη έγκυρη περίοδος κωδικού. |
| `fiscal.context.identity.required` | Ελλείπον Tenant/Store/Item ID. |
| `fiscal.context.invalid` | Άκυρος τύπος είδους, καναλιού ή συναλλαγής/ημερομηνίας. |
| `fiscal.profile.reference.system.invalid` | Φορολογικό πεδίο με αναφορά σε λάθος code system. |
| `fiscal.profile.scope.mismatch` | Φορολογική πρόταση άλλης επιχείρησης ή άλλου είδους. |
| `fiscal.profile.order.not_fiscal` | Επιχειρείται συσχέτιση πρότασης με απλή παραγγελία. |
| `fiscal.profile.period.inactive` | Ημερομηνία εκτός προφίλ ή έκδοση κωδικού εκτός ισχύος. |

## 8. Διαχείριση αλλαγών και rollback

Επειδή δεν υπάρχουν migrations/δεδομένα, το rollback είναι **Git revert του αντίστοιχου PR**. Δεν απαιτείται SQL backup ειδικά για αυτό το βήμα. Μην πειράζεις την `AenaonERP_Dev`.

## 9. Checklist

- [ ] Ισχυρός διαχωρισμός φορολογικών code systems και πηγών.
- [ ] Αμετάβλητα Domain contracts.
- [ ] Tenant/Item isolation και ημερομηνίες ισχύος.
- [ ] Καμία σύνδεση απλής παραγγελίας με φορολογική έκδοση.
- [ ] Καμία αυτόματη φορολογική προεπιλογή ή δήθεν έγκριση.
- [ ] CI Windows/Ubuntu/SQL, build, format, tests πράσινα.
- [ ] Χωρίς αλλαγή δεδομένων/βασικού Dashboard.
- [ ] Επανέλεγχος ΑΑΔΕ/IMPACT πριν από provider mappings.

## 10. Επόμενα στάδια

**9.3B:** fiscal rules + versioned reference data + tenant-safe persistence, μετά από πραγματικό tax mapping review/contract fixtures.

**9.4:** Ασφαλές Back Office GUI/CLI διαχείρισης ειδών και φορολογικών ρυθμίσεων. Πραγματική έκδοση παραστατικών θα γίνει μόνο μετά από εξουσιοδότηση, πλήρη provider integration tests και επιχειρησιακή έγκριση.
