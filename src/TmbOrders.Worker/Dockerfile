# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TmbOrders.Domain/TmbOrders.Domain.csproj src/TmbOrders.Domain/
COPY src/TmbOrders.Infrastructure/TmbOrders.Infrastructure.csproj src/TmbOrders.Infrastructure/
COPY src/TmbOrders.ServiceDefaults/TmbOrders.ServiceDefaults.csproj src/TmbOrders.ServiceDefaults/
COPY src/TmbOrders.Worker/TmbOrders.Worker.csproj src/TmbOrders.Worker/
RUN dotnet restore src/TmbOrders.Worker/TmbOrders.Worker.csproj

COPY src/ src/
RUN dotnet publish src/TmbOrders.Worker/TmbOrders.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TmbOrders.Worker.dll"]
