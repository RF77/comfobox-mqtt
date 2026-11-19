# Changelog – ComfoBox Gateway Add-on

## 1.3.0

- Image `ghcr.io/rf77/comfobox-gateway:1.3.0` (multi-arch: `amd64`, `aarch64`)
- **Die Werte kommen als Push über Server-Sent Events** – die Integration pollt
  nur noch den Gateway-Status (60 s). Ein Auslesezyklus wird als **ein** Ereignis
  gesendet (vorher je Wert), das entlastet Home Assistant deutlich.
- **Jeder Wert wird seiner Bedeutung nach abgebildet**: 0/1-Werte als Schalter
  oder Meldung, Kodewerte wie „Kühlfunktion" als Auswahl mit lesbaren Namen,
  Auslöser als Taster. Nur Messgrößen und Zähler erzeugen noch eine Statistik.
- **Alte Entitäten lassen sich aufräumen** – als Reparatur-Hinweis in Home
  Assistant, samt Zustandsverlauf und Langzeitstatistik.

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
