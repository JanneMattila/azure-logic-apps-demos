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
