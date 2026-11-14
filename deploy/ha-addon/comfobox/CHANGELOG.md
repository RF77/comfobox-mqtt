# Changelog – ComfoBox Gateway Add-on

## 1.1.0

- Image `ghcr.io/rf77/comfobox-gateway:1.1.0` (multi-arch: `amd64`, `aarch64`)
- **Schreibvorgänge funktionieren.** Der Request ging als MS/TP-Frame ohne
  `ExpectingReply` auf den Bus – die ComfoBox darf darauf nicht antworten, der
  Gateway lief in die Zeitüberschreitung. Jetzt antwortet sie (SimpleACK).
- **Der Experten-Modus wirkt sofort** und wird nach dem Speichern korrekt
  zurückgemeldet (vorher zeigte die Oberfläche wieder „aus").
- **Die Oberfläche meldet Erfolg und Fehler** – der Toaster fehlte im
  Wurzel-Template, alle Rückmeldungen liefen ins Leere.
- **Die Port-Auswahl nennt Chipsatz und Art** (`CH340 · USB-Seriell`,
  `UART auf der Platine`) und der BACnet-Test meldet nicht mehr fälschlich
  „dialout fehlt", wenn der Gateway den Port selbst hält.
- Eigenes Markenzeichen und App-Icons (auch Startbildschirm/Manifest).

## 1.0.4

- Image `ghcr.io/rf77/comfobox-gateway:1.0.4` (multi-arch: `amd64`, `aarch64`)
- **Behoben: der Container bekam kein serielles Gerät durchgereicht.** Das
  Manifest hatte lediglich den Schema-Filter `device(subsystem=tty)` (baut nur
  das Dropdown) – `uart` ist laut Supervisor-Referenz per Default `false`.
  Der Adapter wäre im Formular wählbar gewesen, im Container aber unsichtbar.
  Jetzt: `uart: true`.

## 1.0.1

- Image `ghcr.io/rf77/comfobox-gateway:1.0.1` (multi-arch: `amd64`, `aarch64`)
- Das Synology-Paket dieser Version enthält die Rechte-Datei `conf/privilege`
  und lässt sich damit auf DSM 7 installieren

## 1.0.0

- Erstveröffentlichung
- Image `ghcr.io/rf77/comfobox-gateway:1.0.0` (multi-arch: `amd64`, `aarch64`)
- Serieller Port per Supervisor-Dropdown (`device(subsystem=tty)`)
- Web-UI über Ingress (Panel in Home Assistant)
- REST/SSE auf Host-Port 8099
- Optionaler MQTT-Broker (`mqtt:want`)
