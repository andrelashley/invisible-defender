FROM node:24-bookworm-slim AS web
WORKDIR /web
COPY src/web/package*.json ./
RUN npm ci
COPY src/web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /source
COPY NuGet.Config ./
COPY src/api/InvisibleDefender.Api.csproj src/api/
RUN dotnet restore src/api/InvisibleDefender.Api.csproj --configfile NuGet.Config
COPY src/api/ src/api/
RUN dotnet publish src/api/InvisibleDefender.Api.csproj -c Release --no-restore -o /publish /p:UseAppHost=false
COPY --from=web /web/dist /publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=api /publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "InvisibleDefender.Api.dll"]
