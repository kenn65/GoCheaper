# Run this script after every Azure Container Apps deployment.
#
# WHY (Kafka): Aspire's default ACA bicep sets maxReplicas: 10 for all containers.
# Multiple Kafka (KRaft-mode) instances fight over controller state and crash,
# losing all topic metadata. Locking to exactly 1 replica prevents this.
#
# WHY (web sticky sessions): Blazor Server keeps circuit state on one server instance
# per SignalR connection. Without sticky session affinity, ACA routes WebSocket frames
# to different replicas; the circuit never gets its state updates back to the browser,
# so button clicks appear to do nothing. Aspire 13.x does not configure this automatically.
#
# USAGE: .\scripts\post-deploy-azure.ps1
#   Optional: override resource group with -ResourceGroup "rg-MyEnv"

param(
    [string]$ResourceGroup = "rg-AzureTest"
)

$ErrorActionPreference = "Stop"
$allOk = $true

# ── 1. Lock Kafka to a single replica ────────────────────────────────────────
Write-Host "Locking Kafka to a single replica..." -ForegroundColor Cyan
az containerapp update `
    --name kafka `
    --resource-group $ResourceGroup `
    --min-replicas 1 `
    --max-replicas 1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    $allOk = $false
} else {
    Write-Host "  OK - Kafka locked to min=1 / max=1 replica." -ForegroundColor Green
}

# ── 2. Enable sticky sessions on the web app (required for Blazor Server) ────
Write-Host "Enabling sticky session affinity on web..." -ForegroundColor Cyan
az containerapp ingress sticky-sessions set `
    --name web `
    --resource-group $ResourceGroup `
    --affinity sticky
if ($LASTEXITCODE -ne 0) {
    Write-Host "  FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    $allOk = $false
} else {
    Write-Host "  OK - Sticky sessions enabled on web." -ForegroundColor Green
}

if ($allOk) {
    Write-Host "`nAll post-deploy steps completed successfully." -ForegroundColor Green
} else {
    Write-Host "`nOne or more steps failed. Check that you are logged in: az login" -ForegroundColor Red
    exit 1
}
