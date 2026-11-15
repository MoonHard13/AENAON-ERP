# AENAON ERP — 9.5C.4: Εμπορικά scopes και αμετάβλητη εικόνα τιμής

**Κατάσταση:** Feature branch σε review, όχι διαθέσιμη λειτουργία παραγγελίας. **Baseline:** main commit `493f7b2ff61066e817c0d0cba1d120266f9b9117` (15/11/2026).

## Σκοπός

Οριστικοποίηση των υπαρχόντων κανόνων εμπορικής τιμής και εισαγωγή ελάχιστου, versioned Domain contract για την ιστορική εικόνα τιμής που θα χρειαστεί η μελλοντική παραγγελιοληψία. Δεν επηρεάζονται φορολογικοί υπολογισμοί ή καταχωρημένα δεδομένα.

{% hint style="warning" %}
Το `CommercialPriceSnapshot` είναι **μόνο αμετάβλητο αντικείμενο στη μνήμη** και το `PreviewSnapshotAsync` πραγματοποιεί μόνο ανάγνωση. Δεν προσθέτει Orders, API ή CLI endpoint για snapshot, βάση δεδομένων, μηχανισμό offline συγχρονισμού, έκδοση παραστατικού, ΦΠΑ ή myDATA/IMPACT. **Δεν αποτελεί νομικά ή λογιστικά δεσμευμένη τιμή** έως ότου σχεδιαστεί και εγκριθεί η ατομική καταχώρηση παραγγελίας.
{% endhint %}

## Υπάρχοντα εμπορικά πεδία και κανόνες

| Πεδίο | Ερμηνεία |
| --- | --- |
| TenantId | Υποχρεωτική απομόνωση επιχείρησης σε queries και σύνθετα SQL FK |
| StoreId | Συγκεκριμένο κατάστημα ή `null` για όλα τα καταστήματα |
| Channel | `All`, `DineIn`, `Takeaway`, `Delivery`, `Retail`, `Online` |
| ValidFrom / ValidUntil | Συμπεριληπτικές ημερομηνίες ισχύος, με προαιρετική ανοικτή λήξη |
| IsActive | Μόνο ενεργοί τιμοκατάλογοι συμμετέχουν στην επίλυση |
| UnitPrice | EUR, ακριβές `decimal(18,4)`, επιτρέπεται μηδέν |
| PricesIncludeVat | Εμπορική ένδειξη συμπερίληψης ΦΠΑ, **όχι συντελεστής ή φορολογικός χαρακτηρισμός** |

**Προτεραιότητα (policy `store-channel-v1`):** 1) ακριβές Store+Channel, 2) Store+All, 3) AllStores+Channel, 4) AllStores+All. Χωρίς επιλεγμένο Store ελέγχονται μόνο γενικοί τιμοκατάλογοι. Ακριβής κοινή προτεραιότητα δύο ενεργών τιμών σημαίνει `Ambiguous`, ακόμη και με ίσο ποσό.

Το ιστορικό/overlap protection του [Manual 53](53-pricing-integrity-history.md) παραμένει αμετάβλητο.

## Απόφαση για ομάδες πελατών

**Δεν εισάγονται σε αυτό το βήμα.** Δεν υπάρχει ακόμη εγκεκριμένο μοντέλο πελάτη, membership ομάδων ή κανόνας precedence έναντι Store/Channel. Η αυθαίρετη προσθήκη CustomerGroupId σε τιμοκαταλόγους θα μπορούσε να προκαλέσει διαρροή εμπορικής τιμής ή ανεξέλεγκτη υποβάθμιση σε γενική τιμή.

Για μελλοντική σχεδίαση απαιτούνται: tenant-safe πελάτης/ομάδα, επιβεβαιωμένος τρόπος αντιστοίχισης, ρητή προτεραιότητα και συμπεριφορά όταν λείπει ομάδα, edge cases πολλαπλών ομάδων, δικαιώματα και regression. Μέχρι τότε η API επίλυσης **δεν δέχεται** customer-group context.

## Snapshot contract v1

Το `CommercialPriceSnapshot` στο Domain περιέχει μόνο read-only ιδιότητες:

| Ιδιότητα | Περιεχόμενο |
| --- | --- |
| ContractVersion | `1` για μελλοντικό versioned persistence |
| SelectionPolicyVersion | `store-channel-v1`, αποτυπωμένη στη δημιουργία |
| TenantId, StoreId, CatalogItemId | Εξουσιοδοτημένο εμπορικό πλαίσιο |
| PriceListId, PriceListCode | Πηγή επιλεγμένης τιμής |
| PriceListStoreId, PriceListChannel | Scope της πηγής, που μπορεί να είναι γενικό |
| RequestedChannel, BusinessDate | Το κανάλι και η ημερομηνία για την οποία έγινε η επίλυση |
| UnitPrice, Currency, PricesIncludeVat | Επιλεγμένο ποσό, EUR και ένδειξη τιμής |
| CapturedUtc | UTC στιγμή λήψης της εικόνας |

Ο factory έλεγχος απορρίπτει άκυρες αναφορές, scope διαφορετικού καταστήματος, μη συμβατό κανάλι, μη αποδεκτά ποσά, νόμισμα εκτός EUR και timestamp που δεν είναι UTC.

Το `PriceResolutionService.PreviewSnapshotAsync` χρησιμοποιεί την υπάρχουσα `ResolveAsync`: ελέγχει προηγουμένως Read permission για Tenant/Store, ενεργό είδος, ισχύ και μοναδικότητα τιμής. **Μόνο** σε `Resolved` επιστρέφει snapshot. Σε `Denied`, `Invalid`, `ItemUnavailable`, `NotPriced` ή `Ambiguous` επιστρέφει null snapshot, διατηρώντας τη διάγνωση.

## Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται** από προγραμματιστές για unit/regression tests και προετοιμασία των μελλοντικών συμβολαίων Order/POS. Το σημερινό GUI και CLI συνεχίζουν να προσφέρουν τη γνώριμη **μόνο-ανάγνωσης προεπισκόπηση τιμής**, όχι οριστική δέσμευση.

**Δεν χρησιμοποιείται** για έκδοση παραστατικού, POS/offline, λογιστικά ποσά, αποθήκευση παραγγελίας ή ισχυρισμό αμετάβλητης τιμής σε SQL. Δεν δημιουργεί policy revision token ή καταχώρηση σε order line: αυτό απαιτεί μελλοντική transactional αρχιτεκτονική.

## Προαπαιτούμενα και επαλήθευση

Χρειάζονται .NET SDK σύμφωνα με `global.json` (10.0.404), development HTTPS, SQL Server 2022 για τα υπάρχοντα integration tests και λογαριασμός με Tenant Read. Δεν υπάρχει νέα migration. Ενδεικτικές **read-only** εντολές:

```powershell
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet run --project src/Aenaon.Api --launch-profile https -- --migrate-database --dry-run
```

Για σύγκριση εμπορικής τιμής από το υπάρχον CLI:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --resolve-price --username admin --tenant-id TENANT_GUID --item-id ITEM_GUID --store-id STORE_GUID --channel Delivery --business-date 2026-11-15 --confirm-database AenaonERP_Dev --dry-run
```

Η εντολή ζητά διαδραστικά κωδικό πρόσβασης και δεν εκδίδει ούτε αποθηκεύει snapshot. Στο Blazor ανοίξτε **Τιμοκατάλογοι → Προεπισκόπηση τιμής** και επιλέξτε item, store, channel, ημερομηνία.

### Έλεγχοι πριν και μετά

- Επαληθεύστε Tenant/Store, ενεργό είδος/τιμοκατάλογο, business date και απουσία ισοβαθμίας.
- Δοκιμάστε αρχή/λήξη ισχύος, απενεργοποίηση, γενικά fallbacks, Owner/Member και άρνηση ξένου tenant.
- Ελέγξτε ότι μετά από αλλαγή τιμής στην πηγή το **ήδη δημιουργημένο in-memory snapshot** διατηρεί το αρχικό ποσό.
- Δεν υπάρχει επιπλέον SQL, API ή UI εγγραφή. Καμία αλλαγή σε πραγματικά δεδομένα χωρίς ανεξάρτητη έγκριση.

### Πληκτρολόγιο και περιβάλλον

Δεν εισάγονται νέοι έλεγχοι UI ή συντομεύσεις. Η υπάρχουσα φόρμα διατηρεί πλοήγηση με Tab/Enter και τον τεκμηριωμένο μηχανισμό επιλογών της. Η οπτική αποδοχή σε 320/375/390/768/1366/1920 px, Light/Dark, zoom και keyboard-only **παραμένει NOT VERIFIED** αν δεν τρέξει πραγματικός browser.

## Προφυλάξεις και ασφάλεια

Το snapshot δεν αποτελεί αναθεώρηση της πολιτικής τιμολόγησης ούτε απόδειξη συναλλακτικής ατομικότητας: μεταξύ της αναγνωστικής επίλυσης και μιας μελλοντικής εγγραφής παραγγελίας μπορούν να αλλάξουν δεδομένα. Το Order/POS στάδιο πρέπει να επιλέξει ρητά τη στρατηγική optimistic verification/transactional persistence, να αποθηκεύει πλήρες snapshot (όχι μόνο PriceListId), να επιβάλλει tenant/store authorization και να σχεδιάσει idempotency/offline conflicts.

Η αλλαγή policy version σε επόμενο release απαιτεί νέα έκδοση και σαφή διαδρομή συμβατότητας. Μην επανυπολογίζετε ιστορική τιμή από ζωντανό τιμοκατάλογο. Τα audit logs δεν αντικαθιστούν την πραγματική αποθήκευση της τιμής της παραγγελίας.

## Troubleshooting

| Περίπτωση | Ενέργεια |
| --- | --- |
| `NotPriced` | Ελέγξτε περίοδο, ενεργό είδος, κανάλι και Store |
| `Ambiguous` | Εντοπίστε ισόβαθμους τιμοκαταλόγους, διορθώστε policy· μην επιλέξετε αυθαίρετη τιμή |
| `Denied` | Ελέγξτε membership και ιδιοκτησία Store/Tenant |
| `pricing.snapshot.invalid` | Ελέγξτε αναφορές GUID, νόμισμα, κανάλι, scope, ακρίβεια τιμής και UTC timestamp |
| Αλλαγή της τρέχουσας τιμής | Το παλιό snapshot δεν αλλάζει· νέο preview δημιουργεί νέα τιμή |
| Εκκρεμεί migration | Δεν προστέθηκε migration στο 9.5C.4· ελέγξτε την εγκατάσταση χωρίς apply |

## Checklist τεχνικής αποδοχής

- [x] Διατήρηση υλοποιημένων scopes, priority και ambiguity.
- [x] Απόφαση αναβολής customer groups.
- [x] Αμετάβλητο, versioned contract και authorized read-only factory.
- [x] Χωρίς νέα SQL migration, φορολογικό κώδικα ή ενεργοποίηση POS.
- [ ] CI για το συγκεκριμένο PR: restore, format, Release build, unit/SQL/API/CLI και publish.
- [ ] Πραγματικός browser, screenshots, responsive, Light/Dark, πληκτρολόγιο.
- [ ] Review και ρητή αποδοχή από τον χειριστή.

**Κατάσταση:** IMPLEMENTED — NOT FULLY ACCEPTED, μέχρι επιτυχή checks και έγκριση. Επόμενο βήμα μόνο μετά την αποδοχή: **9.5C.5 — Catalog & Pricing Final Acceptance**.
