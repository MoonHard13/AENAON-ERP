#requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Server,
    [ValidateNotNullOrEmpty()]
    [string]$Database = 'AenaonERP_Dev',
    [switch]$TrustServerCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Get-Command dotnet -ErrorAction Stop | Out-Null
$aenaonProject = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Aenaon.Api'
# Η ανάγνωση του project ολοκληρώνεται πριν σταλούν τα secrets μέσω stdin.
$aenaonSecretsId = (& dotnet msbuild (Join-Path $aenaonProject 'Aenaon.Api.csproj') '-getProperty:UserSecretsId' '-nologo' '-m:1' | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($aenaonSecretsId)) {
    throw 'Δεν εντοπίστηκε το UserSecretsId του API.'
}
$aenaonLogin = Read-Host 'SQL login που χρησιμοποιείς στο SSMS'
if ([string]::IsNullOrWhiteSpace($aenaonLogin)) {
    throw 'Το SQL login είναι υποχρεωτικό.'
}

$aenaonSecurePassword = Read-Host 'Κωδικός SQL login' -AsSecureString
$aenaonBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$aenaonPreviousEncoding = $OutputEncoding
$aenaonPassword = $null
$aenaonJson = $null
try {
    # Η τιμή εισάγεται τοπικά και μεταφέρεται στο επίσημο εργαλείο μέσω stdin.
    $aenaonPassword = [System.Net.NetworkCredential]::new('', $aenaonSecurePassword).Password
    if ([string]::IsNullOrEmpty($aenaonPassword)) {
        throw 'Ο κωδικός SQL login είναι υποχρεωτικός.'
    }
    $aenaonBuilder['Data Source'] = $Server
    $aenaonBuilder['Initial Catalog'] = $Database
    $aenaonBuilder['User ID'] = $aenaonLogin
    $aenaonBuilder['Password'] = $aenaonPassword
    $aenaonBuilder['Encrypt'] = 'True'
    $aenaonBuilder['TrustServerCertificate'] = $TrustServerCertificate.IsPresent
    $aenaonBuilder['Persist Security Info'] = $false
    $aenaonJson = @{ 'ConnectionStrings:Aenaon' = $aenaonBuilder.ConnectionString } | ConvertTo-Json -Compress
    # Τα Unicode δεδομένα διατηρούνται και με παλαιότερες κωδικοσελίδες κονσόλας.
    $aenaonJson = [regex]::Replace($aenaonJson, '[^\x00-\x7F]', [System.Text.RegularExpressions.MatchEvaluator] {
        param($aenaonMatch)
        '\u{0:x4}' -f [int][char]$aenaonMatch.Value
    })
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $aenaonJson | & dotnet user-secrets set --id $aenaonSecretsId | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Δεν ολοκληρώθηκε η αποθήκευση στα User Secrets.'
    }
    Write-Host 'Αποθηκεύτηκαν οι ρυθμίσεις ανάπτυξης. Εκτέλεσε τον έλεγχο --check-database.'
}
finally {
    $OutputEncoding = $aenaonPreviousEncoding
    $aenaonBuilder.Clear()
    $aenaonPassword = $null
    $aenaonJson = $null
    $aenaonSecurePassword.Dispose()
}
