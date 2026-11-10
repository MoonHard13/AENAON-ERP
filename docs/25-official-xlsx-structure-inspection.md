# Aenaon ERP — Βήμα 9.3C.3B.1: Έλεγχος πραγματικής δομής XLSX ΑΑΔΕ

## 1. Σκοπός

Το πρώτο υποβήμα της επαλήθευσης του επίσημου XLSX δημιουργεί **read-only δομική επιθεώρηση**. Διαβάζει πραγματικές πληροφορίες OOXML από το αποθηκευμένο αρχείο της ΑΑΔΕ και παρουσιάζει ονόματα φύλλων, αριθμό κελιών, ενδεικτικές διευθύνσεις A1 και κείμενα, μαζί με το SHA-256 των πραγματικών bytes.

{% hint style="danger" %}
**Δεν εγκρίνονται ακόμα φορολογικοί συνδυασμοί.** Η ανάγνωση της δομής, ακόμη και όταν επιτύχει, δεν αποδεικνύει ότι συγκεκριμένη τριάδα τύπου παραστατικού/κατηγορίας/τύπου χαρακτηρισμού επιτρέπεται. `IsClassificationMatrixVerified`, `IsFiscalRuleApproved` και `CanIssueFiscalDocument` παραμένουν πάντα `false`. Ούτε ένα κελί δεν αποστέλλεται στο IMPACT ή στο myDATA.
{% endhint %}

## 2. Πρωτογενείς πηγές

- [ΑΑΔΕ — Τεχνικές προδιαγραφές myDATA](https://www.aade.gr/mydata/tehnikes-prodiagrafes-ekdoseis-mydata), έκδοση **2.0.2**, σύνδεσμος «Συνδυασμοί χαρακτηρισμών (αρχείο xls)».
- [Πρωτότυπο αρχείο ΑΑΔΕ 2.0.2](https://www.aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx), για αποθήκευση και ανθρώπινη επιθεώρηση.
- [IMPACT — Classification & Payment Codes](https://developers.s1ecos.com/classification-payment-method-codes-iapr-869832m0), ανεξάρτητο contract review.
- [Microsoft Learn — Working with Sheets](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-sheets) και [Working with Shared Strings](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-the-shared-string-table), τεχνική περιγραφή OOXML.

Έλεγχος δημόσιων σελίδων: **10/11/2026**. Η απευθείας ανάκτηση του πρωτοτύπου XLSX δεν κατέστη δυνατή στο περιβάλλον ανάπτυξης, άρα **δεν ισχυριζόμαστε ότι γνωρίζουμε τη διάταξη του πραγματικού αρχείου** ή ότι οι δοκιμές καλύπτουν τα συγκεκριμένα φύλλα της ΑΑΔΕ. Ο χρήστης πρέπει να εξετάσει το πραγματικό αρχείο.

## 3. Τι υλοποιήθηκε

- `FiscalXlsxEvidenceInspector` στο Application layer. Δεν χρειάζεται εξωτερικό NuGet.
- Ανάγνωση με `System.IO.Compression.ZipArchive` και `System.Xml.Linq`, χωρίς Excel/COM και χωρίς LibreOffice.
- Σύνδεση `xl/workbook.xml`, `xl/_rels/workbook.xml.rels` και `xl/worksheets/*.xml` με βάση τα πραγματικά relationship IDs.
- Ανάγνωση shared strings, inline strings και αριθμητικών κελιών· οι φόρμουλες εμφανίζονται ως **μη επαληθευμένες**, χωρίς χρήση cached αποτελεσμάτων.
- Όρια: XLSX έως 30 MiB, έως 80 φύλλα, έως 600 ZIP entries, έως 128 MiB συνολικό ασυμπίεστο περιεχόμενο και έως 30 δείγματα ανά φύλλο.
- Απόρριψη εξωτερικού worksheet relationship, DTD, VBA, μη έγκυρων ονομάτων ZIP, υπερβολικής συμπίεσης και μη έγκυρων shared string indexes.
- Development-only CLI και regression tests με **συνθετικό OOXML fixture**, όχι πραγματικούς φορολογικούς συνδυασμούς.
- Μηδέν SQL writes, μηδέν migration, μηδέν HTTP/provider calls.

## 4. Προϋποθέσεις

1. .NET SDK που ορίζει το `global.json` και κώδικας από το branch/PR του 9.3C.3B.1.
2. Το επίσημο `.xlsx` αρχείο, κατεβασμένο **από τον σύνδεσμο της ΑΑΔΕ** και αποθηκευμένο ανέπαφο εκτός Git repository.
3. Δικαιώματα ανάγνωσης του φακέλου.
4. Αν απαιτείται φορολογική ερμηνεία, ξεχωριστή διασταύρωση από αρμόδιο λογιστή/φοροτεχνικό.

{% hint style="warning" %}
Παρότι η ιστοσελίδα της ΑΑΔΕ αναφέρει «αρχείο xls», ο τρέχων σύνδεσμος καταλήγει σε **.xlsx**. Τα παλαιά binary `.xls`, τα macro-enabled `.xlsm` και τα αρχεία που μετονομάστηκαν ψευδώς σε `.xlsx` δεν υποστηρίζονται.
{% endhint %}

## 5. Κατέβασμα και προστασία πρωτοτύπου

Άνοιξε τη σελίδα της ΑΑΔΕ, επίλεξε το αρχείο συνδυασμών και αποθήκευσέ το σε `C:\FiscalEvidence\`, χωρίς επεξεργασία. Μην το προσθέσεις στο Git.

Υπολόγισε το αποτύπωμα με PowerShell:

```powershell
Get-FileHash -LiteralPath "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" -Algorithm SHA256
```

Εάν η πραγματική λήψη έχει διαφορετικό όνομα, χρησιμοποίησε το πραγματικό όνομα του αρχείου. Το SHA-256 αποτελεί έλεγχο ακεραιότητας και δεν τεκμηριώνει από μόνο του την αυθεντικότητα της πηγής.

## 6. Εγκατάσταση και δοκιμές

Από PowerShell στη ρίζα του repository:

```powershell
git fetch origin
git switch --track origin/feature/9-3c-3b1-official-xlsx-structure-inspector
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx -c Release --no-restore
dotnet test AenaonERP.slnx -c Release --no-build --no-restore --filter "TestCategory!=SqlServer"
```

Εάν το branch υπάρχει ήδη τοπικά, επίλεξέ το με `git switch feature/9-3c-3b1-official-xlsx-structure-inspector`.

## 7. Ανάγνωση δομής πρωτότυπου XLSX

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-xlsx --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx"
```

Η έξοδος παρουσιάζει SHA-256, μέγεθος αρχείου, λίστα φύλλων, πλήθος κελιών και έως 30 **πραγματικά δείγματα ανά φύλλο**. Για να εστιάσεις σε συγκεκριμένο φύλλο:

```powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --inspect-fiscal-xlsx --evidence-file "C:\FiscalEvidence\syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx" --sheet "ΑΚΡΙΒΕΣ_ΟΝΟΜΑ_ΦΥΛΛΟΥ"
```

Τα ονόματα φύλλων πρέπει να δοθούν ακριβώς όπως εμφανίζονται στην πρώτη έξοδο. Τα τεχνητά `--sheet` παραδείγματα δεν εγγυώνται ότι υπάρχουν στο επίσημο αρχείο.

## 8. Πώς διαβάζουμε το αποτέλεσμα

| Πεδίο | Τι σημαίνει |
| --- | --- |
| SHA-256 πρωτοτύπου | Hash των πραγματικών bytes που αναγνώστηκαν. |
| Φύλλα | Πλήθος πραγματικών OOXML worksheet relationships. |
| Μη κενά κελιά | Αριθμός cells με τιμή ή φόρμουλα που βρέθηκαν στο worksheet XML. |
| `A1`, `B2` κ.λπ. | Πραγματική διεύθυνση κελιού στην XML διάταξη. |
| `[FORMULA: μη επαληθευμένη τιμή]` | Τύπος Excel· δεν εμπιστευόμαστε το cache του XLSX. |

Το πλήθος μη κενών κελιών δεν ισούται με αριθμό έγκυρων φορολογικών συνδυασμών. Επίσης, χρώματα/μορφοποιήσεις/merged ranges **δεν έχουν ερμηνευθεί**· πρέπει να αξιολογηθούν πριν κατασκευαστεί πραγματικό rule matrix.

## 9. Πότε χρησιμοποιείται / δεν χρησιμοποιείται

**Χρησιμοποιείται:** για discovery της διάταξης του επίσημου XLSX, προσδιορισμό sheets και exact cell coordinates και αρχικό τεκμήριο για σχεδιασμό του επόμενου verified importer.

**Δεν χρησιμοποιείται:** για αυτόματο χαρακτηρισμό ή VAT mapping, έγκριση συνδυασμών, απάντηση «επιτρέπεται» σε τύπο παραστατικού, δημοσίευση στους παραγωγικούς πίνακες ή αποστολή σε IMPACT/ΑΑΔΕ.

## 10. Ασφάλεια, logging και αντιμετώπιση προβλημάτων

| Ένδειξη | Ενέργεια |
| --- | --- |
| `fiscal.xlsx.structure.invalid` | Βεβαιώσου ότι είναι πραγματικό OOXML .xlsx, χωρίς αλλοίωση, εξωτερικό worksheet link ή μακροεντολές. |
| Σφάλμα αρχείου/δικαιωμάτων | Έλεγξε το `-LiteralPath`, τα δικαιώματα και το μέγεθος. |
| `--sheet` δεν βρέθηκε | Αντέγραψε ακριβώς το όνομα από την προεπισκόπηση. |
| Εμφανίζεται φόρμουλα | Το cached αποτέλεσμα δεν θεωρείται επαληθευμένο· απαιτείται ειδικός χειρισμός. |
| Διαφορές Excel οπτικής εμφάνισης | Η πρώτη υλοποίηση διαβάζει δομή OOXML, όχι styles/merged cells ή όλη τη μορφοποίηση. |

Exit codes: `0` επιτυχής ανάγνωση, `1` I/O, `2` μη έγκυρη είσοδος/δομή, `130` ακύρωση. Τα logs περιέχουν μόνο την έναρξη, ολοκλήρωση ή κωδικό απόρριψης και **δεν** καταγράφουν πλήρη περιεχόμενα XLSX.

## 11. Προ/μετα-έλεγχοι και rollback

Πριν: επιβεβαίωση official URL, λήψη πρωτότυπου, SHA-256, build/test/CI, αποθήκευση εκτός repository.

Μετά: σύγκρινε τη λίστα φύλλων/πρώτων κελιών με το πραγματικό Excel. Μην αλλάξεις χειροκίνητα το hash ώστε να περάσει λάθος αρχείο. Αν η διάταξη βασίζεται σε styles, χρώματα ή merged ranges, απαιτείται επέκταση του parser και επιπλέον golden tests.

**Rollback:** αποκλειστικά Git revert. Η βάση `AenaonERP_Dev` παραμένει με τις 5 migrations, εφόσον έχουν ήδη εφαρμοστεί επιτυχώς. Δεν υπάρχει νέο schema.

## 12. Checklist

- [ ] Επίσημο αρχείο 2.0.2 κατεβασμένο και διατηρημένο αυτούσιο.
- [ ] SHA-256 υπολογισμένο και αρχειοθετημένο.
- [ ] Ο inspector δείχνει πραγματικά worksheets/cells με shared strings.
- [ ] ZIP, XML, formulas και external relationships αντιμετωπίζονται ασφαλώς.
- [ ] Τα synthetic tests δεν χρησιμοποιούνται ως production code list.
- [ ] Καμία φορολογική έγκριση, SQL write, migration ή provider call.
- [ ] CI Windows/Ubuntu/SQL Server πράσινο πριν από review/merge.
- [ ] Το πραγματικό αποτέλεσμα του inspector έχει επιθεωρηθεί από άνθρωπο.

## 13. Επόμενο βήμα — 9.3C.3B.2

Αφού έχουμε **την πραγματική έξοδο από το επίσημο αρχείο**, θα προσθέσουμε profile-specific parser του συγκεκριμένου layout, support για merged cells/styles εφόσον χρειάζεται, 100% comparison υποψήφιων rules με κελιά, golden fixtures και ξεχωριστή έγκριση φορολογικού reviewer. Η αντιπαραβολή IMPACT και η πραγματική ενεργοποίηση παραμένουν εκκρεμείς.
