"""Die Datenquellen des Vergleichs.

Live: der Broker liefert die Payloads (``paho-mqtt``).
Snapshot: eine Aufzeichnung aus einer Datei.

Beide Quellen liefern denselben Typ (:class:`Snapshot`) - der Vergleich
selbst kennt den Unterschied nicht.
"""

from __future__ import annotations

import time
from dataclasses import dataclass

from .snapshot import RecordedValue, Snapshot, now_iso


@dataclass(frozen=True)
class MqttTarget:
    """Das Ziel einer MQTT-URI."""

    #: Der Broker (Host oder IP).
    host: str

    #: Der Port (Standard 1883).
    port: int

    #: Das Topic-Filter (``ComfoBox/#``).
    topic_filter: str

    #: Das ``BaseTopic`` (das erste Segment des Filters).
    base_topic: str


def parse_mqtt_uri(uri: str) -> MqttTarget:
    """Zerlegt eine MQTT-URI der Form ``mqtt://host:port/topic/#``.

    Die URI enthält ein ``#`` (Wildcard) - deshalb wird sie von Hand
    zerlegt und nicht mit ``urlparse`` (das würde das ``#`` als Fragment
    behandeln).
    """
    text = uri.strip()
    if not text.lower().startswith("mqtt://"):
        raise ValueError(f"'{uri}' ist keine MQTT-URI (erwartet: mqtt://host:port/topic/#).")

    rest = text[len("mqtt://") :]
    authority, _, path = rest.partition("/")

    # Das Topic-Filter beginnt **ohne** Schrägstrich: ``ComfoBox/#``.
    topic_filter = path or "#"

    host, _, port_text = authority.partition(":")
    port = int(port_text) if port_text else 1883

    if not host:
        raise ValueError(f"'{uri}' enthält keinen Host.")

    base_topic = topic_filter.strip("/").split("/")[0] if topic_filter.strip("/") else ""

    return MqttTarget(
        host=host,
        port=port,
        topic_filter=topic_filter,
        base_topic=base_topic,
    )


class LiveCollector:
    """Sammelt die Payloads eines Brokers."""

    def __init__(
        self,
        target: MqttTarget,
        username: str | None = None,
        password: str | None = None,
        client_id: str | None = None,
    ) -> None:
        self._target = target
        self._username = username
        self._password = password
        self._client_id = client_id or f"cbcompare-{int(time.time() * 1000) % 100000}"

    def collect(self, duration: float) -> Snapshot:
        """Zeichnet ``duration`` Sekunden lang auf."""
        try:
            import paho.mqtt.client as mqtt
        except ImportError as exc:  # pragma: no cover - nur ohne paho erreichbar
            raise RuntimeError(
                "paho-mqtt wird für den Live-Modus benötigt: pip install -r requirements.txt"
            ) from exc

        values: dict[str, RecordedValue] = {}

        def on_message(_client, _userdata, message):
            payload = message.payload.decode("utf-8", errors="replace")
            values[message.topic] = RecordedValue(payload=payload, timestamp=now_iso())

        client = mqtt.Client(client_id=self._client_id, clean_session=True)
        if self._username:
            client.username_pw_set(self._username, self._password)
        client.on_message = on_message

        client.connect(self._target.host, self._target.port, keepalive=30)
        client.loop_start()
        try:
            client.subscribe(self._target.topic_filter, qos=0)
            time.sleep(max(duration, 0.0))
        finally:
            client.loop_stop()
            client.disconnect()

        return Snapshot(
            recorded_at=now_iso(),
            source=f"mqtt://{self._target.host}:{self._target.port}{self._target.topic_filter}",
            base_topic=self._target.base_topic,
            duration_seconds=duration,
            values=values,
        )


def collect_from_uri(
    uri: str,
    duration: float,
    username: str | None = None,
    password: str | None = None,
) -> Snapshot:
    """Zeichnet live von einem Broker auf."""
    return LiveCollector(
        parse_mqtt_uri(uri), username=username, password=password
    ).collect(duration)
