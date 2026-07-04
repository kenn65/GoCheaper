# ============================================================
# GoCheaper — Azure Communication Services Email Setup
# ============================================================
# Creates:
#   - Resource group
#   - Email Communication Services resource  (gocheaper)
#   - Azure-managed sender domain            (gocheaper.azurecomm.net)
#   - Communication Services resource        (gocheaper-comm)
#   - Links the domain to the comm resource
#   - Sets user secrets for Notification.Api
#
# Requirements: Azure CLI (az) installed and logged in
#   az login
#
# Usage:
#   .\scripts\setup-azure-email.ps1
#   .\scripts\setup-azure-email.ps1 -ResourceGroup "my-rg" -DataLocation "unitedstates"
# ============================================================

param(
    [string] $ResourceGroup  = "gocheaper-rg",
    [string] $Location       = "westeurope",     # Azure region for the resource group
    [string] $DataLocation   = "europe",         # Data residency (europe / unitedstates / etc.)
    [string] $EmailService   = "gocheaper",      # Determines sender domain: gocheaper.azurecomm.net
    [string] $CommService    = "gocheaper-comm", # ACS resource (connection string comes from here)
    [string] $FromEmail      = "donotreply@gocheaper.azurecomm.net"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── 1. Verify Azure CLI login ────────────────────────────────
Write-Host "`n[1/7] Checking Azure CLI login..." -ForegroundColor Cyan
$account = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not logged in to Azure CLI. Run 'az login' first."
    exit 1
}
$accountObj = $account | ConvertFrom-Json
Write-Host "  Logged in as: $($accountObj.user.name) | Subscription: $($accountObj.name)" -ForegroundColor Green

# ── 2. Install required CLI extensions ─────────────���────────
Write-Host "`n[2/7] Ensuring CLI extensions are installed..." -ForegroundColor Cyan
$extensions = az extension list --query "[].name" -o tsv
if ($extensions -notcontains "communication") {
    Write-Host "  Installing 'communication' extension..."
    az extension add --name communication --yes
} else {
    Write-Host "  'communication' extension already installed." -ForegroundColor Green
}

# ── 3. Create resource group ────────────────────────────────
Write-Host "`n[3/7] Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Cyan
az group create --name $ResourceGroup --location $Location | Out-Null
Write-Host "  Done." -ForegroundColor Green

# ── 4. Create Email Communication Services resource ─────────
Write-Host "`n[4/7] Creating Email Communication Services '$EmailService'..." -ForegroundColor Cyan
az communication email show --name $EmailService --resource-group $ResourceGroup 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Already exists — skipping." -ForegroundColor Yellow
} else {
    az communication email create `
        --name          $EmailService `
        --resource-group $ResourceGroup `
        --location      "global" `
        --data-location $DataLocation | Out-Null
    Write-Host "  Done." -ForegroundColor Green
}

# ── 5. Provision Azure-managed sender domain ────────────────
Write-Host "`n[5/7] Provisioning Azure-managed domain '$EmailService.azurecomm.net'..." -ForegroundColor Cyan
az communication email domain show `
    --domain-name       "AzureManagedDomain" `
    --email-service-name $EmailService `
    --resource-group    $ResourceGroup 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Domain already provisioned — skipping." -ForegroundColor Yellow
} else {
    az communication email domain create `
        --domain-name       "AzureManagedDomain" `
        --email-service-name $EmailService `
        --resource-group    $ResourceGroup `
        --location          "global" `
        --domain-management "AzureManaged" | Out-Null
    Write-Host "  Done." -ForegroundColor Green
}

# Get domain resource ID for linking
$domainId = az communication email domain show `
    --domain-name       "AzureManagedDomain" `
    --email-service-name $EmailService `
    --resource-group    $ResourceGroup `
    --query id -o tsv

# ── 6. Create Azure Communication Services resource ─────────
Write-Host "`n[6/7] Creating Communication Services resource '$CommService'..." -ForegroundColor Cyan
az communication show --name $CommService --resource-group $ResourceGroup 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Already exists — linking domain..." -ForegroundColor Yellow
    az communication update `
        --name           $CommService `
        --resource-group $ResourceGroup `
        --linked-domains $domainId | Out-Null
} else {
    az communication create `
        --name           $CommService `
        --resource-group $ResourceGroup `
        --location       "global" `
        --data-location  $DataLocation `
        --linked-domains $domainId | Out-Null
    Write-Host "  Done." -ForegroundColor Green
}

# ── 7. Retrieve connection string and set user secrets ───────
Write-Host "`n[7/7] Retrieving connection string and setting user secrets..." -ForegroundColor Cyan
$connectionString = az communication list-key `
    --name           $CommService `
    --resource-group $ResourceGroup `
    --query          primaryConnectionString -o tsv

if ([string]::IsNullOrEmpty($connectionString)) {
    Write-Error "Failed to retrieve connection string from '$CommService'."
    exit 1
}

$projectPath = Join-Path $PSScriptRoot "..\src\GoCheaper.Notification.Api"

dotnet user-secrets set "AzureCommunicationServices:ConnectionString" $connectionString --project $projectPath
dotnet user-secrets set "AzureCommunicationServices:FromEmail"        $FromEmail        --project $projectPath

# ── Summary ──────────────────────────────────────────────────
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "  Setup complete!" -ForegroundColor Green
Write-Host "  Resource group : $ResourceGroup"
Write-Host "  Email service  : $EmailService"
Write-Host "  Comm service   : $CommService"
Write-Host "  Sender address : $FromEmail"
Write-Host "  User secrets   : set on GoCheaper.Notification.Api"
Write-Host "============================================================`n" -ForegroundColor Cyan
Write-Host "Restart GoCheaper.AppHost - emails will now go via Azure ACS." -ForegroundColor Green
