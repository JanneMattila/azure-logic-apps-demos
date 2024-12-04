Set-Location .\mapping

Connect-AzAccount

$site = "https://contoso.sharepoint.com/sites/myteam"
$list = "Mapping"

$deployment = .\deploy.ps1 -Site $site -List $list

# Authorize OAuth connections
# https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-deploy-azure-resource-manager-templates#authorize-oauth-connections

# Update .env file with the Logic App URI
"logicAppUri=$($deployment.outputs.uri.value)" | Out-File -FilePath .env

Remove-AzResourceGroup -Name $deployment.ResourceGroupName -Force
