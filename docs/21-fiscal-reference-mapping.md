# Aenaon ERP — Βήμα 9.3B: Fiscal Reference Data & Tax Mapping

## 1. Σκοπός

Το 9.3B προσθέτει **versioned φορολογικούς κωδικούς** και **προσωρινές αντιστοιχίσεις ανά επιχείρηση, είδος και κανάλι B2C/B2B/B2G**, χρησιμοποιώντας EF Core / SQL Server. Προσθέτει ασφαλή προεπισκόπηση, όχι φορολογική μηχανή έκδοσης.

{% hint style="danger" %}
**Καμία αντιστοίχιση αυτής της έκδοσης δεν επιτρέπει έκδοση φορολογικού παραστατικού.** Η ιδιότητα `CanIssueFiscalDocument` παραμένει πάντα `false`. Δεν υπάρχουν υπολογισμοί ΦΠΑ, παραγωγικό seed, αποστολή στο IMPACT, παράλληλη διαβίβαση ΑΑΔΕ, εισαγωγή credentials ή user-facing διαχείριση.
{% endhint %}

## 2. Επίσημες πηγές και πολιτική συμμόρφωσης

Έλεγχος 10 Νοεμβρίου 2026:

| Πηγή | Τι παρέχει |
| --- | --- |
| [ΑΑΔΕ myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Ξεχωριστή τεκμηρίωση ERP και παρόχων, XSD, αρχείο επιτρεπόμενων συνδυασμών χαρακτηρισμών. |
| [IMPACT VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | `VatCategoryCode`, `vatExemptionCategoryCode`, ποσοστά/αιτίες με διακριτή σημασία. |
| [IMPACT Classifications](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | `ClassificationCategoryCode`, `ClassificationTypeCode`. |
| [IMPACT Units](https://developers.s1ecos.com/measurement-unit-codes-iapr-en16931-869829m0) | myDATA `MeasurementUnitCode` και EN16931 `MeasurementUnitCodeEN`. |
| [IMPACT Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Υποχρεωτικά πεδία ανά είδος εγγράφου, ειδικές απαιτήσεις B2G. |
| [IMPACT Changelog](https://developers.s1ecos.com/changelog-1116726m0) | Επικαιροποιήσεις v2.0.2, αλλαγές στον κατάλογο απαλλαγών βάσει ν. 5144/2024. |

**Κανόνας:** κωδικός + σύστημα + πηγή + έκδοση + περίοδος ισχύος αποτελούν συγκεκριμένη αναφορά. Ο σκέτος κωδικός «1» δεν είναι αρκετός. Το `VerifiedAtUtc` είναι ο χρόνος ελέγχου της πηγής, όχι η νομική ημερομηνία έναρξης ισχύος. Την `EffectiveFrom` την τεκμηριώνουμε χωριστά.

{% hint style="warning" %}
Τα δοκιμαστικά δεδομένα έχουν κωδικούς `SYNTHETIC-TEST-CODE` και έκδοση `CI-ONLY`. Δεν είναι πραγματικοί κωδικοί φορολογίας και δεν πρέπει να εισάγονται σε παραγωγική βάση. Δεν υπάρχει αυτόματο import από ιστοσελίδες και δεν πραγματοποιείται seed κωδικών στο migration.
{% endhint %}

## 3. Αρχιτεκτονική

| Στοιχείο | Αρμοδιότητα |
| --- | --- |
| `FiscalReferenceRecord` | Πηγή, code system, version, περιγραφή, HTTPS URL, περίοδος και UTC timestamp ελέγχου. |
| `FiscalTaxMappingRecord` | Προσωρινό προφίλ Tenant/Item/CustomerChannel και ξένα κλειδιά προς εξειδικευμένες αναφορές. |
| `FiscalReferenceConfiguration` | Composite unique identity, σωστά μεγέθη πεδίων, constraints, ημερομηνίες. |
| `FiscalTaxMappingConfiguration` | Composite FK `(TenantId, CatalogItemId)`, FK για κάθε κωδικό, μοναδική ημερομηνία έναρξης. |
| `FiscalMappingPreviewService` | Read-only επίλυση υποψηφίου, απόρριψη ασάφειας/ανύπαρκτων refs/ξένου Tenant/ανενεργών ημερομηνιών. |
| `20261110150000_FiscalReferenceMapping` | Τέταρτη μη καταστροφική SQL migration. |

Το `FiscalMappingPreviewService` είναι **εσωτερική υπηρεσία preview**. Επιλογή έγκυρου κωδικού σημαίνει μόνο ότι υπάρχει έγκυρη καταχώριση στο δικό μας registry· **δεν τεκμηριώνει** ότι ο συνδυασμός ΦΠΑ/χαρακτηρισμού είναι νόμιμος για συγκεκριμένο παραστατικό. Οι πολλαπλές χρονικά επικαλυπτόμενες εγγραφές απορρίπτονται ρητά. Δεν επιτρέπεται B2G preview πριν συμπληρωθούν οι ειδικοί κανόνες.

### Περιορισμοί

Δεν υπάρχουν ακόμη εγκριτικές ροές, audit χρήστη εγκρίσεων, έλεγχοι αρχείου επιτρεπόμενων συνδυασμών ΑΑΔΕ, αποτίμηση ποσοστού VAT ή status κατά παραστατικό. Το `CustomerChannel` μόνο του δεν καθορίζει τύπο τιμολογίου ή απαλλαγή. Μελλοντικός κανόνας έκδοσης απαιτεί document type, περιοχή, φορολογική κατάσταση αντισυμβαλλομένου, πραγματική έγκριση και ειδικά contract tests.

## 4. Προϋποθέσεις

- Windows ή Ubuntu με .NET SDK της έκδοσης `global.json` (10.0.403).
- SQL Server 2022 με compatibility level 160 για εφαρμογή migration μόνο σε απομονωμένο Development περιβάλλον.
- Επιβεβαιωμένη σύνδεση στη δική σου βάση ανάπτυξης, όχι παραγωγική βάση.
- Git, πρόσβαση στο repository και έγκυρα Development User Secrets όπως περιγράφονται στο `docs/03-sql-server-connection.md`.

## 5. Προεπισκόπηση migration — ασφαλής διαδικασία

Από το root του repository:

```powershell
git fetch origin
git switch feature/9-3b-fiscal-reference-mapping
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api -- --check-database
```

{% hint style="warning" %}
Μην εκτελέσεις migration στην `AenaonERP_Dev` πριν ολοκληρωθεί επιτυχώς το PR CI και δημιουργηθεί backup της βάσης. Η εντολή εφαρμόζεται μόνο αφού επιβεβαιώσεις το επιθυμητό όνομα Development βάσης, σύμφωνα με `docs/04-tenancy-database.md`.
{% endhint %}

## 6. Έλεγχοι πριν / μετά

Πριν: ελέγχουμε εκδόσεις SDK/πακέτων, `git status -sb`, κατάσταση CI, τρέχουσες migrations και backup.

Μετά: περιμένουμε τέσσερις migrations και δύο νέους πίνακες `dbo.FiscalReferenceCodes`, `dbo.FiscalTaxMappings`. Οι πίνακες πρέπει να είναι κενοί μετά την εφαρμογή migration, επειδή δεν διανέμουμε φορολογικά δεδομένα χωρίς επιβεβαίωση.

Το SQL CI δημιουργεί απομονωμένη βάση, εφαρμόζει migration, ελέγχει composite FK απομόνωσης Tenant, namespace validation, αμφίσημες περιόδους, B2G gate και ότι η προεπισκόπηση δεν δίνει δικαίωμα έκδοσης.

## 7. Σφάλματα και troubleshooting

| Κωδικός | Αιτία / ενέργεια |
| --- | --- |
| `fiscal.registry.evidence.invalid` | Λείπει αξιόπιστη HTTPS πηγή, περιγραφή ή UTC timestamp. |
| `fiscal.mapping.invalid` | Λείπουν IDs, σωστό κανάλι ή περίοδος. |
| `fiscal.mapping.store.invalid` | Το κατάστημα δεν ανήκει στον Tenant του αιτήματος. |
| `fiscal.mapping.item.invalid` | Το είδος είναι ανενεργό, ξένης επιχείρησης ή άλλου τύπου. |
| `fiscal.mapping.not_found` | Δεν υπάρχει πρόταση για τη συγκεκριμένη επιχείρηση/είδος/ημερομηνία/κανάλι. |
| `fiscal.mapping.ambiguous` | Δύο προτάσεις καλύπτουν την ίδια ημερομηνία. |
| `fiscal.mapping.reference.invalid` | Λείπει έκδοση κωδικού ή αναφέρεται λάθος φορολογικό namespace. |
| `fiscal.profile.period.inactive` | Η πρόταση ή έκδοση κωδικού είναι εκτός ισχύος. |
| `fiscal.profile.order.not_fiscal` | Έγινε χρήση preview σε απλή παραγγελία. |
| `fiscal.mapping.b2g.pending` | Δεν έχει ολοκληρωθεί η B2G policy/EN16931 επιβεβαίωση. |

Σε SQL error 547 ελέγχουμε σύνθετο Tenant/Item FK και όλα τα referenced code IDs. Δεν κάνουμε bypass στα constraints.

## 8. Ασφάλεια και logging

Ούτε κωδικοί πρόσβασης ούτε API keys ή φορολογικά payloads καταγράφονται. Η read-only υπηρεσία χρησιμοποιεί EF Core `AsNoTracking` και `CancellationToken`, εκδίδει δομημένο πληροφοριακό log μόνο για την προεπισκόπηση. Το registry είναι κοινόχρηστο (global reference data), ενώ κάθε mapping ανήκει αυστηρά σε ένα Tenant.

## 9. Rollback και συντήρηση

Πριν από migration: επαναφορά μόνο του Git branch/PR. Μετά από εφαρμοσμένο migration: **backup και δοκιμασμένο σχέδιο επαναφοράς**. Η χειροκίνητη εφαρμογή `Down` θα διέγραφε τα δύο fiscal tables και δεν αποτελεί αποδεκτό production rollback. Μην κάνεις `database drop`, `EnsureCreated` ή μη ελεγχόμενο `database update`.

## 10. Checklist αποδοχής

- [ ] PR checks Windows/Ubuntu πράσινα.
- [ ] EF model snapshot ισοδύναμο με runtime model.
- [ ] SQL Server migration + constraints ελεγμένα.
- [ ] Απομόνωση Tenant/Item με composite FK.
- [ ] Πηγή και έκδοση κωδικών με χρονική ισχύ.
- [ ] Καμία προεπιλογή 24%/13% ή τεχνητός κωδικός σε production seed.
- [ ] Καμία λειτουργία φορολογικής έκδοσης ή IMPACT/ΑΑΔΕ write.
- [ ] Έγκριση λογιστικών/φορολογικών συνδυασμών πριν την επόμενη φάση.

## 11. Επόμενο βήμα

**9.3C — Verified Reference Import & Fiscal Rule Approval**: ελεγχόμενο versioned import πραγματικών κωδικών, αρχειοθέτηση πηγών, cross-code combination matrix της ΑΑΔΕ, context ανά τύπο παραστατικού, audit εγκρίσεων και σταδιακά contract tests του IMPACT. Μόνο μετά από επιβεβαίωση ενεργοποιείται φορολογική επίλυση και το Back Office 9.4.
