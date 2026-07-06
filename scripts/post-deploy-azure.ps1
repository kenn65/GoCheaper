# Run this script after every Azure Container Apps deployment.
#
# WHY: Aspire's default ACA bicep sets maxReplicas: 10 for all containers.
# Multiple Kafka (KRaft-mode) instances fight over controller state and crash,
# losing all topic metadata. Locking to exactly 1 replica prevents this.
#
# USAGE: .\scripts\post-deploy-azure.ps1
#   Optional: override resource group with -ResourceGroup "rg-MyEnv"

param(
    [string]$ResourceGroup = "rg-AzureTest"
)

Write-Host "Locking Kafka to a single replica in '$ResourceGroup'..." -ForegroundColor Cyan

az containerapp update `
    --name kafka `
    --resource-group $ResourceGroup `
    --min-replicas 1 `
    --max-replicas 1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Done. Kafka is now locked to min=1 / max=1 replica." -ForegroundColor Green
} else {
    Write-Host "az command failed (exit code $LASTEXITCODE). Check that you are logged in: az login" -ForegroundColor Red
}
