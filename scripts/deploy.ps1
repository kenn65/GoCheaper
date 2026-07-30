#Requires -Version 7
<#
.SYNOPSIS
    Builds Docker images for all GoCheaper services using `dotnet publish`,
    pushes them to ACR, and applies all Kubernetes manifests.

.PARAMETER AcrName
    Azure Container Registry name (default: gocheaperregistry)

.PARAMETER Tag
    Image tag (default: latest)

.PARAMETER SkipBuild
    Skip dotnet publish / docker push — only apply K8s manifests.

.EXAMPLE
    .\scripts\deploy.ps1
    .\scripts\deploy.ps1 -SkipBuild    # re-apply manifests without rebuilding
#>
param(
    [string]$AcrName   = "gocheaperregistry",
    [string]$Tag       = "latest",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Join-Path $PSScriptRoot ".."
$acrLogin = "$AcrName.azurecr.io"

$services = @(
    @{ Name = "identity-api";    Project = "src/GoCheaper.Identity.Api/GoCheaper.Identity.Api.csproj" },
    @{ Name = "notification-api"; Project = "src/GoCheaper.Notification.Api/GoCheaper.Notification.Api.csproj" },
    @{ Name = "trips-api";        Project = "src/GoCheaper.Trips.Api/GoCheaper.Trips.Api.csproj" },
    @{ Name = "booking-api";      Project = "src/GoCheaper.Booking.Api/GoCheaper.Booking.Api.csproj" },
    @{ Name = "web";              Project = "src/GoCheaper.Web/GoCheaper.Web.csproj" }
)

if (-not $SkipBuild) {
    Write-Host "=== Logging in to ACR ===" -ForegroundColor Cyan
    az acr login --name $AcrName

    foreach ($svc in $services) {
        $image = "$acrLogin/$($svc.Name):$Tag"
        Write-Host "`n=== Building $($svc.Name) → $image ===" -ForegroundColor Cyan

        Push-Location $repoRoot
        dotnet publish $svc.Project `
            /t:PublishContainer `
            /p:ContainerImageName="$acrLogin/$($svc.Name)" `
            /p:ContainerImageTag="$Tag" `
            /p:ContainerRegistry="$acrLogin"
        Pop-Location

        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed for $($svc.Name)"
        }
        Write-Host "     Pushed $image" -ForegroundColor Green
    }
}

Write-Host "`n=== Applying Kubernetes manifests ===" -ForegroundColor Cyan
$k8sRoot = Join-Path $repoRoot "k8s"

# Apply in dependency order
$manifests = @(
    "namespace.yaml",
    "configmap.yaml",
    "sqlserver/statefulset.yaml",
    "sqlserver/service.yaml",
    "kafka/statefulset.yaml",
    "kafka/service.yaml",
    "identity-api/deployment.yaml",
    "identity-api/service.yaml",
    "notification-api/deployment.yaml",
    "notification-api/service.yaml",
    "trips-api/deployment.yaml",
    "trips-api/service.yaml",
    "booking-api/deployment.yaml",
    "booking-api/service.yaml",
    "web/deployment.yaml",
    "web/service.yaml",
    "ingress.yaml"
)

foreach ($manifest in $manifests) {
    $path = Join-Path $k8sRoot $manifest
    Write-Host "  kubectl apply -f $manifest"
    kubectl apply -f $path
}

Write-Host "`n=== Waiting for rollout ===" -ForegroundColor Cyan
$deployments = @("identity-api", "notification-api", "trips-api", "booking-api", "web")
foreach ($d in $deployments) {
    Write-Host "  Waiting for $d..."
    kubectl rollout status deployment/$d --namespace gocheaper --timeout=180s
}

Write-Host "`n=== Getting ingress public IP ===" -ForegroundColor Cyan
$ip = kubectl get svc ingress-nginx-controller --namespace ingress-nginx -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>$null
if ($ip) {
    Write-Host "  App is available at: http://$ip" -ForegroundColor Green
    Write-Host "  Update WebApp__BaseUrl in k8s/configmap.yaml to: http://$ip" -ForegroundColor Yellow
    Write-Host "  Then re-run: kubectl apply -f k8s/configmap.yaml && kubectl rollout restart deployment/notification-api -n gocheaper" -ForegroundColor Yellow
} else {
    Write-Host "  Ingress IP not yet assigned. Run:" -ForegroundColor Yellow
    Write-Host "  kubectl get svc ingress-nginx-controller -n ingress-nginx" -ForegroundColor Yellow
}

Write-Host "`n=== Deploy complete! ===" -ForegroundColor Cyan
