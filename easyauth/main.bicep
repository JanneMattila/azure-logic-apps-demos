param location string = resourceGroup().location
param sku string = 'WorkflowStandard'
param skuCode string = 'WS1'

param clientId string
param integrationClientIds array

var workflowName = 'workflow${uniqueString(resourceGroup().name)}'
var storageAccountName = 'stor${uniqueString(resourceGroup().name)}'
var contentShare = 'content'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    defaultToOAuthAuthentication: true
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2020-08-01' = {
  name: 'log-integration'
  location: location
  properties: {}
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-integration'
  location: location
  properties: {
    maximumElasticWorkerCount: 5
    zoneRedundant: false
  }
  sku: {
    tier: sku
    name: skuCode
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'ai-integration'
  location: location
  kind: 'web'
  properties: {
    Request_Source: 'IbizaWebAppExtensionCreate'
    Flow_Type: 'Redfield'
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource workflowApp 'Microsoft.Web/sites@2022-03-01' = {
  name: workflowName
  kind: 'functionapp,workflowapp'
  location: location
  tags: {
    'hidden-link: /app-insights-resource-id': applicationInsights.id
  }
  properties: {
    siteConfig: {
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~18'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys('2023-05-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys('2023-05-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: contentShare
        }
        {
          name: 'AzureFunctionsJobHost__extensionBundle__id'
          value: 'Microsoft.Azure.Functions.ExtensionBundle.Workflows'
        }
        {
          name: 'AzureFunctionsJobHost__extensionBundle__version'
          value: '[1.*, 2.0.0)'
        }
        {
          name: 'APP_KIND'
          value: 'workflowApp'
        }
        {
          name: 'WEBSITE_AUTH_AAD_ALLOWED_TENANTS'
          value: subscription().tenantId
        }
        {
          name: 'WEBSITE_AUTH_AAD_REQUIRE_CLIENT_SERVICE_PRINCIPAL'
          value: 'true'
        }
      ]
      cors: {}
      use32BitWorkerProcess: false
      ftpsState: 'FtpsOnly'
      netFrameworkVersion: 'v6.0'
    }
    clientAffinityEnabled: false
    // functionsRuntimeAdminIsolationEnabled: false
    publicNetworkAccess: 'Enabled'
    httpsOnly: true
    serverFarmId: hostingPlan.id

    // Disable SAS authentication in triggers
    logicAppsAccessControlConfiguration: {
      triggers: {
        sasAuthenticationPolicy: {
          state: 'Disabled'
        }
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource scm 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-09-01' = {
  parent: workflowApp
  name: 'scm'
  properties: {
    allow: false
  }
}

resource ftp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-09-01' = {
  parent: workflowApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource authentication 'Microsoft.Web/sites/config@2022-09-01' = {
  name: 'authsettingsV2'
  parent: workflowApp
  properties: {
    httpSettings: {
      requireHttps: true
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'AllowAnonymous'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        login: {
          disableWWWAuthenticate: false
        }
        registration: {
          clientId: clientId
          openIdIssuer: 'https://sts.windows.net/${subscription().tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            'api://${clientId}'
          ]
          defaultAuthorizationPolicy: {
            allowedApplications: integrationClientIds
          }
        }
      }
    }

    login: {
      tokenStore: {
        enabled: true
      }
      nonce: {
        validateNonce: true
        nonceExpirationInterval: '00:05:00'
      }
      preserveUrlFragmentsForLogins: true
      routes: {}
    }
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
  }
}

output uri string = workflowApp.properties.hostNames[0]
