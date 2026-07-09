# Run this script after every Azure Container Apps deployment.
#
# WHY (web sticky sessions): Blazor Server keeps circuit state on one server instance
# per SignalR connection. Without sticky session affinity, ACA routes WebSocket frames
# to different replicas; the circuit never gets its state updates back to the browser,
# so button clicks appear to do nothing. Aspire 13.x does not configure this automatically.
# Sticky sessions are NOT embedded in the Aspire deployment template and must be set
# via CLI after every deploy.
#
# NOTE (min-replicas and Kafka max-replicas): PublishAsAzureContainerApp requires
# AddAzureContainerAppEnvironment which conflicts with the VS publish wizard and breaks
# the deploy. These settings must be applied via CLI after every deploy (steps below).
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

# ── 3. Keep all application services at min 1 replica (prevents cold starts) ─
$services = @("identity-api", "trips-api", "booking-api", "notification-api", "web")
foreach ($svc in $services) {
    Write-Host "Setting min-replicas=1 on $svc..." -ForegroundColor Cyan
    az containerapp update `
        --name $svc `
        --resource-group $ResourceGroup `
        --min-replicas 1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
        $allOk = $false
    } else {
        Write-Host "  OK - $svc will always keep at least 1 replica running." -ForegroundColor Green
    }
}

if ($allOk) {
    Write-Host "`nAll post-deploy steps completed successfully." -ForegroundColor Green
} else {
    Write-Host "`nOne or more steps failed. Check that you are logged in: az login" -ForegroundColor Red
    exit 1
}
