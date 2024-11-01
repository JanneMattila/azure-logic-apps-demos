# Logic Apps and EasyAuth

```mermaid
sequenceDiagram
    participant Client1
    participant Client2
    participant Client3
    participant Integration
    Client1->>Integration: Request
    Integration-->>Client1: Response OK
    Client2->>Integration: Request
    Integration-->>Client2: Response OK
    Client2->>Integration: Request Blocked in EasyAuth
```

```
GET https://<workflow>.azurewebsites.net:443/api/workflow1/triggers/request/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Frequest%2Frun&sv=1.0&sig=Qs...hAB8o
Authorization: Bearer <token>
```

```json
{
  "error": {
    "code": "DirectApiRequestHasMoreThanOneAuthorization",
    "message": "The request has SAS authentication scheme, 'Bearer' authorization scheme or internal token scheme. Only one scheme should be used."
  }
}
```

```
GET https://<workflow>.azurewebsites.net:443/api/workflow1/triggers/request/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Frequest%2Frun&sv=1.0&sig=Qs...hAB8o
```

```json
{
  "error": {
    "code": "DirectApiInvalidAuthorizationScheme",
    "message": "The request has SAS authentication scheme while it is disabled under your access control policy."
  }
}
```
