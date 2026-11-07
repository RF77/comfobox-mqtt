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
> `ghcr.io/rf77/comfobox-gateway:1.0.1` aus der GitHub-Registry. Ist das
> Image **privat**, kann die NAS es nicht ziehen – der Container startet
> dann nicht und es passiert gar nichts.

**So prüfen Sie das** – auf der Paket-Seite von GitHub:

<https://github.com/users/RF77/packages/container/comfobox-gateway>

| Antwort | Bedeutung |
|---|---|
| **Public** | alles bereit → weiter mit Schritt 1 |
| **Private** | das Image ist privat → Schritt 0a ist nötig |

> **Nicht** die Adresse
> `https://ghcr.io/v2/rf77/comfobox-gateway/manifests/1.0.0` im Browser
> aufrufen: Sie antwortet **immer** mit `401 Unauthorized` – auch wenn das
> Image öffentlich ist. Die Registry verlangt für jede Anfrage einen Token,
> und den kann der Browser nicht selbst anfordern. Ein `401` an dieser
> Adresse sagt also **nichts** über die Sichtbarkeit aus.

**Wenn Sie es genau wissen wollen** (ohne Anmeldung, also so, wie es die NAS
sieht) – auf einem Rechner mit Docker:

```bash
docker pull ghcr.io/rf77/comfobox-gateway:1.0.1
```

| Antwort | Bedeutung |
|---|---|
| `Status: Downloaded newer image` | das Image ist öffentlich → weiter mit Schritt 1 |
| `unauthorized: authentication required` | das Image ist privat → Schritt 0a ist nötig |

Ohne Docker geht es mit zwei Befehlen – der erste holt einen anonymen Token,
der zweite fragt das Manifest damit ab:

```bash
TOKEN=$(curl -s "https://ghcr.io/token?scope=repository:rf77/comfobox-gateway:pull&service=ghcr.io" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.oci.image.index.v1+json" \
  "https://ghcr.io/v2/rf77/comfobox-gateway/manifests/1.0.0"
```

`200` = öffentlich, `401` = privat.

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

> **Ab Schritt 4 läuft der Image-Workflow.** Er prüft am Ende, ob sich das
> Image **anonym** ziehen lässt – genau das, was die NAS tut. Der Job wird
> rot, wenn das nicht klappt; das Ergebnis steht im Workflow-Protokoll.

---

## Schritt 1 – Paket installieren

> **Empfohlen: über eine Paketquelle.** Dann müssen Sie **keine Datei auf die
> NAS kopieren** – das Paketzentrum lädt das Paket selbst und kennt den
> Herausgeber. Die manuelle Installation einer lokalen Datei ist der Weg, an
> dem DSM 7.4 mit *„Dieses Paket wurde von einem unbekannten Herausgeber
> veröffentlicht“* abbricht (siehe unten).

### Variante A – Paketquelle (empfohlen)

1. **Paketcenter → Einstellungen** (Zahnrad oben rechts) → Registerkarte
   **Paketquellen** → **Hinzufügen**.
2. Name: `ComfoBox`
3. Speicherort (URL):
   <https://github.com/RF77/comfobox-mqtt/releases/download/v1.0.1/packages.json>
4. **OK** → das Paket erscheint in der Liste **Community**.
5. **ComfoBox Gateway** anklicken → **Installieren**.

> Die Adresse enthält die Release-Version – bei einem neuen Release ändert
> sich der Teil hinter `download/`. Das Paketzentrum braucht eventuell einen
> Moment, bis es die Quelle neu einliest.

### Variante B – Datei von Hand

Das Paket aus dem neuesten [Release](https://github.com/RF77/comfobox-mqtt/releases)
herunterladen und auf die NAS kopieren (Samba-Freigabe oder `scp`), dann
**Paketcenter → Installieren → Manuelle Installation → Datei wählen**.

> **Wichtig:** Auf der NAS darf **nur eine** `.spk` liegen. Das Paketzentrum
> installiert die Datei, die Sie im Dateiauswahler anklicken – und auf der NAS
> liegen oft mehrere Versionen nebeneinander. Eine **ältere** Datei ohne die
> Datei `conf/privilege` wird vom DSM abgelehnt (Schritt 2). Löschen Sie
> deshalb zuerst alle älteren `.spk` auf der NAS.

**Prüfen, ob die Datei die richtige ist** – auf einem Rechner mit Bash:

````bash
tar tf ComfoBoxGateway_noarch-dsm7_1.0.1-0007.spk | grep conf
# erwartet:  conf/
#            conf/privilege
#            conf/resource
````

Fehlt `conf/privilege`, ist die Datei zu alt. Fehlt `conf/resource`, startet
der Container nicht.

> **Wichtig vor dem ersten Installationsversuch:** Ist auf der NAS bereits
> ein **fehlgeschlagenes** Paket installiert, steht es im Status „broken“.
> DSM 7.4 versucht dann ein Update statt einer Neuinstallation und
> scheitert an *„package does not have preupgrade / postupgrade script“*.
> **Vorher deinstallieren:** *Paketcenter → ComfoBox Gateway →
> Deinstallieren* (bei Status „Installiert“ geht das direkt).

### Wenn das Paketzentrum das Paket ablehnt

Das DSM nennt im Dialog nur die Ursache, nicht den Grund. Zwei Meldungen
bedeuten zwei **verschiedene** Dinge:

| Meldung | Bedeutung | Lösung |
|---|---|---|
| „Dieses Paket wurde von einem **unbekannten Herausgeber** veröffentlicht“ | Das DSM kennt den Herausgeber nicht – weil das Paket **nicht** über eine Paketquelle kam | **Variante A** verwenden: *Paketcenter → Einstellungen → Paketquellen → Hinzufügen*. Danach das Paket aus der Liste **Community** installieren. |
| „Paket kann nicht installiert werden. Versuchen Sie es später erneut …“ | Ein Skript des Pakets oder der DSM-Worker ist gescheitert | Diagnose über SSH (unten) – nur so bekommt man den echten Fehlercode. |

> **Sie müssen dafür nichts am Paket ändern.** Die Meldung zum unbekannten
> Herausgeber betrifft **nicht** die Datei, sondern die Art der Installation:
> Das DSM kann nur bei einer **registrierten Paketquelle** sagen, wer das
> Paket herausgegeben hat. Deshalb ist Variante A (Paketquelle) der Weg, den
> DSM 7.4 vorgesehen hat – und Sie müssen dafür **keine Datei auf die NAS
> kopieren**.

#### 1. SSH auf der NAS aktivieren

*Control Panel → Terminal & SNMP →* **SSH-Dienst aktivieren** (Port 22).

#### 2. Per SSH verbinden

Auf einem Rechner mit SSH (unter Windows: PowerShell oder „Windows
Terminal“):

````bash
ssh admin@<IP-der-NAS>
````

Sie melden sich mit dem DSM-Administrator an (Passwort wie im DSM).

#### 3. Skript und Paket auf die NAS holen

Beide Dateien kommen in `/tmp` der NAS. Sie können entweder die Dateien vom
Rechner kopieren …

````bash
scp dsm-diagnose.sh admin@<IP-der-NAS>:/tmp/
scp ComfoBoxGateway_noarch-dsm7_1.0.1-0007.spk admin@<IP-der-NAS>:/tmp/
````

… oder das Skript direkt auf der NAS herunterladen (das Paket selbst liegt
dann schon auf der NAS, z. B. im Gemeinsamen Ordner):

````bash
cd /tmp
wget https://raw.githubusercontent.com/RF77/comfobox-mqtt/Upgrade/deploy/synology-spk/tools/dsm-diagnose.sh
````

#### 4. Diagnose ausführen

In der SSH-Sitzung:

````bash
sudo sh /tmp/dsm-diagnose.sh /tmp/ComfoBoxGateway_noarch-dsm7_1.0.1-0007.spk --install
````

`sudo` fragt nach dem Passwort des DSM-Administrators. Ohne `--install` liest
das Skript nur (keine Änderung an der NAS).

**Ausgabe:** DSM-Version, der Inhalt des Pakets, die `privilege`-Datei, die
**Log-Auszüge** des DSM und schließlich die JSON-Antwort von
`synopkg install` – dort steht der **Fehlercode**, zum Beispiel:

````json
{"error":{"code":319,"description":"invalid package privilege content"},"stage":"install_failed"}
````

> **Fehlercode 319** heißt: Das DSM liest die Datei `conf/privilege`, lehnt
> aber ihren **Inhalt** ab. Ursache ist in aller Regel der Block `ctrl-script`
> mit `"run-as": "root"`: DSM 7 lässt Pakete von Drittanbietern **nicht** mit
> Root-Rechten laufen. Die **Log-Auszüge** im Skript verraten den genauen
> Grund – dort schreibt das DSM hin, was ihm nicht passt.
>
> Mit `--test` probiert das Skript zusätzlich mehrere Formen der
> `privilege`-Datei **durch** und installiert die erste, die funktioniert:
>
> ````bash
> sudo sh /tmp/dsm-diagnose.sh /tmp/ComfoBoxGateway_noarch-dsm7_1.0.1-0007.spk --test
> ````
>
> Ein Fehler in dieser Phase ändert **nichts** an der NAS.

> Das Skript liegt auch **im Release** (neben dem `.spk`) und im Repository
> unter `deploy/synology-spk/tools/dsm-diagnose.sh`.
>
> **Hinweis:** Der Fehler 319 entsteht **vor** jedem Logeintrag – die
> Log-Auszüge zeigen darum oft nichts. Die entscheidende Information
> liefert der Variantentest (`--test`).

---

## Schritt 2 – Installieren

> **Falls Sie Variante A gewählt haben, ist das Paket jetzt schon
> installiert** – Sie können direkt zu Schritt 3 weiter.

**Paketcenter → Installieren → Manuelle Installation** → Datei wählen →
*Installieren*.

Das Paket prüft vorher selbst (DSM-Version) und richtet die Umgebung ein. Den
Container startet danach das DSM selbst – der Browser öffnet sich
automatisch auf `http://<NAS-IP>:8081/`.

> **Wie der Container startet:** Das Paket startet ihn **nicht** selbst. Es
> übergibt das dem DSM (dem „Worker“ in `conf/resource`). Das DSM startet und
> stoppt den Container beim Start bzw. Stopp des Pakets. Der Grund: Seit
> DSM 7 dürfen Pakete von Drittanbietern **nicht** mit Root-Rechten laufen –
> ein solches Paket lehnt das Paketzentrum mit **Fehler 319** ab.
>
> Deshalb ist im Container-Manager **ein Projekt mit dem Namen `comfobox`**
> zu sehen – kein einzelner Container. Das ist das erwartete Bild.

> **Meldung: „kann nicht installiert werden, da es mit Root-Rechten
> ausgeführt wird"?** Dann ist die `.spk` zu alt – ab DSM 7.0 muss sie die
> Datei `conf/privilege` enthalten. Laden Sie eine neuere Version herunter
> (Schritt 1) und löschen Sie die alte Datei von der NAS. Details in der
> [Fehlersuche](09-fehlersuche.md).

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
