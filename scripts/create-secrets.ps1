#Requires -Version 7
<#
.SYNOPSIS
    Reads secrets from AppHost/appsettings.Development.json and creates the
    gocheaper-secrets Kubernetes Secret in the gocheaper namespace.

    Run this once after setup-aks.ps1, and again whenever you rotate secrets.

.PARAMETER SqlSaPassword
    SQL Server SA password. If omitted you will be prompted.
#>
param(
    [string]$SqlSaPassword = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$settingsPath = Join-Path $PSScriptRoot "..\src\GoCheaper.AppHost\appsettings.Development.json"
if (-not (Test-Path $settingsPath)) {
    Write-Error "Could not find $settingsPath — make sure you are running from the repo root."
}

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$p = $settings.Parameters

if (-not $SqlSaPassword) {
    $SqlSaPassword = Read-Host -Prompt "Enter SQL Server SA password (same as in Aspire local secrets)"
}

# Build connection strings — SQL Server runs as 'sqlserver' service in gocheaper namespace
$sqlHost    = "sqlserver.gocheaper.svc.cluster.local,1433"
$connBase   = "Server=$sqlHost;User Id=sa;Password=$SqlSaPassword;TrustServerCertificate=True"
$connIdentity = "$connBase;Database=identitydb"
$connTrips    = "$connBase;Database=tripsdb"
$connBooking  = "$connBase;Database=bookingdb"
$connWeb      = "$connBase;Database=webdb"

Write-Host "Creating namespace 'gocheaper' (if not exists)..." -ForegroundColor Yellow
kubectl apply -f (Join-Path $PSScriptRoot "..\k8s\namespace.yaml")

Write-Host "Creating/updating gocheaper-secrets..." -ForegroundColor Yellow

# Delete existing secret (ignore error if it doesn't exist)
kubectl delete secret gocheaper-secrets --namespace gocheaper 2>$null

kubectl create secret generic gocheaper-secrets `
    --namespace gocheaper `
    --from-literal=sql-sa-password="$SqlSaPassword" `
    --from-literal=jwt-key="$($p.'jwt-key')" `
    --from-literal=identity-api-key="$($p.'identity-api-key')" `
    --from-literal=trips-api-key="$($p.'trips-api-key')" `
    --from-literal=booking-api-key="$($p.'booking-api-key')" `
    --from-literal=notification-api-key="$($p.'notification-api-key')" `
    --from-literal=smtp-username="$($p.'smtp-username')" `
    --from-literal=smtp-password="$($p.'smtp-password')" `
    --from-literal=smtp-from-email="$($p.'smtp-from-email')" `
    --from-literal=conn-identitydb="$connIdentity" `
    --from-literal=conn-tripsdb="$connTrips" `
    --from-literal=conn-bookingdb="$connBooking" `
    --from-literal=conn-webdb="$connWeb"

Write-Host "Done. Secret 'gocheaper-secrets' created in namespace 'gocheaper'." -ForegroundColor Green
