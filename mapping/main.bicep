param logicAppName string = 'logic-map'
param sharepointName string = 'sharepoint'

param site string
param list string

resource sharepointResource 'Microsoft.Web/connections@2016-06-01' = {
  name: sharepointName
  location: resourceGroup().location
  properties: {
    displayName: 'SharePoint'
    statuses: [
      {
        status: 'Connected'
      }
    ]
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${resourceGroup().location}/managedApis/sharepointonline'
      name: sharepointName
      type: 'Microsoft.Web/locations/managedApis'
      displayName: 'SharePoint'
      description: 'SharePoint helps organizations share and collaborate with colleagues, partners, and customers. You can connect to SharePoint Online or to an on-premises SharePoint 2016 or 2019 farm using the On-Premises Data Gateway to manage documents and list items.'
      iconUri: 'https://connectoricons-prod.azureedge.net/u/jayawan/releases/v1.0.1697/1.0.1697.3786/sharepointonline/icon.png'
      brandColor: '#036C70'
    }
    testLinks: []
  }
}

resource logicAppResource 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: resourceGroup().location
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {
        '$connections': {
          defaultValue: {}
          type: 'Object'
        }
        site: {
          defaultValue: site
          type: 'String'
        }
        list: {
          defaultValue: list
          type: 'String'
        }
      }
      triggers: {
        request: {
          type: 'Request'
          kind: 'Http'
          inputs: {
            method: 'POST'
            schema: {
              type: 'object'
              properties: {
                key: {
                  type: 'string'
                }
              }
            }
          }
          operationOptions: 'SuppressWorkflowHeadersOnResponse'
        }
      }
      actions: {
        Get_items: {
          runAfter: {}
          type: 'ApiConnection'
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'${sharepointName}\'][\'connectionId\']'
              }
            }
            method: 'get'
            path: '/datasets/@{encodeURIComponent(encodeURIComponent(parameters(\'site\')))}/tables/@{encodeURIComponent(encodeURIComponent(parameters(\'list\')))}/items'
            queries: {
              '$filter': 'Title eq \'@{triggerBody()?[\'key\']}\''
              '$top': 1
            }
          }
        }
        Condition: {
          actions: {
            Response: {
              type: 'Response'
              kind: 'Http'
              inputs: {
                statusCode: 200
                body: 'Here is response:\n\n@{first(body(\'Get_items\')?[\'value\'])?[\'Map\']}'
              }
            }
          }
          runAfter: {
            Get_items: [
              'Succeeded'
            ]
          }
          else: {
            actions: {
              Response_1: {
                type: 'Response'
                kind: 'Http'
                inputs: {
                  statusCode: 404
                  body: 'Mapping not found!'
                }
              }
            }
          }
          expression: {
            and: [
              {
                equals: [
                  '@length(body(\'Get_items\')?[\'value\'])'
                  1
                ]
              }
            ]
          }
          type: 'If'
        }
      }
      outputs: {}
    }
    parameters: {
      '$connections': {
        value: {
          '${sharepointName}': {
            id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${resourceGroup().location}/managedApis/sharepointonline'
            connectionName: sharepointName
            connectionId: sharepointResource.id
          }
        }
      }
    }
  }
}

output uri string = listCallbackUrl('${logicAppResource.id}/triggers/request', logicAppResource.apiVersion).value
