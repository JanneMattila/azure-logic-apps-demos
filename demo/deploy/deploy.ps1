Param (
    [Parameter(HelpMessage = "Deployment target resource group")] 
    [string] $ResourceGroupName = "rg-logicapp-standard-demo",

    [Parameter(HelpMessage = "Deployment target resource group location")] 
    [string] $Location = "North Europe",

    [Parameter(HelpMessage = "Logic App app Name")] 
    [string] $LogicAppName = "contosologicapp-local",

    [string] $Template = "azuredeploy.json",
    [string] $TemplateParameters = "$PSScriptRoot\azuredeploy.parameters.json"
)

$ErrorActionPreference = "Stop"

$date = (Get-Date).ToString("yyyy-MM-dd-HH-mm-ss")
$deploymentName = "Local-$date"

if ([string]::IsNullOrEmpty($env:GITHUB_SHA)) {
    Write-Host (@"
Not executing inside GitHub Action.
Make sure you have done "Login-AzAccount" and
"Select-AzSubscription -SubscriptionName name"
so that script continues to work correctly for you.
"@)
}
else {
    $deploymentName = $env:GITHUB_SHA
}

# Target deployment resource group
if ($null -eq (Get-AzResourceGroup -Name $ResourceGroupName -Location $Location -ErrorAction SilentlyContinue)) {
    Write-Warning "Resource group '$ResourceGroupName' doesn't exist and it will be created."
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location -Verbose
}

# Additional parameters that we pass to the template deployment
$additionalParameters = New-Object -TypeName hashtable
$additionalParameters['logicAppName'] = $LogicAppName

$result = New-AzResourceGroupDeployment `
    -DeploymentName $deploymentName `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile $Template `
    -TemplateParameterFile $TemplateParameters `
    @additionalParameters `
    -Mode Complete -Force `
    -Verbose

if ($null -eq $result.Outputs.apimGateway) {
    Throw "Template deployment didn't return web app information correctly and therefore deployment is cancelled."
}

$result | Select-Object -ExcludeProperty TemplateLinkString

$apimGateway = $result.Outputs.apimGateway.value

# Publish variable to the GitHub Action runner so that they
# can be used in follow-up tasks such as application deployment
Write-Host "::set-output name=LogicAppName::$($LogicAppName)"

$body = ConvertTo-Json @{
    "id"      = 123
    "name"    = "John"
    "phone"   = "+1234567890"
    "address" = @{
        "street"     = "Street 1"
        "city"       = "City"
        "postalCode" = "12345"
        "country"    = "Finland"
    }
}

$workflowName = "HttpHelloWorld"
$url = 
"/resourceGroups/$ResourceGroupName" +
"/providers/Microsoft.Web/sites/$LogicAppName" +
"/hostruntime/runtime/webhooks/workflow/api/management/workflows/$workflowName" +
"/triggers/manual/listCallbackUrl?api-version=2018-11-01"

$response = Invoke-AzRestMethod -Path $url

Write-Host "Smoke testing that our *MANDATORY* API is up and running..."
$workflowTriggerUri = "$($response.value)"
$data = @{
    id      = 1
    name    = "Doe"
    address = @{
        street     = "My street 1"
        postalCode = "12345"
        city       = "My city"
        country    = "Finland"
    }
}
$body = ConvertTo-Json $data
for ($i = 0; $i -lt 60; $i++) {
    try {
        $request = Invoke-WebRequest -Body $body -ContentType "application/json" -Method "POST" -DisableKeepAlive -Uri $workflowTriggerUri -ErrorAction SilentlyContinue
        Write-Host "API status code $($request.StatusCode)."

        if ($request.StatusCode -eq 200) {
            Write-Host "API is up and running."
            return
        }
    }
    catch {
        Start-Sleep -Seconds 3
    }
}

Throw "Mandatory API didn't respond on defined time."
