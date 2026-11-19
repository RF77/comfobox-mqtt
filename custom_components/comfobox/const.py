"""Konstanten der ComfoBox-Integration."""

from __future__ import annotations

from typing import Final

DOMAIN: Final = "comfobox"

#: Standard-Intervall der **Status**-Abfrage (Sekunden).
#:
#: Die Werte kommen per SSE (Push) - gepollt wird nur noch ``/api/status``
#: (BACnet-/MQTT-Verbindung, Schreibzähler, Version). Das Gateway liest den
#: Bus selbst mit ``Gateway:PollIntervalSeconds`` (Standard 60 s); häufiger
#: als einmal pro Minute zu fragen bringt deshalb keine neuen Werte.
DEFAULT_SCAN_INTERVAL: Final = 60

#: Untergrenze für das vom Nutzer konfigurierbare Status-Intervall.
MIN_SCAN_INTERVAL: Final = 15

#: Obergrenze für das vom Nutzer konfigurierbare Status-Intervall.
MAX_SCAN_INTERVAL: Final = 3600

#: Standardpfad des Gateways im lokalen Netz.
DEFAULT_HOST: Final = "http://localhost:8099"

#: Vom Gateway unterstützte Versionen.
MIN_GATEWAY_VERSION: Final = "1.0.0"

#: Pfade der verwendeten API-Endpunkte.
API_PATH_STATUS: Final = "/api/status"
API_PATH_ITEMS: Final = "/api/items"
API_PATH_HEALTH: Final = "/api/health"
API_PATH_EVENTS: Final = "/api/events"
API_PATH_RESTART: Final = "/api/service/restart"

#: Wie lange auf die nächste Zeile des Ereignis-Stroms gewartet wird (Sekunden).
#:
#: Der Gateway schickt alle 30 s ein Lebenszeichen (``: ping``). Bleibt auch
#: das aus, ist die Verbindung tot und wird neu aufgebaut.
SSE_READ_TIMEOUT: Final = 90

#: Wartezeit vor dem ersten Wiederverbinden (Sekunden).
SSE_RECONNECT_MIN: Final = 1

#: Obergrenze der Wartezeit beim Wiederverbinden (Sekunden).
SSE_RECONNECT_MAX: Final = 60

#: Nach so vielen erfolglosen Verbindungsversuchen gelten die Entitäten als
#: nicht verfügbar.
SSE_FAILURES_BEFORE_UNAVAILABLE: Final = 3

#: Gerätehersteller und -modell für den Device-Registry.
MANUFACTURER: Final = "Zehnder"
MODEL: Final = "ComfoBox Series 5 (ELESTA)"

#: Reservierter Schlüssel im Coordinator-Daten für den Gateway-Status.
STATUS_KEY: Final = "__status__"
