# Aenaon ERP — Βήμα 9.3C.3B.2: Πλήρης Εξαγωγή XLSX και Έλεγχος Πληρότητας

## 1. Σκοπός

Το προηγούμενο `--inspect-fiscal-xlsx` εμφάνιζε **μέχρι 30 κελιά ανά φύλλο**. Αυτό επέτρεπε την παρατήρηση της δομής αλλά προκαλούσε παρανοήσεις, αφού φύλλα με 100+ μη κενά κελιά έδειχναν μόνο το πρώτο τμήμα τους στο terminal.

Η νέα υλοποίηση διατηρεί **όλα τα μη κενά κελιά** στη μνήμη και μπορεί να παράγει πλήρες JSON, με έλεγχο πλήθους, συγχωνεύσεων και καταγραφή υποψήφιων χαρακτηρισμών εσόδων/εξόδων. Δεν μεταβάλλει ποτέ τα πρωτότυπα δεδομένα.

{% hint style="danger" %}
**Η πλήρης εξαγωγή δεν σημαίνει φορολογική έγκριση.** Οι κατηγορίες και οι τύποι E3 στο JSON είναι **υποψήφια στοιχεία προς έλεγχο**, όχι εγκεκριμένοι συνδυασμοί ΑΑΔΕ ή συμβατά payloads IMPACT. Ειδικές οδηγίες, συσχετίσεις, μεταφορά χρήσης και «Δεν ενημερώνει E3» χρειάζονται ξεχωριστή ανθρώπινη ερμηνεία. Δεν υπολογίζεται ΦΠΑ και δεν επιτρέπεται έκδοση ή διαβίβαση.
{% endhint %}

## 2. Επίσημες πηγές

| Πηγή | Ρόλος |
| --- | --- |
| [ΑΑΔΕ — τεχνικές προδιαγραφές myDATA v2.0.2](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata) | Επίσημο σημείο ενημέρωσης και σύνδεσμος «Συνδυασμοί χαρακτηρισμών (αρχείο xls)» |
| [ΑΑΔΕ — αρχείο συνδυασμών 2.0.2](https://www.aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx) | Πρωτότυπο προς επιθεώρηση και αρχειοθέτηση SHA-256 |
| [IMPACT — Classification & Payment Method Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0) | Διακριτή αναφορά κατηγοριών και τύπων |
| [IMPACT — VAT & TAX Codes](https://developers.s1ecos.com/vat-tax-codes-869826m0) | Χωριστός έλεγχος ΦΠΑ και εξαιρέσεων σε μελλοντική φάση |
| [IMPACT — Conditional Fields](https://developers.s1ecos.com/minimum-conditional-fields-869822m0) | Πρόσθετες προϋποθέσεις τύπου παραστατικού και καναλιού |

Τα παραπάνω επιβεβαιώθηκαν ως διαθέσιμες τεχνικές πηγές στις **10/11/2026**. Η μηχανή δεν αντικαθιστά την επίσημη τεκμηρίωση, τον φοροτεχνικό ή τον έλεγχο πραγματικής συναλλαγής.

## 3. Προϋποθέσεις

- Git και .NET SDK που ορίζει το `global.json`.
- Windows Development περιβάλλον του Aenaon ERP.
- Το πρωτότυπο αρχείο της ΑΑΔΕ, αποθηκευμένο **χωρίς μεταβολή** στο `C:\FiscalEvidence\`.
- Δικαιώματα ανάγνωσης XLSX και δημιουργίας **νέου** JSON στον φάκελο εξαγωγής.
- Αποθήκευση του JSON εκτός Git repository, επειδή είναι πλήρες αντίγραφο περιεχομένου τεκμηρίου.

Δεν απαιτούνται SQL credentials, κλειδιά IMPACT, API σύνδεση ή επιπλέον NuGet packages. Το πρόγραμμα είναι διαθέσιμο μόνο στο Development.

## 4. Λειτουργίες και shortcuts

Η λειτουργία εκτελείται από CLI. Υποστηρίζονται οι εξής παράμετροι:

| Παράμετρος | Συμπεριφορά |
| --- | --- |
| `--evidence-file FILE.xlsx` | Υποχρεωτικό πρωτότυπο, read-only |
| `--sheet NAME` | Εμφανίζει λεπτομέρειες μόνο του επιλεγμένου φύλλου στο terminal (οι συνοπτικές μετρήσεις παραμένουν για όλα) |
| `--all` | Εκτυπώνει **όλα** τα κελιά αντί για το προεπιλεγμένο δείγμα των 30 ανά φύλλο |
| `--export-json FILE.json` | Αποθηκεύει **όλα τα φύλλα και κελιά**, υποψήφια ζεύγη και εκκρεμείς οδηγίες· δεν αντικαθιστά υπάρχον αρχείο |

Για ακύρωση χρησιμοποίησε `Ctrl+C`. Δεν υπάρχει GUI σε αυτό το στάδιο ούτε απαιτούνται επιπλέον keyboard shortcuts.

## 5. Εκτέλεση — προτεινόμενη πλήρης εξαγωγή

### 5.1 Συγχρονισμός και ποιοτικοί έλεγχοι

```powershell
cd "$env:USERPROFILE\Desktop\AENAON-ERP"
git fetch origin
git switch --track origin/feature/9-3c-3b2-xlsx-complete-export

dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Εάν έχεις ήδη το branch τοπικά, αντί για `git switch --track` χρησιμοποίησε `git switch feature/9-3c-3b2-xlsx-complete-export`.

### 5.2 Δημιουργία πλήρους JSON

```powershell
New-Item -ItemType Directory -Force "C:\FiscalEvidence" | Out-Null

dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-xlsx --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --export-json "C:\FiscalEvidence\AADE_myDATA_2026_FullEvidence.json"
```

Πρέπει να αναγράφεται:

```text
Πληρότητα ανάγνωσης: X/X κελιά — ΠΛΗΡΗΣ
...
Δημιουργήθηκε πλήρες JSON: C:\FiscalEvidence\AADE_myDATA_2026_FullEvidence.json
Δεν έγινε φορολογική έγκριση, αντιπαραβολή κανόνων IMPACT, SQL write ή έκδοση.
```

Τα X είναι **πραγματικά** πλήθη του αρχείου. Δεν προβλέπεται σταθερή τιμή. Εάν ήδη υπάρχει το JSON, χρησιμοποίησε **νέο όνομα**, αντί να αντικαταστήσεις παλαιό τεκμήριο.

### 5.3 Εναλλακτική πλήρης εκτύπωση terminal

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-xlsx --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --all
```

{% hint style="warning" %}
Η εκτύπωση πολλών χιλιάδων γραμμών μπορεί να ξεπεράσει το ιστορικό του terminal. **Το JSON προτιμάται ως πρωτεύον τεκμήριο**. Η κανονική προεπισκόπηση των 30 εμφανίζει πλέον ρητά «30/X» ώστε να μη θεωρείται πλήρης απεικόνιση.
{% endhint %}

### 5.4 Επιθεώρηση συγκεκριμένου φύλλου

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-xlsx --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --sheet "11.1" --all
```

Αυτό περιορίζει τις λεπτομέρειες του terminal, όχι τα δεδομένα που συλλέγονται ούτε αυτά που περιλαμβάνει τυχόν `--export-json`.

## 6. Δομή και σημασία JSON

| Πεδίο | Σημασία |
| --- | --- |
| `ArtifactSha256` | SHA-256 των bytes του εισερχόμενου XLSX |
| `SheetCount` | Πραγματικό πλήθος φύλλων |
| `NonEmptyCellCount` | Πλήθος μη κενών κελιών όλων των φύλλων |
| `ExportedCellCount` | Πλήθος κελιών που γράφτηκαν στο JSON |
| `IsComplete` | `true` μόνο αν τα πλήθη συμφωνούν σε κάθε φύλλο |
| `Sheets[].Cells[]` | **Όλες** οι διευθύνσεις κελιών, τιμές, formula flag και style index |
| `Sheets[].MergedRanges[]` | Οι δηλωμένες συγχωνευμένες περιοχές, π.χ. `B7:B8` |
| `ClassificationEvidence.CandidatePairs[]` | Παρατηρούμενα ζεύγη κατηγορίας/E3, με διακριτό `Direction` (`Income`/`Expense`), πηγή φύλλου και κελιά |
| `ClassificationEvidence.RequiresReview[]` | Επεξηγηματικά, ειδικοί κανόνες, τιμές χωρίς κατηγορία, φόρμουλες ή απροσδιόριστο περιεχόμενο |
| `IsFiscalRuleApproved` / `CanIssueFiscalDocument` | **Πάντα `false`** |

Ο αναλυτής υποθέτει προσωρινά την παρατηρημένη δομή **B/C για έσοδα και E/F για έξοδα** μόνο ως υποψήφια ένδειξη. Δεν συνάγει ότι κάθε γραμμή είναι επιτρεπόμενη σε συγκεκριμένο τύπο παραστατικού. Κάθε `E3_` χωρίς σαφή ενεργή κατηγορία ή κάθε οδηγία μη-Ε3 παραπέμπεται σε review.

## 7. Ασφάλεια και τεχνικά όρια

- Η διαχείριση XLSX είναι read-only και χρησιμοποιεί `ZipArchive` και ασφαλή XML parsing (DTD απενεργοποιημένο).
- Όριο πρωτοτύπου 30 MiB, 80 φύλλα, 600 ZIP entries, συνολικά 128 MiB uncompressed.
- Όριο 500.000 μη κενών κελιών και **8.192 χαρακτήρων ανά κελί**. Αν παραβιαστεί, απορρίπτεται το workbook αντί να γίνει σιωπηρή περικοπή.
- Έλεγχος διπλών cell addresses, συντεταγμένων Excel και πραγματικών merged ranges.
- Το `--export-json` χρησιμοποιεί `FileMode.CreateNew`. Δεν αντικαθιστά υπάρχον JSON ούτε το πρωτότυπο XLSX. Αποτυχία κατά τη συγγραφή προκαλεί προσπάθεια διαγραφής του *νέου, μερικώς δημιουργημένου* JSON.
- Οι μορφοποιήσεις αναφέρονται ως **style indexes**, χωρίς ερμηνεία χρωμάτων ή επιχειρησιακής σημασίας.
- Οι φόρμουλες καταγράφονται ως μη αξιόπιστες, χωρίς εκτέλεση ή χρήση cached results.
- Καμία SQL αλλαγή, migration, seed, απόφαση ΦΠΑ, myDATA/IMPACT API call ή μεταβολή role.

## 8. Troubleshooting

| Μήνυμα | Ενέργεια |
| --- | --- |
| `fiscal.xlsx.structure.invalid` | Επίλεξε έγκυρο XLSX, έλεγξε κατεστραμμένο ZIP/XML, διπλά addresses, VBA, DTD ή όρια μεγέθους |
| `fiscal.xlsx.export.incomplete` | Η συλλογή δεν καλύπτει όλα τα μετρημένα κελιά· σταμάτα την επεξεργασία |
| `fiscal.xlsx.export.path.invalid` | Δώσε διαδρομή νέου `.json` |
| Υπάρχει ήδη το JSON | Χρησιμοποίησε διαφορετικό όνομα για versioned αρχειοθέτηση |
| Το terminal δείχνει 30/X | Προεπισκόπηση μόνο — χρησιμοποίησε `--all` ή `--export-json` |
| `RequiresReview` περιέχει εγγραφές | Είναι σκόπιμο: **μην** τις μετατρέψεις σε αυτόματα επιτρεπτούς συνδυασμούς |

Κωδικοί CLI: `0` επιτυχής επιθεώρηση/εξαγωγή, `1` πρόσβαση/συγγραφή, `2` άκυρη είσοδος/δομή, `130` ακύρωση. Τα logs καταγράφουν μόνο counts και error codes, όχι κωδικούς πρόσβασης ή περιεχόμενο των πινάκων.

## 9. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται:** τεχνικός έλεγχος πληρότητας του επίσημου XLSX, σύγκριση εκδόσεων και προετοιμασία για golden-file verification όλων των επιτρεπτών/ειδικών συνδυασμών.

**Δεν χρησιμοποιείται:** για έκδοση ή διαβίβαση παραστατικών, αυτόματη έγκριση λογιστικών κανόνων, συμπλήρωση IMPACT payloads ή παραγωγική ενημέρωση `FiscalReferenceCodes`.

## 10. Έλεγχοι, αναστροφή, συντήρηση

- **Πριν:** επαλήθευση προέλευσης πρωτοτύπου από επίσημη ΑΑΔΕ, αποθήκευση εκτός Git και υπολογισμός SHA-256.
- **Μετά:** σύγκριση `NonEmptyCellCount == ExportedCellCount` και `IsComplete == true`, επιθεώρηση `MergedRanges` / `RequiresReview`, αρχείο JSON σε ασφαλή φάκελο.
- **Rollback:** Git revert στο PR εάν προκύψει regression· δεν υπάρχουν migrations ή αλλαγές SQL να αναστραφούν.
- **Ενημέρωση:** όταν αλλάζει το πρωτότυπο XLSX, νέα εξαγωγή με νέο hash/όνομα και νέο review.
- **Αρχειοθέτηση:** αντιμετώπισε το JSON ως τεκμήριο με ελεγχόμενα δικαιώματα και γνωστή πολιτική retention.

## 11. Checklist ολοκλήρωσης

- [ ] Επίσημο XLSX και πρωτότυπο SHA-256 επιβεβαιωμένα.
- [ ] Όλα τα 53 φύλλα (για το συγκεκριμένο τεκμήριο που επιθεωρήθηκε) εμφανίζονται χωρίς χαμένες εγγραφές.
- [ ] Τα πλήθη πραγματικών και εξαγόμενων κελιών συμφωνούν **ανά φύλλο** και συνολικά.
- [ ] Έχει δημιουργηθεί νέο JSON με όλες τις τιμές, merged ranges και style indices.
- [ ] Υποψήφια ζεύγη εσόδων/εξόδων δεν αναμειγνύονται.
- [ ] Οι επεξηγηματικές οδηγίες καταγράφονται για ανθρώπινο review.
- [ ] CI σε Windows/Ubuntu, format, build, MSTest και SQL Server regression πράσινα.
- [ ] Καμία ενεργοποίηση κανόνα, φορολογικού import ή παραγωγικής τιμολόγησης.

## 12. Επόμενο βήμα

**9.3C.3B.3 — Verified Rule-Matrix Reconciliation:** αντιπαραβολή του **πλήρους** JSON με την πραγματική διάταξη του πρωτοτύπου (styles, merged cells, εξαιρέσεις, συσχετιζόμενα παραστατικά), golden tests από ελεγχόμενο official artifact, ανεξάρτητη φοροτεχνική έγκριση και ξεχωριστά IMPACT contract tests. Καμία ενεργοποίηση πριν ολοκληρωθούν οι παραπάνω έλεγχοι.
