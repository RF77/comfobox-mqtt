# `conf/privilege` – die Rechte des Pakets

> **Diese Datei wird nicht ins SPK gepackt** – nur `conf/privilege` selbst.
> `build-spk.sh` kopiert ausschließlich die `privilege`-Datei.

## Warum es die Datei braucht

Ab **DSM 7.0** erzwingt Synology für jedes Paket eine **niedrigere Berechtigung**
(*„Force lower privilege for package"*, siehe
[Breaking Changes in 7.0](https://help.synology.com/developer-guide/breaking_changes.html)):

> „All packages should provide `conf/privilege` with `package` in `run-as`
> explicitly. Any privileged operation should be accomplished via resource worker."

Fehlt die Datei, gilt das Paket als **Root-Paket** und das Paketzentrum
verweigert die Installation **schon beim Hochladen**:

> *„ComfoBox Gateway kann nicht installiert werden, da es mit Root-Rechten
> ausgeführt wird. Pakete mit Root-Rechten können Befehle als Root-Benutzer
> ausführen und die Sicherheit Ihres Systems gefährden."*

Das ist **kein** Fehler im Paket, sondern die erwartete Reaktion des DSM auf
fehlende Rechtedeklaration.

## Der Aufbau

```json
{
	"defaults":{
		"run-as": "package"
	},
	"username": "sc-ComfoBoxGateway",
	"ctrl-script": [{
		"action": "postinst",
		"run-as": "root"
	}]
}
```

| Feld | Bedeutung |
|---|---|
| `defaults.run-as` | **Pflicht.** `package` = der Paket-Benutzer `sc-ComfoBoxGateway`. `root` ist in DSM 7 **verboten** (`run-as system` wurde entfernt). |
| `username` | Der Paket-Benutzer. Ohne diese Angabe nimmt DSM den Paketnamen. |
| `ctrl-script` | Pro Lifecycle-Skript die Identität. |

## Zwei Fallen, die das DSM mit **Fehler 319** quittiert

> *„invalid package privilege content“*

Das DSM prüft die Datei gegen eine Whitelist. Zwei Dinge führen zu dieser
Meldung – beide sind in **keinem** funktionierenden Referenzpaket zu finden
(geprüft an `SynoCommunity/spksrc`, dem Buildsystem der Community-Pakete).
Der Nachweis kam vom Variantentest des Diagnose-Skripts auf einem echten
DSM 7.4.1.

### 1. `groupname` – **das ist der Auslöser**

Keines der Referenzpakete führt ein `groupname`. Die DSM prüft es gegen eine
Whitelist und lehnt die Datei ab, wenn es gesetzt ist.

> **Also:** `groupname` **weglassen**. DSM nimmt dann seinen eigenen Default.

### 2. Die Actions `prestart` / `prestop`

Kein Referenzpaket führt sie. Die erlaubten Actions sind `preinst`,
`postinst`, `preuninst`, `postuninst`, `preupgrade`, `postupgrade`, `start`,
`stop`, `status`.

`prestart` / `prestop` stehen zwar in der alten DSM-Doku (2016) noch drin,
werden von DSM 7.4 aber nicht mehr akzeptiert. Sie werden deshalb **nicht**
über `ctrl-script` gesteuert – `precheckstartstop="yes"` in der `INFO` sorgt
dafür, dass sie trotzdem laufen, nur eben als `sc-ComfoBoxGateway`. Das
`prestart` prüft nur, ob der Container Manager läuft (liest nur) – das
gelingt dem Paket-Benutzer.

### 3. Windows-Zeilenenden (CRLF) – zusätzlich riskant

Das DSM verträgt **kein** `\r` in der Datei. Die Referenzpakete entstehen mit
`jq` unter Linux und haben deshalb immer LF.

> **Windows-Falle:** mit `core.autocrlf=false` schreibt Git die CRLF
> **unverändert** in den Blob – auch der Linux-CI-Build packt sie dann so
> ins SPK. Deshalb konvertiert `build-spk.sh` die Datei **immer** auf LF
> (`tr -d '\r'`), und `tools/check-spk.sh` prüft das.

## Die Form, die DSM 7 erzeugt

Für **DSM ≥ 7** erzeugt `SynoCommunity/spksrc` die Datei so:

```json
{
  "defaults": {
    "run-as": "package"
  },
  "username": "sc-<paket>"
}
```

**Ohne** `ctrl-script`. Der Grund steht in den *Breaking Changes in 7.0*:

> „All packages should provide `conf/privilege` with `package` in `run-as`
> explicitly. **Any privileged operation should be accomplished via resource
> worker.**“

### Die Ground Truth: das Plex-Paket auf der NAS

Das auf dieser NAS **funktionierende** Plex-DSM7-Paket
(`PlexMediaServer-…-x86_64_DSM7.spk`) hat:

````json
{
    "defaults":{
        "run-as": "package"
    },
    "username": "PlexMediaServer",
    "groupname": "PlexMediaServer"
}
````

und dazu eine `conf/resource`:

````json
{
        "video-driver": {},
        "port-config": "conf/PlexMediaServer.sc"
}
````

Zwei Unterschiede zu unserem Paket:

1. **kein `ctrl-script`** – DSM 7.4 verweigert die Root-Rechte in der
   `privilege`-Datei offenbar für Drittanbieter
2. **eine `conf/resource` ist vorhanden** – alle DSM-7-Pakete haben eine
   (spksrc legt sie immer an, notfalls als `{}`)

Privilegierte Operationen gehören also in den **Resource-Worker**
(`conf/resource`) – nicht in ein root-Skript:

| Aufgabe | DSM-7-Weg |
|---|---|
| systemd-Unit | Datei nach `conf/systemd/pkg-<name>` – das Framework kopiert sie selbst nach `/usr/local/lib/systemd/system` |
| Container | `conf/resource` mit dem **Docker-Worker** (`"docker"`) – er legt Compose-Datei, Volume, Image und Container an und startet/stoppt sie |
| Port | `conf/resource` mit `"port-config"` |
| udev-Regel | **Problemfall** – dafür gibt es keinen Worker. Alternative: den stabilen by-id-Pfad des Adapters verwenden |

## Warum die Lifecycle-Skripte `root` brauchen

`defaults.run-as: package` gilt für die **Daten** des Pakets. Die
Lifecycle-Skripte (`preinst`, `postinst`, …) brauchen echte Root-Rechte, weil
sie auf dem NAS systemweite Dinge anlegen:

| Skript | Tut was | Braucht |
|---|---|---|
| `preinst` | prüft DSM-Version, Container Manager, freien Port 8081 | root |
| `postinst` | schreibt `/etc/udev/rules.d/99-comfobox-serial.rules` und `/etc/systemd/system/comfobox-gateway.service`, setzt Eigentümer (uid 1001) | root |
| `preuninst` / `postuninst` | stoppt den Container, entfernt udev-Regel und Unit | root |
| `start` / `stop` / `status` | steuern den Docker-Daemon des Container Managers (Socket-Zugriff) | root |

> **Ohne** `run-as: root` bei `ctrl-script` würden diese Skripte als
> `sc-ComfoBoxGateway` laufen und **an den Root-Pfaden scheitern** – der
> Container würde nie starten.

Die Alternative wäre der Weg über die **Resource-Worker** der DSM
(`docker`-Worker u. a.). Das wäre der sauberere Weg, verlangt aber eine
`conf/resource` mit Zugangsrechten auf den Container-Manager – das ist ein
deutlich größerer Umbau und für dieses Paket nicht nötig, weil der Container
selbst **unprivileged** läuft (`USER comfobox`, kein `privileged`, kein
Docker-Socket im Container).

## Was das Package Center anzeigt

Der Paket-Benutzer `sc-ComfoBoxGateway` erscheint in DSM unter
*Control Panel → Shared Folder → Edit → Permission → System internal user*.
Es ist **kein** Anmeldungskonto.

## Wenn es trotzdem nicht klappt

`tools/dsm-diagnose.sh` (läuft auf der NAS) probiert die möglichen Varianten
der `privilege`-Datei **durch** und meldet, welche funktioniert:

````bash
sudo sh dsm-diagnose.sh /tmp/ComfoBoxGateway_noarch-dsm7_1.0.1-0003.spk --test
````

Der Test läuft in `prepare_install` – ein Fehler dort ändert nichts an der
NAS. Die erste funktionierende Variante installiert das Paket.
