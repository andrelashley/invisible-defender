[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [string]$ResourceGroup = 'rg-football-hackathon',
    [string]$FoundryResource,
    [string]$Location,
    [string]$EnvironmentName = 'invisible-defender-env',
    [string]$RegistryName,
    [string]$AppName = 'invisible-defender',
    [switch]$Apply,
    [switch]$BillingApproved
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function AzJson {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $ErrorActionPreference = 'Continue'
    $scopeArgs = if ($Arguments[0] -eq 'extension') { @() } else { @('--subscription', $SubscriptionId) }
    $result = & az @Arguments @scopeArgs --only-show-errors -o json
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI failed: $($Arguments[0..([Math]::Min(2, $Arguments.Length - 1))] -join ' ')" }
    if ($result) { return ($result | ConvertFrom-Json) }
}
function Native {
    param([string]$Command, [Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    # Windows PowerShell treats redirected native stderr (including build warnings)
    # as errors. Native process exit codes remain the failure authority here.
    $ErrorActionPreference = 'Continue'
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE)." }
}
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $account = AzJson account show
    if ($account.id -ne $SubscriptionId -or $account.state -ne 'Enabled') { throw 'Select the intended enabled subscription with az account set first.' }
    $group = AzJson group show -n $ResourceGroup
    $inventory = @(AzJson resource list -g $ResourceGroup)
    $inventory | Select-Object name,type,location | Format-Table | Out-Host
    if (!$FoundryResource) {
        $candidates = @($inventory | Where-Object type -eq 'Microsoft.CognitiveServices/accounts')
        if ($candidates.Count -ne 1) { throw 'Specify -FoundryResource from the discovered inventory.' }
        $FoundryResource = $candidates[0].name
    }
    $foundry = AzJson cognitiveservices account show -g $ResourceGroup -n $FoundryResource
    if ($foundry.kind -notin @('AIServices','OpenAI')) { throw 'Expected an Azure OpenAI-compatible Cognitive Services account.' }
    $deployment = AzJson cognitiveservices account deployment show -g $ResourceGroup -n $FoundryResource --deployment-name gpt-4.1-mini-1
    if ($deployment.properties.provisioningState -ne 'Succeeded' -or $deployment.properties.model.name -ne 'gpt-4.1-mini') { throw 'Existing model deployment is not ready.' }
    $endpoint = $foundry.properties.endpoints.'OpenAI Language Model Instance API'
    if (!$endpoint) { throw 'No discovered Azure OpenAI endpoint; inspect the resource before continuing.' }
    $endpoint = $endpoint.TrimEnd('/') + '/openai/v1/'
    if (!$Location) { $Location = $foundry.location }
    $registries = @($inventory | Where-Object type -eq 'Microsoft.ContainerRegistry/registries')
    if (!$RegistryName) {
        if ($registries.Count -eq 1) { $RegistryName = $registries[0].name }
        elseif ($registries.Count -gt 1) { throw 'Specify the registry to reuse.' }
        else { throw 'Specify a globally unique -RegistryName for the proposed Basic registry.' }
    }
    if ($RegistryName -notin @($registries | Select-Object -ExpandProperty name)) {
        $availability = AzJson acr check-name -n $RegistryName
        if (!$availability.nameAvailable) { throw 'Proposed registry name is unavailable. Choose another globally unique name.' }
    }
    $environments = @($inventory | Where-Object type -eq 'Microsoft.App/managedEnvironments')
    if ($environments.Count -gt 0 -and $EnvironmentName -notin @($environments | Select-Object -ExpandProperty name)) { throw 'Existing environments found. Select a suitable -EnvironmentName rather than creating another.' }
    $role = @(AzJson role definition list --name 'Cognitive Services OpenAI User')[0]
    if ('Microsoft.CognitiveServices/accounts/OpenAI/deployments/chat/completions/action' -notin $role.permissions[0].dataActions) { throw 'Inference role does not grant expected chat permission.' }
    $extensions = @(AzJson extension list)
    Write-Host "Subscription: $($account.name) ($SubscriptionId)"
    Write-Host "Plan: reuse $FoundryResource / gpt-4.1-mini-1; environment $EnvironmentName in $Location; Basic ACR $RegistryName; one app $AppName."
    Write-Host 'Consumption 0.25 vCPU / 0.5Gi, min=max=1, single revision, HTTPS 8080, system identity. No Log Analytics workspace.'
    Write-Host 'Costs: active/idle compute + memory, ACR daily storage tier, requests/egress, and explicit Foundry tokens. See docs/DEPLOYMENT.md.'
    Write-Host "Containerapp extension present: $([bool]($extensions | Where-Object name -eq 'containerapp'))"
    if (!$Apply) { Write-Host 'Discovery only. No resources changed.'; return }
    if (!$BillingApproved) { throw 'Apply requires explicit user approval for this plan, then -BillingApproved.' }
    if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker is required. Install/start a Linux container engine and rerun. No resources created.' }
    Native docker info
    # Complete local production verification before creating or updating cloud resources.
    Native dotnet build src/api/InvisibleDefender.slnx
    Native dotnet run --project tests/Analytics.Tests --no-build
    Native npm.cmd --prefix src/web ci
    Native npm.cmd --prefix src/web run build
    Native npm.cmd --prefix src/web test
    $tag = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    $localImage = "invisible-defender:$tag"
    Native docker build --pull -t $localImage .
    $container = 'invisible-defender-smoke-' + $tag
    Native docker run --detach --name $container --publish 127.0.0.1:8080:8080 --env Narration__Provider=Disabled $localImage
    try {
        $ready = $false
        for ($i=0; $i -lt 30; $i++) {
            try { $null = Invoke-RestMethod http://127.0.0.1:8080/api/health; $ready = $true; break } catch { Start-Sleep -Seconds 1 }
        }
        if (!$ready) { throw 'Local container failed readiness.' }
        $env:API_URL = 'http://127.0.0.1:8080'
        Native node tests/Integration/smoke.mjs
        Native node tests/Integration/production.mjs
        $env:PITCH_API_URL = $env:API_URL
        try { Native npm.cmd --prefix src/web test } finally { Remove-Item Env:PITCH_API_URL -ErrorAction SilentlyContinue }
        Native node tests/Integration/explanations.mjs
    } finally { & docker rm -f $container | Out-Null; Remove-Item Env:API_URL -ErrorAction SilentlyContinue }
    if (!($extensions | Where-Object name -eq 'containerapp')) { $null = AzJson extension add -n containerapp }
    foreach ($namespace in @('Microsoft.App','Microsoft.ContainerRegistry','Microsoft.OperationalInsights')) {
        $provider = AzJson provider show -n $namespace
        if ($provider.registrationState -ne 'Registered') { $null = AzJson provider register -n $namespace --wait }
    }
    if ($RegistryName -notin @($registries | Select-Object -ExpandProperty name)) { $null = AzJson acr create -g $ResourceGroup -n $RegistryName -l $Location --sku Basic --admin-enabled false }
    $registry = AzJson acr show -g $ResourceGroup -n $RegistryName
    if ($registry.PSObject.Properties['roleAssignmentMode'] -and $registry.roleAssignmentMode -eq 'AbacRepositoryPermissions') { throw 'This script requires an RBAC registry with AcrPull. Do not change shared registry permissions blindly.' }
    $image = "$($registry.loginServer)/invisible-defender:$tag"
    Native az acr login -n $RegistryName --subscription $SubscriptionId --only-show-errors
    Native docker tag $localImage $image
    Native docker push $image
    if ($EnvironmentName -notin @($environments | Select-Object -ExpandProperty name)) {
        $null = AzJson containerapp env create -g $ResourceGroup -n $EnvironmentName -l $Location --enable-workload-profiles true --logs-destination none
    }
    $environment = AzJson containerapp env show -g $ResourceGroup -n $EnvironmentName
    if (!($environment.properties.workloadProfiles | Where-Object workloadProfileType -eq 'Consumption')) { throw 'Environment has no Consumption profile.' }
    if ($AppName -notin @($inventory | Where-Object type -eq 'Microsoft.App/containerApps' | Select-Object -ExpandProperty name)) {
        # Bootstrap only this app with internal ingress to establish its system identity before private ACR pull.
        $null = AzJson containerapp create -g $ResourceGroup -n $AppName --environment $environment.id --image mcr.microsoft.com/k8se/quickstart:latest --ingress internal --target-port 80 --transport auto --system-assigned --cpu 0.25 --memory 0.5Gi --min-replicas 1 --max-replicas 1 --workload-profile-name Consumption --revisions-mode single
    }
    $identity = AzJson containerapp identity assign -g $ResourceGroup -n $AppName --system-assigned
    foreach ($grant in @(@{Scope=$registry.id;Role='AcrPull'}, @{Scope=$foundry.id;Role='Cognitive Services OpenAI User'})) {
        $existing = @(AzJson role assignment list --scope $grant.Scope --query "[?principalId=='$($identity.principalId)' && roleDefinitionName=='$($grant.Role)']")
        if (!$existing.Count) { $null = AzJson role assignment create --assignee-object-id $identity.principalId --assignee-principal-type ServicePrincipal --role $grant.Role --scope $grant.Scope }
    }
    $null = AzJson containerapp registry set -g $ResourceGroup -n $AppName --server $registry.loginServer --identity system
    $null = AzJson containerapp revision set-mode -g $ResourceGroup -n $AppName --mode single
    $null = AzJson containerapp update -g $ResourceGroup -n $AppName --image $image --cpu 0.25 --memory 0.5Gi --min-replicas 1 --max-replicas 1 --set-env-vars ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080 Narration__Provider=Foundry "Narration__Endpoint=$endpoint" Narration__Deployment=gpt-4.1-mini-1 Narration__ManagedIdentity=true
    $null = AzJson containerapp ingress enable -g $ResourceGroup -n $AppName --type external --target-port 8080 --transport auto --allow-insecure false
    $app = AzJson containerapp show -g $ResourceGroup -n $AppName
    $url = "https://$($app.properties.configuration.ingress.fqdn)"
    $null = AzJson containerapp update -g $ResourceGroup -n $AppName --set-env-vars "PublicOrigin=$url"
    Write-Host "Public URL (not yet verified): $url"
    # A ready revision can precede public ingress route propagation.
    $publicReady = $false
    for ($i=0; $i -lt 30; $i++) {
        try {
            $health = Invoke-RestMethod "$url/api/health" -TimeoutSec 10
            if ($health.status -eq 'ok') { $publicReady = $true; break }
        } catch { }
        Start-Sleep -Seconds 2
    }
    if (!$publicReady) { throw 'Public HTTPS health did not become ready; inspect ingress and revision status.' }
    $env:API_URL = $url
    Native node tests/Integration/smoke.mjs
    Native node tests/Integration/production.mjs
    Native node tests/Integration/foundry-public.mjs
    Write-Host "Public automated smoke tests passed: $url. Complete browser checklist in docs/DEPLOYMENT.md."
    Write-Host "Logs: az containerapp logs show -g $ResourceGroup -n $AppName --follow --subscription $SubscriptionId"
} finally { Remove-Item Env:API_URL -ErrorAction SilentlyContinue; Pop-Location }
