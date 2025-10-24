FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
ARG GIT_COMMIT="Pipeline Placeholder for Commit"
ARG BUILD_ID="Pipeline Placeholder for Build ID"

WORKDIR /src
# Add files needed for package restore to image
COPY ["AddressDownloader.csproj", "AddressDownloader.csproj"]
RUN dotnet restore "AddressDownloader.csproj"
# Add rest of files to image after package restore, so that changes here don't trigger cache miss for package restore step
# Note that for this to work it's important that the **/bin/ and **/obj/ directories are dockerignored
COPY [".", "."]

# RUN dotnet publish "AddressDownloader.csproj" --no-restore --configuration Release -o /app/publish /p:UseAppHost=false
RUN dotnet build "AddressDownloader.csproj" --no-restore --configuration Release -o /app/publish

# In pipeline, add metadata about build for version page
RUN echo $GIT_COMMIT > /app/publish/git-commit.txt \
    && echo $BUILD_ID > /app/publish/build-id.txt

# FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
FROM mcr.microsoft.com/dotnet/runtime:9.0-noble-chiseled AS final
# To trigger rebuild on change of digest of latest, update value of latest digest below, which last I checked is:
# sha256:0eb100b4135630b388acecf0b35c9d974a682fcf4c96d7ef731d814a4a8ea93b
# Tags found at https://mcr.microsoft.com/en-us/artifact/mar/dotnet/runtime/tags
WORKDIR /app
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "AddressDownloader.dll"]
