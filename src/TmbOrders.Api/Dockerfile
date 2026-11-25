# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só os arquivos de projeto primeiro para aproveitar o cache do "restore".
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TmbOrders.Domain/TmbOrders.Domain.csproj src/TmbOrders.Domain/
COPY src/TmbOrders.Infrastructure/TmbOrders.Infrastructure.csproj src/TmbOrders.Infrastructure/
COPY src/TmbOrders.ServiceDefaults/TmbOrders.ServiceDefaults.csproj src/TmbOrders.ServiceDefaults/
COPY src/TmbOrders.Api/TmbOrders.Api.csproj src/TmbOrders.Api/
RUN dotnet restore src/TmbOrders.Api/TmbOrders.Api.csproj

COPY src/ src/
RUN dotnet publish src/TmbOrders.Api/TmbOrders.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl é usado pelo healthcheck do docker-compose.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TmbOrders.Api.dll"]
