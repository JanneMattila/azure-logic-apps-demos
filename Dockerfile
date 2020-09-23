# This Dockerfile contains Build and Release steps:
# 1. Build image(https://hub.docker.com/_/microsoft-dotnet-core-sdk/)
FROM mcr.microsoft.com/dotnet/core/sdk:3.1.402-alpine3.12 AS build
WORKDIR /source

# Cache nuget restore
COPY *.csproj .
RUN dotnet restore logic-apps-anywhere.csproj

# Copy sources and compile
COPY . .
RUN dotnet publish logic-apps-anywhere.csproj --output /app/ --configuration Release

# 2. Release image
FROM mcr.microsoft.com/azure-functions/dotnet:3.0.14492-appservice

# ENV AzureWebJobsStorage=<storage-account-connection-string>
ENV AZURE_FUNCTIONS_ENVIRONMENT=Development
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
ENV FUNCTIONS_V2_COMPATIBILITY_MODE=true

# Copy content from Build image
COPY --from=build /app /home/site/wwwroot
