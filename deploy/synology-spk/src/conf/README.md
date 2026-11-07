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
  "defaults": { "run-as": "package" },
  "username":  "sc-ComfoBoxGateway",
  "groupname": "sc-ComfoBoxGateway",
  "ctrl-script": [ { "action": "postinst", "run-as": "root" } ]
}
```

| Feld | Bedeutung |
|---|---|
| `defaults.run-as` | **Pflicht.** `package` = der Paket-Benutzer `sc-ComfoBoxGateway`. `root` ist in DSM 7 **verboten** (`run-as system` wurde entfernt). |
| `username` / `groupname` | Der Paket-Benutzer und -Benutzerkreis. Ohne diese Angaben nimmt DSM den Paketnamen. |
| `ctrl-script` | Pro Lifecycle-Skript die Identität. |

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
