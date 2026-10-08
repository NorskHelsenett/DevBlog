# Build health check app
FROM mcr.microsoft.com/dotnet/sdk:10.0-resolute AS build-health-checker
WORKDIR /source
# Because this doesn't really ever change, don't bother with spending lines and layers on optimizing for cache hits during development
COPY HealthChecker/ ./
RUN dotnet publish --configuration Release --no-self-contained -o /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-resolute AS build-env
WORKDIR /source

# Copy only csprojes for restore
COPY StateDistributor/StateDistributor.csproj ./
# Restore as distinct layers
RUN dotnet restore StateDistributor.csproj
# Build and publish a release
COPY StateDistributor/ ./
RUN dotnet publish --no-restore --configuration Release -o /app StateDistributor.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-resolute-chiseled-composite
VOLUME [ "/ContainerData" ]
WORKDIR /app
COPY --from=build-env /app .
COPY --from=build-health-checker /app /HealthChecker/
HEALTHCHECK CMD ["dotnet", "/HealthChecker/HealthChecker.dll", "--", "http://localhost:8080/healthz"]
ENTRYPOINT ["dotnet", "./StateDistributor.dll"]
