"""Konstanten der ComfoBox-Integration."""

from __future__ import annotations

from typing import Final

DOMAIN: Final = "comfobox"

#: Standard-Intervall zwischen zwei Abfragen des Gateways (Sekunden).
DEFAULT_SCAN_INTERVAL: Final = 10

#: Untergrenze für das vom Nutzer konfigurierbare Intervall.
MIN_SCAN_INTERVAL: Final = 5

#: Standardpfad des Gateways im lokalen Netz.
DEFAULT_HOST: Final = "http://localhost:8099"

#: Vom Gateway unterstützte Versionen.
MIN_GATEWAY_VERSION: Final = "1.0.0"

#: Pfade der verwendeten API-Endpunkte.
API_PATH_STATUS: Final = "/api/status"
API_PATH_ITEMS: Final = "/api/items"
API_PATH_HEALTH: Final = "/api/health"
API_PATH_RESTART: Final = "/api/service/restart"

#: Gerätehersteller und -modell für den Device-Registry.
MANUFACTURER: Final = "Zehnder"
MODEL: Final = "ComfoBox Series 5 (ELESTA)"

#: Anzahl der Sonder-Entitäten, die nicht aus dem Item-Katalog stammen.
EXTRA_ENTITY_COUNT: Final = 4

#: Reservierter Schlüssel im Coordinator-Daten für den Gateway-Status.
STATUS_KEY: Final = "__status__"
