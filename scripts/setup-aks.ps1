#Requires -Version 7
<#
.SYNOPSIS
    Provisions the AKS cluster, attaches ACR, and installs the NGINX ingress controller.
    Run once before the first deploy.

.PARAMETER ResourceGroup
    Azure resource group name (default: GoCheaper)

.PARAMETER ClusterName
    AKS cluster name (default: GoCheaper-Cluster)

.PARAMETER AcrName
    Azure Container Registry name — must be lowercase alphanumeric (default: gocheaperregistry)

.PARAMETER Location
    Azure region (default: swedencentral)

.EXAMPLE
    .\scripts\setup-aks.ps1
#>
param(
    [string]$ResourceGroup = "GoCheaper",
    [string]$ClusterName   = "GoCheaper-Cluster",
    [string]$AcrName       = "gocheaperregistry",
    [string]$Location      = "swedencentral"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== GoCheaper AKS Setup ===" -ForegroundColor Cyan

# 1. Create resource group
Write-Host "`n[1/6] Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "     Done." -ForegroundColor Green

# 2. Create ACR
Write-Host "`n[2/6] Creating ACR '$AcrName'..." -ForegroundColor Yellow
az acr create --resource-group $ResourceGroup --name $AcrName --sku Basic --output none
Write-Host "     Done." -ForegroundColor Green

# 3. Create AKS cluster (Standard_D2s_v3 = 2 vCPU, 8 GiB — enough for all services)
Write-Host "`n[3/6] Creating AKS cluster '$ClusterName' (this takes ~5 minutes)..." -ForegroundColor Yellow
az aks create `
    --resource-group $ResourceGroup `
    --name $ClusterName `
    --node-count 2 `
    --node-vm-size Standard_D2s_v3 `
    --generate-ssh-keys `
    --location $Location `
    --output none
Write-Host "     Done." -ForegroundColor Green

# 4. Attach ACR to AKS so nodes can pull images without explicit credentials
Write-Host "`n[4/6] Attaching ACR to AKS..." -ForegroundColor Yellow
az aks update --resource-group $ResourceGroup --name $ClusterName --attach-acr $AcrName --output none
Write-Host "     Done." -ForegroundColor Green

# 5. Get credentials
Write-Host "`n[5/6] Fetching kubeconfig..." -ForegroundColor Yellow
az aks get-credentials --resource-group $ResourceGroup --name $ClusterName --overwrite-existing
Write-Host "     Done." -ForegroundColor Green

# 6. Install NGINX ingress controller via Helm
Write-Host "`n[6/6] Installing NGINX ingress controller..." -ForegroundColor Yellow
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx 2>$null
helm repo update ingress-nginx

helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx `
    --namespace ingress-nginx `
    --create-namespace `
    --set controller.replicaCount=1 `
    --set controller.nodeSelector."kubernetes\.io/os"=linux `
    --wait
Write-Host "     Done." -ForegroundColor Green

Write-Host "`n=== Setup complete! ===" -ForegroundColor Cyan
Write-Host "Next steps:"
Write-Host "  1. Copy k8s/secrets.template.yaml to k8s/secrets.yaml and fill in your values"
Write-Host "  2. Run .\scripts\create-secrets.ps1 (or kubectl apply -f k8s/secrets.yaml)"
Write-Host "  3. Run .\scripts\deploy.ps1"
