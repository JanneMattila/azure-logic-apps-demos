# Logic Apps Anywhere

```bash
docker build -t logicapp1 .
docker run --rm -p 8080:80 -e AzureWebJobsStorage=key logicapp1
```
