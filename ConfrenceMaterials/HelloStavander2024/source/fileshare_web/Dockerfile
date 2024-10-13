# Build health check app
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build-health-checker
WORKDIR /source
# Because this doesn't really ever change, don't bother with spending lines and layers on optimizing for cache hits during development
COPY HealthChecker/ ./
RUN dotnet publish --configuration Release --no-self-contained -o /app

# Build the fileshare_web app
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build-env
WORKDIR /source

# Copy only csprojes for restore
COPY fileshare_web/fileshare_web.csproj ./
# Restore as distinct layers
RUN dotnet restore fileshare_web.csproj
# Build and publish a release
COPY fileshare_web/ ./
RUN dotnet publish --no-restore --configuration Release -o /app fileshare_web.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-composite
WORKDIR /app
COPY --from=build-env /app .
COPY --from=build-health-checker /app /HealthChecker/
ENTRYPOINT ["dotnet", "./fileshare_web.dll"]
