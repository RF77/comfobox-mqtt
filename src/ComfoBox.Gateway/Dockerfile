# syntax=docker/dockerfile:1

# ─────────────────────────────────────────────────────────────────────────────
# ComfoBox MQTT – Gateway + Web-Oberfläche in EINEM Image
#
# → ADR-007 (docs/05-entscheidungen/adr-007-ein-container-image.md)
# → Spec:   docs/03-deployment/containerisierung.md
#
# Bauen aus dem Repository-Wurzelverzeichnis:
#   docker build -f src/ComfoBox.Gateway/Dockerfile -t comfobox-gateway:local .
#
# Das Image enthält:
#   * den .NET-10-Gateway (MQTT, REST/SSE-API, BACnet/MS-TP)
#   * die gebaute Angular-Oberfläche unter /app/wwwroot (kein nginx!)
#   * tini als PID 1 (korrektes SIGTERM → LWT wird ausgelöst)
# ─────────────────────────────────────────────────────────────────────────────

# ═════════════════════════════════════════════════════════════════════════════
# Stage 1 – Web-Oberfläche bauen (Angular)
# ═════════════════════════════════════════════════════════════════════════════
FROM node:22-alpine AS web-build
WORKDIR /app

# package*.json zuerst → der npm-Layer bleibt gültig, wenn nur der Code
# geändert wurde.
COPY src/ComfoBox.Web/package.json src/ComfoBox.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY src/ComfoBox.Web/ ./
RUN npm run build

# Ergebnis: /app/dist/ComfoBox.Web/browser


# ═════════════════════════════════════════════════════════════════════════════
# Stage 2 – Gateway bauen (.NET)
# ═════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore zuerst → Layer-Cache bleibt gültig, wenn nur der Code geändert wurde.
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props ./
COPY src/ComfoBox.Domain/ComfoBox.Domain.csproj src/ComfoBox.Domain/
COPY src/ComfoBox.Domain/packages.lock.json src/ComfoBox.Domain/
COPY src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj src/ComfoBox.Bacnet/
COPY src/ComfoBox.Bacnet/packages.lock.json src/ComfoBox.Bacnet/
COPY src/ComfoBox.Gateway/ComfoBox.Gateway.csproj src/ComfoBox.Gateway/
COPY src/ComfoBox.Gateway/packages.lock.json src/ComfoBox.Gateway/
RUN dotnet restore src/ComfoBox.Gateway/ComfoBox.Gateway.csproj

COPY src/ src/

# Die gebaute SPA an die Stelle, die das Gateway-csproj erwartet
# (Content Include ..\ComfoBox.Web\dist\ComfoBox.Web\browser\**).
COPY --from=web-build /app/dist/ComfoBox.Web/browser \
     src/ComfoBox.Web/dist/ComfoBox.Web/browser

RUN dotnet publish src/ComfoBox.Gateway/ComfoBox.Gateway.csproj \
        --configuration Release \
        --output /app/publish \
        --no-restore \
        /p:UseAppHost=false \
        /p:InvariantGlobalization=false


# ═════════════════════════════════════════════════════════════════════════════
# Stage 3 – Laufzeit (kein Node, kein SDK)
# ═════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

LABEL org.opencontainers.image.title="ComfoBox MQTT Gateway" \
      org.opencontainers.image.description="MQTT-Gateway für die Zehnder ComfoBox Series 5 (BACnet/MS-TP)" \
      org.opencontainers.image.source="https://github.com/rf77/comfobox-mqtt" \
      org.opencontainers.image.licenses="MIT"

# tini: PID 1 ohne Zombie-Reaping und ohne Signal-Weiterleitung.
# curl:   nur für den Healthcheck (busybox-wget reicht nicht für -fsS mit
#         korrektem Exit-Code unter allen Busybox-Versionen).
# icu-*:  das Alpine-Basis-Image setzt DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
#         (alpine bringt kein ICU mit). Die Oberfläche formatiert deutsch -
#         deshalb wird ICU nachinstalliert und der Invariant-Modus abgeschaltet.
RUN apk add --no-cache tini curl icu-libs icu-data-full \
 && addgroup -S comfobox -g 1001 \
 && adduser -S comfobox -u 1001 -G comfobox

WORKDIR /app
COPY --from=build /app/publish ./

# Datenverzeichnis (persistenter 24-h-Schreibzähler + config.json) und
# Logverzeichnis. Beide gehören dem Benutzer comfobox (kein Root).
RUN mkdir -p /var/lib/comfobox /var/log/comfobox \
 && chown -R comfobox:comfobox /app /var/lib/comfobox /var/log/comfobox

USER comfobox

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    COMFOBOX_Gateway__DataDirectory=/var/lib/comfobox \
    COMFOBOX_Logging__Directory=/var/log/comfobox \
    COMFOBOX_Bacnet__Port=/dev/ttyUSB0 \
    COMFOBOX_Bacnet__Baudrate=38400

EXPOSE 8080

# Das Datenverzeichnis enthält den Schreibzähler (Befund A3) - es darf
# nicht verloren gehen. `docker compose down` OHNE `-v` verwenden.
VOLUME ["/var/lib/comfobox"]

# Der Healthcheck prüft nur die ERREICHBARKEIT. Ein Zustand "degraded"
# (z. B. kein MQTT-Broker) ist kein Container-Problem und darf keinen
# Neustart auslösen.
HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["/sbin/tini", "--"]
CMD ["dotnet", "ComfoBox.Gateway.dll"]
