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
# Τα διαπιστευτήρια εισάγονται αποκλειστικά από την κοινή εντολή .NET.
$aenaonArguments = @('run', '--project', $aenaonProject, '--launch-profile', 'https', '--', '--configure-database', '--server', $Server, '--database', $Database)
if ($TrustServerCertificate) {
    $aenaonArguments += '--trust-server-certificate'
}
& dotnet @aenaonArguments
exit $LASTEXITCODE
