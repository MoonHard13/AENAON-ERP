# Aenaon ERP — Login, Logout και Ασφαλείς Συνεδρίες

## Σκοπός

Το Βήμα 8.3 υλοποιεί πραγματικές HTTPS συνεδρίες μέσω ASP.NET Core Cookie Authentication, CSRF tokens, rate limiting και ελέγχου Identity. Οι λειτουργίες είναι **Development-only** μέχρι να ετοιμαστεί ασφαλές production hosting. Δεν απαιτείται migration.

## Προϋποθέσεις

- .NET 10 SDK, SQL Server 2022 και ενημερωμένο repository.
- Ενεργό User Secret σύνδεσης για τη δοκιμαστική βάση AenaonERP_Dev, με δύο applied migrations.
- Υπάρχων Administrator admin και Owner membership στην Aenaon Demo.
- Αξιόπιστο HTTPS developer certificate.

{% hint style="warning" %}
Μη δημοσιεύσεις τα Development endpoints σε δημόσιο περιβάλλον. Για Production απαιτούνται shared/persistent Data Protection keys, HTTPS, σωστή διαχείριση reverse proxy, email verification και περαιτέρω πολιτικές λογαριασμών.
{% endhint %}

## 1. Ενημέρωση και έλεγχοι

Μετά το merge του Pull Request:

~~~powershell
git status
git pull --ff-only origin main
dotnet restore AenaonERP.slnx --locked-mode
dotnet format AenaonERP.slnx --verify-no-changes --no-restore
dotnet build AenaonERP.slnx --configuration Release --no-restore
dotnet test AenaonERP.slnx --configuration Release --no-build --no-restore --filter "TestCategory!=SqlServer"
dotnet dev-certs https --trust
~~~

## 2. Εκκίνηση HTTPS API

Στο πρώτο VS Code Terminal:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https
~~~

Χρησιμοποίησε μόνο https://localhost:7140. Το HTTP δεν επιτρέπεται για διαχείριση ασφαλών cookies.

## 3. API endpoints

| Method | Endpoint | Περιγραφή | Session |
| --- | --- | --- | --- |
| GET | /api/auth/csrf | Λήψη antiforgery request token | Όχι |
| POST | /api/auth/login | Έλεγχος κωδικού, έκδοση cookie | Όχι |
| GET | /api/auth/me | Τρέχων χρήστης και προσβάσιμες επιχειρήσεις | Ναι |
| GET | /api/auth/access | Έλεγχος Read/Manage σε Tenant/Store | Ναι |
| POST | /api/auth/logout | Διαγραφή cookie | Ναι |

Το CSRF token στέλνεται σε header με όνομα X-Aenaon-CSRF για κάθε POST. Μετά το login απαιτείται **νέο** CSRF token για logout.

## 4. Πρακτική δοκιμή PowerShell

Άνοιξε δεύτερο Terminal και κράτησε το API ενεργό στο πρώτο:

~~~powershell
$baseUrl = 'https://localhost:7140'
$webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf = Invoke-RestMethod -Uri "$baseUrl/api/auth/csrf" -Method Get -WebSession $webSession
~~~

Για login ζήτησε κωδικό μέσω ασφαλούς prompt:

~~~powershell
$credential = Get-Credential -UserName 'admin' -Message 'Aenaon ERP Development login'
$loginBody = @{ userName = 'admin'; password = $credential.GetNetworkCredential().Password } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -WebSession $webSession -ContentType 'application/json' -Headers @{ 'X-Aenaon-CSRF' = $csrf.token } -Body $loginBody
Remove-Variable loginBody, credential
$login
~~~

{% hint style="danger" %}
Μην κοινοποιήσεις τον κωδικό, το loginBody, τα session cookies ή τα CSRF tokens. Η PowerShell διατηρεί προσωρινά το password στη μνήμη μόνο για την κατασκευή της JSON αίτησης. Μην το βάλεις στο command history ή σε αρχείο.
{% endhint %}

Για έλεγχο συνεδρίας:

~~~powershell
$me = Invoke-RestMethod -Uri "$baseUrl/api/auth/me" -Method Get -WebSession $webSession
$me | ConvertTo-Json -Depth 6
~~~

Αναμένονται UserId, username και μόνο τα memberships του συνδεδεμένου χρήστη. Δεν εμφανίζονται password hashes ή security stamps.

## 5. Έλεγχος Tenant Authorization

~~~powershell
$tenantId = '9698df79-3c31-41c9-b292-b7240c7265fb'
$storeId = 'c697b9c3-e221-4156-b533-f54dee27d979'
$access = Invoke-RestMethod -WebSession $webSession -Method Get -Uri "$baseUrl/api/auth/access?tenantId=$tenantId&storeId=$storeId&permission=Manage"
$access
~~~

Αναμένεται allowed=true. Ένα διαφορετικό ή ξένο StoreId πρέπει να επιστρέψει HTTP 403, χωρίς να αποκαλύπτει στοιχεία του ξένου πόρου. Το UserId προέρχεται από το επικυρωμένο cookie και όχι από το αίτημα.

## 6. Logout

Πάρε νέο CSRF token μετά το login και αποσυνδέσου:

~~~powershell
$csrf = Invoke-RestMethod -Uri "$baseUrl/api/auth/csrf" -Method Get -WebSession $webSession
Invoke-RestMethod -Uri "$baseUrl/api/auth/logout" -Method Post -WebSession $webSession -Headers @{ 'X-Aenaon-CSRF' = $csrf.token }
~~~

Στη συνέχεια το GET /api/auth/me πρέπει να επιστρέψει HTTP 401.

## 7. Αρχιτεκτονική και ασφάλεια

| Κλάση / υπηρεσία | Ευθύνη |
| --- | --- |
| ISessionIdentityService | Ασφαλές συμβόλαιο για session identities |
| IdentitySessionService | Password verification, έλεγχος stamp και membership |
| SessionCookieEvents | Ανάκληση μη έγκυρου cookie |
| SessionEndpoints | HTTPS CSRF, login, logout, me και access |
| TenantAuthorizationService | Έλεγχος Read/Manage μέσα στη σωστή επιχείρηση |

Τα session cookies έχουν πρόθεμα __Host-, HttpOnly, Secure, SameSite=Strict και Path=/. Η διάρκεια είναι 30 λεπτά, χωρίς sliding renewal. Το ASP.NET Core Data Protection προστατεύει το cookie. Κάθε αίτημα ελέγχει ξανά το security stamp και την ύπαρξη ενεργού membership.

Το rate limiting επιτρέπει έως 10 αιτήματα authentication ανά λεπτό ανά IP και επιστρέφει HTTP 429 σε υπέρβαση. Παραμένει το υπάρχον Identity lockout: 5 λανθασμένοι κωδικοί / 15 λεπτά.

{% hint style="info" %}
Οι λειτουργίες δεν αλλάζουν επιχειρησιακά δεδομένα ή migrations. Ωστόσο η επαλήθευση password μπορεί να ενημερώνει τον Identity access-failure counter.
{% endhint %}

## 8. CLI, logging και troubleshooting

Η υπάρχουσα εντολή CLI παραμένει διαθέσιμη για διάγνωση κωδικού χωρίς cookie:

~~~powershell
dotnet run --project src/Aenaon.Api --launch-profile https -- --verify-credentials --username admin --confirm-database AenaonERP_Dev
~~~

| HTTP status | Σημασία / έλεγχος |
| --- | --- |
| 400 | Χωρίς έγκυρο CSRF token ή μη έγκυρη αίτηση |
| 401 | Χωρίς έγκυρη συνεδρία ή μη αποδεκτός κωδικός |
| 403 | Ο χρήστης δεν έχει το συγκεκριμένο δικαίωμα |
| 404 | Το auth API δεν χαρτογραφήθηκε εκτός Development |
| 429 | Εξαντλήθηκε το rate limit, προσπάθησε αργότερα |

Στο πρόβλημα με cookies χρησιμοποίησε το ίδιο WebRequestSession και HTTPS origin. Για αποτυχία SQL έλεγξε το --check-database χωρίς να εμφανίσεις connection strings ή passwords. Για διακοπή του API πάτησε Ctrl+C. Η εφαρμογή δεν πρέπει να καταγράφει κωδικούς, cookies ή CSRF token values.

## 9. Checklist

- [ ] GitHub Actions Windows, Linux, SQL integration tests επιτυχή.
- [ ] Τοπικό Release build και tests επιτυχή.
- [ ] GET /api/auth/csrf επιστρέφει request token.
- [ ] Login χωρίς CSRF επιστρέφει 400.
- [ ] Login με σωστό κωδικό εκδίδει Secure HttpOnly session cookie.
- [ ] GET /api/auth/me επιστρέφει τον σωστό Administrator και την Aenaon Demo.
- [ ] GET /api/auth/access επιτρέπει MAIN/Manage.
- [ ] Ξένο StoreId απορρίπτεται.
- [ ] Logout με νέο CSRF token επιστρέφει 204.
- [ ] Μετά logout το /me επιστρέφει 401.
- [ ] Δεν υπάρχουν κωδικοί ή tokens σε logs.

## 10. Παραγωγική λειτουργία και επόμενα βήματα

Πριν από Production χρειάζονται διαχείριση Data Protection keys για κάθε instance, ασφαλής reverse-proxy διαμόρφωση, staging tests, παρακολούθηση/login audit, CSRF στο frontend, email confirmation, MFA όπου ενδείκνυται και tenant-aware authorization για όλα τα business endpoints. Επόμενο βήμα: Blazor login UI, σχεδιασμένο να χρησιμοποιεί τα ασφαλή cookie endpoints.
