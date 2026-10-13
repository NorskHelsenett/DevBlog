FROM mcr.microsoft.com/dotnet/aspnet:10.0-resolute-chiseled-composite AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0-resolute AS build
WORKDIR /src
COPY ["./TopicCopier.csproj", "TopicCopier/"]
RUN dotnet restore "TopicCopier/TopicCopier.csproj"
WORKDIR "/src/TopicCopier"
COPY . ./
RUN dotnet build "TopicCopier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TopicCopier.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TopicCopier.dll"]
