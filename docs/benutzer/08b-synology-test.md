---
title: Synology testen
description: Schritt für Schritt das Paket auf der DS923+ installieren und den ersten Test machen
order: 82
icon: pi pi-check-circle
tags: [synology, nas, test]
---

# Auf der Synology testen

Diese Seite führt Sie Schritt für Schritt durch den ersten Test auf der
DS923+ – vom Download bis zur Anzeige, ob die Heizung antwortet.

> **Sie brauchen:** die DS923+ mit DSM 7.2 oder neuer, den **Container
> Manager** (DSM installiert ihn bei Bedarf selbst) und – für den Test mit
> der echten Heizung – den USB-Adapter.

---

## Schritt 0 – Das Image muss öffentlich sein

> **Das ist der wichtigste Schritt, und er liegt auf GitHub, nicht auf der
> NAS.** Das Paket ist nur ein Wrapper: es startet das Container-Image
> `ghcr.io/rf77/comfobox-gateway:1.0.0` aus der GitHub-Registry. Ist das
> Image **privat**, kann die NAS es nicht ziehen – der Container startet
> dann nicht und es passiert gar nichts.

**So prüfen Sie das** (ohne Anmeldung, also so, wie es die NAS sieht):

```
https://ghcr.io/v2/rf77/comfobox-gateway/manifests/1.0.0
```

| Antwort | Bedeutung |
|---|---|
| **401 Unauthorized** | das Image ist privat → Schritt 0a ist nötig |
| **200 OK** | alles bereit → weiter mit Schritt 1 |

### Schritt 0a – Image öffentlich machen

1. **Token anlegen** – GitHub → *Settings* → *Developer settings* →
   *Personal access tokens* → *Tokens (classic)* → *Generate new token*.
   Erlaubte Scopes: **`write:packages`** und **`read:packages`**.

2. **Token als Secret hinterlegen** – im Repository → *Settings* →
   *Secrets and variables* → *Actions* → *New repository secret*:
   Name `GHCR_TOKEN`, Wert = der Token.

3. **Altes Paket löschen** – <https://github.com/users/RF77/packages/container/comfobox-gateway/settings>
   → *Delete package*. Das ist nötig, weil ein Image, das mit dem
   automatischen GitHub-Token veröffentlicht wurde, **nicht** auf „public"
   umgestellt werden kann.

4. **Tag neu pushen** – damit das Image mit Ihrem Token neu veröffentlicht
   wird. Im Repository:

   ```bash
   git push --delete origin v1.0.0
   git push origin v1.0.0
   ```

5. **Sichtbarkeit umstellen** – dieselbe Seite wie in Schritt 3 →
   *Change visibility* → *Public*.

> **Ab Schritt 4 läuft der Image-Workflow** und meldet am Ende selbst, ob
> das Paket öffentlich ist. Sie können die Warnung in den Workflow-Protokollen
> nachlesen.

---

## Schritt 1 – Paket herunterladen

[ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk](https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.0/ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk)
herunterladen und auf die NAS kopieren (Samba-Freigabe oder `scp`).

> **Alternativ** über eine eigene Paketquelle – dann erscheint das Paket
> direkt in der normalen Liste des Paketzentrums:
> *Paketcenter → Einstellungen → Paketquelle → Hinzufügen*, URL
> <https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.0/packages.json>

---

## Schritt 2 – Installieren

**Paketcenter → Installieren → Manuelle Installation** → Datei wählen →
*Installieren*.

Das Paket prüft vorher selbst (DSM-Version, Container Manager, freier Port
8081) und startet danach den Container. Der Browser öffnet sich
automatisch auf `http://<NAS-IP>:8081/`.

**Ohne Hardware testen?** Das geht: Der Assistent startet auch ohne
USB-Adapter. Sie können Web-Oberfläche, MQTT-Anbindung und Kennwort
einrichten. Die BACnet-Werte bleiben dann leer – das ist erwartet.

---

## Schritt 3 – Der erste Blick

| Was prüfen | Wo | Erwartet |
|---|---|---|
| Läuft der Container? | Container Manager → `comfobox-gateway` | Status *läuft*, keine Neustarts |
| Antwortet der Gateway? | `http://<NAS-IP>:8081/api/health` | `"state":"online"` |
| Zeigt die Oberfläche? | Browser auf Port 8081 | Dashboard ohne Fehlerbanner |
| Läuft der Überwachungsdienst? | `synopkg status ComfoBoxGateway` | aktiv |

Der Container braucht **etwa 30 Sekunden** bis zum ersten „healthy" – der
Healthcheck läuft erst in diesem Abstand.

---

## Schritt 4 – Den seriellen Port prüfen

Stecken Sie den USB-Adapter an und rufen Sie die Ports ab:

```
http://<NAS-IP>:8081/api/config/serial-ports
```

Sie sollten Ihren Adapter sehen, zum Beispiel:

```json
{ "ports": [ { "device": "/dev/ttyUSB0",
               "stableId": "/dev/serial/by-id/usb-Prolific_USB-Serial-if00-port0",
               "description": "Prolific CH340 (1a86:7523)" } ] }
```

| Sehen Sie … | Bedeutung |
|---|---|
| Ihren Adapter | gut – weiter mit Schritt 5 |
| **nichts** | der Adapter wird nicht erkannt (Treiber, USB-Port, Kontakt) |
| einen **anderen** Port | der Adapter belegt einen anderen Anschluss – diesen wählen |

> **Frage:** Ist die ComfoBox auf **38400 Baud** umgestellt? Werksdefault ist
> 76800. Bei 76800 antwortet die Heizung nicht – der Gateway meldet dann
> ständig Zeitüberschreitungen. Das lässt sich am Display der Heizung
> einstellen (dazu braucht es einen Neustart der Heizung).

---

## Schritt 5 – Der Assistent

Der Assistent läuft in fünf Schritten:

| Schritt | Was passiert |
|---|---|
| 1 | **Seriellen Port wählen** – den aus Schritt 4 |
| 2 | **BACnet prüfen** – Baudrate 38400, Geräte-ID 1, Verbindungstest |
| 3 | **MQTT** – Broker eintragen (optional, aber der eigentliche Zweck) |
| 4 | **Web-Zugang** – Benutzername und Passwort (mind. 12 Zeichen) |
| 5 | **Fertig** – die Konfiguration wird gespeichert, der Gateway startet neu |

> **Der Assistent ist wiederholbar** – *Konfiguration → Erneut einrichten*.
> Praktisch, wenn Sie den Adapter umgesteckt haben.

**Wichtiger Nebeneffekt:** Sobald der Assistent einen Port gespeichert hat,
startet der Supervisor den Container mit genau diesem Gerät neu. Das
Container-Protokoll zeigt dann die serielle Zuweisung.

---

## Schritt 6 – Prüfen, ob die Heizung antwortet

In der Web-Oberfläche → **Diagnose**:

| Reiter | Was Sie sehen sollten |
|---|---|
| **Prüfungen** | BACnet: *verbunden*, MQTT: *verbunden* |
| **BACnet-Bus** | Zähler steigen, der Token läuft im Umlauf |
| **Protokoll** | keine Fehler, regelmäßig gelesene Werte |

Und im **Dashboard** die ersten Werte der Heizung (Temperatur, Sollwerte,
Warmwasser).

> **Der Reiter „BACnet-Bus" ist nur lesend** – er stellt keine Anfrage an die
> Heizung und ändert nichts. Er zählt mit, ob auf dem Bus überhaupt ein Token
> herumgeht.

---

## Wenn etwas nicht klappt

### Das Paket installiert, aber der Container startet nicht

Fast immer die Ursache: **das Image ist privat**. Prüfen Sie Schritt 0
noch einmal. Das Protokoll des Pakets zeigt das deutlich:

```bash
sudo tail -50 /tmp/ComfoBoxGateway.log
```

### Der Container startet, aber die Heizung antwortet nicht

| Ursache | Prüfung |
|---|---|
| Falscher Port | Diagnose → BACnet-Bus: kommen überhaupt Frames an? |
| Falsche Baudrate | 38400? (Werksdefault 76800) |
| Falsche Geräte-ID | 1 (Standard) |
| Adapter nicht erkannt | Schritt 4 wiederholen |

### Logs ansehen

| Weg | Datei |
|---|---|
| Paket-Log | `/tmp/ComfoBoxGateway.log` |
| Container-Log | `/var/packages/ComfoBoxGateway/var/logs/` |
| Live | Container Manager → `comfobox-gateway` → *Protokoll* |
| Ohne SSH | Web-Oberfläche → **Diagnose** → *Protokoll* |

### Alles zurücksetzen

```bash
sudo synopkg stop ComfoBoxGateway
sudo synopkg uninstall ComfoBoxGateway
```

> **Die Konfiguration und der Schreibzähler bleiben erhalten** – ein
> Deinstallieren setzt den 24-h-Schreibzähler **nicht** zurück. Das ist
> Absicht: er schützt die Elektronik der Heizung.

---

## Nächste Schritte

| Ziel | Anleitung |
|---|---|
| MQTT-Broker einrichten | [MQTT](04-mqtt.md) |
| Werte in Home Assistant | [Home Assistant](05-home-assistant.md) |
| Betrieb und Überwachung | [Monitoring](08-monitoring.md) |
| Allgemeine Fehlersuche | [Fehlersuche](09-fehlersuche.md) |
