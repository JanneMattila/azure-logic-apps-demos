Param (
    [Parameter(HelpMessage = "Deployment target resource group")]
    [string] $ResourceGroupName = "rg-logic-apps",

    [Parameter(HelpMessage = "SharePoint Site URL")]
    [string] $Site = "https://contoso.sharepoint.com/sites/myteam",

    [Parameter(HelpMessage = "SharePoint List Name")]
    [string] $List = "Mapping",
    
    [Parameter(HelpMessage = "Deployment target resource group location")]
    [string] $Location = "swedencentral",

    [string] $Template = "main.bicep"
)

$ErrorActionPreference = "Stop"

$date = (Get-Date).ToString("yyyy-MM-dd-HH-mm-ss")
$deploymentName = "Local-$date"

if ([string]::IsNullOrEmpty($env:RELEASE_DEFINITIONNAME)) {
    Write-Host (@"
Not executing inside Azure DevOps Release Management.
Make sure you have done "Login-AzAccount" and
"Select-AzSubscription -SubscriptionName name"
so that script continues to work correctly for you.
"@)
}
else {
    $deploymentName = $env:RELEASE_RELEASENAME
}

# Target deployment resource group
if ($null -eq (Get-AzResourceGroup -Name $ResourceGroupName -Location $Location -ErrorAction SilentlyContinue)) {
    Write-Warning "Resource group '$ResourceGroupName' doesn't exist and it will be created."
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location -Verbose
}

# Additional parameters that we pass to the template deployment
$additionalParameters = New-Object -TypeName hashtable

$additionalParameters['site'] = $Site
$additionalParameters['list'] = $List

$result = New-AzResourceGroupDeployment `
    -DeploymentName $deploymentName `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile $Template `
    @additionalParameters `
    -Mode Incremental -Force `
    -Verbose

$result