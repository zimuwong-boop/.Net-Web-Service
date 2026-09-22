FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/QosServicePlatform.Web/QosServicePlatform.Web.csproj --configfile NuGet.Config
RUN dotnet publish src/QosServicePlatform.Web/QosServicePlatform.Web.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV PROVIDER_BASE_URL=http://providers:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "QosServicePlatform.Web.dll"]

