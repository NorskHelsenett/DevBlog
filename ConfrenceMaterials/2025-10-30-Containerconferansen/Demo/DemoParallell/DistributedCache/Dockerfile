FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
ARG GIT_COMMIT="Pipeline Placeholder for Commit"
ARG BUILD_ID="Pipeline Placeholder for Build ID"

WORKDIR /src
# Add files needed for package restore to image
COPY ["DistributedCache.csproj", "DistributedCache.csproj"]
RUN dotnet restore "DistributedCache.csproj"
# Add rest of files to image after package restore, so that changes here don't trigger cache miss for package restore step
# Note that for this to work it's important that the **/bin/ and **/obj/ directories are dockerignored
COPY [".", "."]

RUN dotnet publish "DistributedCache.csproj" \
    --configuration Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false
# RUN dotnet publish "DistributedCache.csproj" --configuration Release -o /app/publish /p:UseAppHost=false
# RUN dotnet build "DistributedCache.csproj" --configuration Release -o /app/publish

# In pipeline, add metadata about build for version page
RUN echo $GIT_COMMIT > /app/publish/git-commit.txt \
    && echo $BUILD_ID > /app/publish/build-id.txt

# FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
# docker pull mcr.microsoft.com/dotnet/aspnet:9.0.3-azurelinux3.0-distroless
# docker pull mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled
FROM mcr.microsoft.com/dotnet/aspnet:9.0.3-azurelinux3.0-distroless AS final
# To trigger rebuild on change of digest of latest, update value of latest digest below, which last I checked is:
# sha256:dba2cfa6a751eac6ecc5371c2f7f08cad711edbf465afe8180c60be2853e7a5b
# Tags found at https://mcr.microsoft.com/en-us/artifact/mar/dotnet/aspnet/tags
WORKDIR /app
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "DistributedCache.dll"]
