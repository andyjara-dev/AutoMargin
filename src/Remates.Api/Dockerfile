# Imagen de la API. Se construye desde la raíz del repositorio:
#   docker build -f src/Remates.Api/Dockerfile .

# ---------- Compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: mientras las dependencias no cambien, Docker reutiliza la capa
# del restore y no vuelve a descargar los paquetes en cada build.
COPY src/Remates.Domain/Remates.Domain.csproj src/Remates.Domain/
COPY src/Remates.Infrastructure/Remates.Infrastructure.csproj src/Remates.Infrastructure/
COPY src/Remates.Api/Remates.Api.csproj src/Remates.Api/
RUN dotnet restore src/Remates.Api/Remates.Api.csproj

COPY src/ src/
RUN dotnet publish src/Remates.Api/Remates.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Ejecución ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl no viene en la imagen de runtime y el healthcheck lo necesita.
# Usuario sin privilegios: si alguien logra ejecutar código dentro del contenedor,
# no lo hace como root.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 5678 --create-home --shell /usr/sbin/nologin automargin \
    && mkdir -p /app/logs \
    && chown -R automargin:automargin /app

COPY --from=build --chown=automargin:automargin /app/publish .

USER automargin

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

EXPOSE 8080

# El healthcheck consulta el estado real de la base, no solo que el proceso viva.
# Contra 127.0.0.1 y no localhost, para no depender de cómo resuelva IPv6 el contenedor.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD ["/bin/sh", "-c", "curl -fsS http://127.0.0.1:8080/health || exit 1"]

ENTRYPOINT ["dotnet", "Remates.Api.dll"]
