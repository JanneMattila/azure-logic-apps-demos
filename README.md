# Logic Apps Anywhere

```bash
dotnet publish logic-apps-anywhere.csproj --output app/ --configuration Release
docker build -t logicapp1 .
docker run --rm -p 8080:80 -e WEBSITE_HOSTNAME=localhost -e AzureWebJobsStorage="key" logicapp1
```

```bash
docker build -t logicapp1 -f Dockerfile2 .
docker run --rm -p 8080:80 -e WEBSITE_HOSTNAME=localhost -e AzureWebJobsStorage="key" logicapp1
```

## HttpHelloWorld

Workflow definition:

![workflow](https://user-images.githubusercontent.com/2357647/94061225-c6a4ff80-fded-11ea-83ad-b4f42e522bd8.png)

To test, just `POST` this payload:

```json
{
  "id": 123,
  "name": "John",
  "phone": "+1234567890",
  "address": {
    "street": "Street 1",
    "city": "City",
    "postalCode": "12345",
    "country": "Finland"
  }
}
```

You should get this response back:

```json
{
  "phone": "+1234567890"
}
```
