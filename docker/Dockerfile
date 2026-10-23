# SmartStock AI - imagen de producción (multi-stage).
# Se construye con el repo raíz como contexto:
#   docker build -f docker/Dockerfile -t smartstockai .

# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copian primero solo los .csproj para que 'restore' quede en caché
# mientras no cambien las dependencias.
COPY src/SmartStockAI.Domain/*.csproj         SmartStockAI.Domain/
COPY src/SmartStockAI.Application/*.csproj    SmartStockAI.Application/
COPY src/SmartStockAI.Infrastructure/*.csproj SmartStockAI.Infrastructure/
COPY src/SmartStockAI.Web/*.csproj            SmartStockAI.Web/
RUN dotnet restore SmartStockAI.Web/SmartStockAI.Web.csproj

COPY src/ .
RUN dotnet publish SmartStockAI.Web/SmartStockAI.Web.csproj \
    -c Release -o /app/publish --no-restore -p:UseAppHost=false

# ---------- Etapa 2: runtime (solo lo necesario para ejecutar) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# La imagen trae un usuario sin privilegios; no se ejecuta como root.
USER $APP_UID

# Dentro del contenedor solo se escucha HTTP; el TLS lo termina el proxy
# (Azure App Service) o no se usa en local.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SmartStockAI.Web.dll"]
