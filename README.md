# Logic Apps Anywhere

```bash
docker build -t logicapp1 .
docker run --rm -p 8080:80 -e WEBSITE_HOSTNAME=localhost -e AzureWebJobsStorage="key" logicapp1
```
