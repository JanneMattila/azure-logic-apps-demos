Set-Location .\easyauth

# Login to Azure with account that can create app registrations
Connect-AzAccount

# Workflow application:
$integrationApp = New-AzADApplication -DisplayName "Integration Workflow" -SignInAudience AzureADMyOrg
Update-AzADApplication -InputObject $integrationApp -IdentifierUri "api://$($integrationApp.AppId)"
New-AzADServicePrincipal -ApplicationId $integrationApp.AppId

# Integration client application 1:
$integrationClientApp1 = New-AzADApplication -DisplayName "Integration Client 1" -SignInAudience AzureADMyOrg
New-AzADServicePrincipal -ApplicationId $integrationClientApp1.AppId
$integrationClientApp1Secret = New-AzADAppCredential -ObjectId $integrationClientApp1.Id -EndDate (Get-Date).AddYears(1)

# Integration client application 2:
$integrationClientApp2 = New-AzADApplication -DisplayName "Integration Client 2" -SignInAudience AzureADMyOrg
New-AzADServicePrincipal -ApplicationId $integrationClientApp2.AppId
$integrationClientApp2Secret = New-AzADAppCredential -ObjectId $integrationClientApp2.Id -EndDate (Get-Date).AddYears(1)

# Integration client application 3:
$integrationClientApp3 = New-AzADApplication -DisplayName "Integration Client 3 - Not enabled" -SignInAudience AzureADMyOrg
New-AzADServicePrincipal -ApplicationId $integrationClientApp3.AppId
$integrationClientApp3Secret = New-AzADAppCredential -ObjectId $integrationClientApp3.Id -EndDate (Get-Date).AddYears(1)

# Summary:
"Integration Workflow AppId: $($integrationApp.AppId)"
"Integration Client 1 AppId: $($integrationClientApp1.AppId)"
"Integration Client 1 Secret: $($integrationClientApp1Secret.SecretText)"
"Integration Client 2 AppId: $($integrationClientApp2.AppId)"
"Integration Client 2 Secret: $($integrationClientApp2Secret.SecretText)"
"Integration Client 3 AppId: $($integrationClientApp3.AppId)"
"Integration Client 3 Secret: $($integrationClientApp3Secret.SecretText)"

$clientId = $integrationApp.AppId
$integrationClientIds = @($integrationClientApp1.AppId, $integrationClientApp2.AppId)

# Deploy the EasyAuth Logic App:
$deployment = .\deploy.ps1 -ClientId $clientId -IntegrationClientIds $integrationClientIds
$deployment.outputs.uri.value

# Use portal to do workflow development.

# Grab the request URI from the Logic App:
$requestUri = "https://$($deployment.outputs.uri.value)/api/workflow1/triggers/request/invoke?api-version=2022-05-01"

# Integration Client 1 test:
$tenantId = (Get-AzContext).Tenant.Id
$clientPassword = ConvertTo-SecureString $integrationClientApp1Secret.SecretText -AsPlainText -Force
$credentials = New-Object System.Management.Automation.PSCredential($integrationClientApp1.AppId, $clientPassword)
Connect-AzAccount -ServicePrincipal -Credential $credentials -TenantId $tenantId

$integrationClient1Token = Get-AzAccessToken -Resource $integrationApp.AppId
$integrationClient1Token.Token
$integrationClient1Token.Token | Set-Clipboard
# Study in jwt.ms

Invoke-RestMethod `
    -Method Post `
    -Uri $requestUri `
    -ContentType "application/json" `
    -Headers @{"Authorization" = "Bearer $($integrationClient1Token.Token)" }

# Integration Client 2 test:
$tenantId = (Get-AzContext).Tenant.Id
$clientPassword = ConvertTo-SecureString $integrationClientApp2Secret.SecretText -AsPlainText -Force
$credentials = New-Object System.Management.Automation.PSCredential($integrationClientApp2.AppId, $clientPassword)
Connect-AzAccount -ServicePrincipal -Credential $credentials -TenantId $tenantId

$integrationClient2Token = Get-AzAccessToken -Resource $integrationApp.AppId
$integrationClient2Token.Token
$integrationClient2Token.Token | Set-Clipboard
# Study in jwt.ms

Invoke-RestMethod `
    -Method Post `
    -Uri $requestUri `
    -ContentType "application/json" `
    -Headers @{"Authorization" = "Bearer $($integrationClient2Token.Token)" }

# Integration Client 3 test:
$tenantId = (Get-AzContext).Tenant.Id
$clientPassword = ConvertTo-SecureString $integrationClientApp3Secret.SecretText -AsPlainText -Force
$credentials = New-Object System.Management.Automation.PSCredential($integrationClientApp3.AppId, $clientPassword)
Connect-AzAccount -ServicePrincipal -Credential $credentials -TenantId $tenantId

$integrationClient3Token = Get-AzAccessToken -Resource $integrationApp.AppId
$integrationClient3Token.Token
$integrationClient3Token.Token | Set-Clipboard
# Study in jwt.ms

# This will fail because the client is not enabled in the template!
Invoke-RestMethod `
    -Method Post `
    -Uri $requestUri `
    -ContentType "application/json" `
    -Headers @{"Authorization" = "Bearer $($integrationClient3Token.Token)" }
# -> Invoke-RestMethod: You do not have permission to view this directory or page. 