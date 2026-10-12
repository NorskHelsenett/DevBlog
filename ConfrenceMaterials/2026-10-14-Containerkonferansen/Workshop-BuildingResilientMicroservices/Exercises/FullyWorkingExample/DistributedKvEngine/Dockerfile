# Latest aspnet runtime tags: https://mcr.microsoft.com/en-us/product/dotnet/aspnet/tags
# Latest sdk tags: https://mcr.microsoft.com/en-us/product/dotnet/nightly/sdk/tags

# Build main app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-kv-api
WORKDIR /App
# Copy csproj and restore as distinct layer, so that packages download step is cached as long as csprojfile doesn't change
# Be sure that you have properly ignored the **/bin/ and **/obj/ paths in your dockerignore!
COPY KeyValueApi/KeyValueApi.csproj ./
RUN dotnet restore
# Copy everything else for build
COPY KeyValueApi/ ./
# Build and publish a release
RUN dotnet publish --no-restore --configuration Release -o out

# Build health check app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-health-checker
WORKDIR /App
# Because this doesn't really ever change, don't bother with spending lines and layers on optimizing for cache hits during development
COPY HealthChecker/ ./
RUN dotnet publish --configuration Release --no-self-contained -o out

# Compile final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-composite AS final
WORKDIR /App
COPY --from=build-kv-api /App/out .
COPY --from=build-health-checker /App/out /HealthChecker/
HEALTHCHECK CMD [ "dotnet", "/HealthChecker/HealthChecker.dll", "--", "http://localhost:8080/healthz" ]
ENTRYPOINT ["dotnet", "KeyValueApi.dll"]
