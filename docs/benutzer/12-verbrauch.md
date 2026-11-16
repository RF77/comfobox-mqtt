---
title: Verbrauch und Effizienz
description: kWh-Zähler, COP und die Wärmemengenmessung am Gerät
order: 120
icon: pi pi-chart-line
tags:
  - energie
  - cop
---

# Verbrauch und Effizienz

Das Gateway liest die Zähler Ihrer ComfoBox und zeigt, wie viel Wärme die Anlage
erzeugt, wie viel Strom sie dafür braucht und wie gut das Verhältnis ist.

> **Das Wichtigste zuerst:** Wärmemenge und COP entstehen **im Regler**, nicht im
> Gateway. Ist am Gerät die **Wärmemengenmessung** ausgeschaltet, rechnet der
> Regler nicht – dann fehlen diese Werte. Sie stehen **nicht** auf 0, sondern sind
> „nicht verfügbar". Wie Sie das ändern, steht unter
> [Wärmemengenmessung einschalten](#wärmemengenmessung-einschalten).

---

## Welche Werte es gibt

### Wärme

| Wert | Einheit | Bedeutung |
|---|---|---|
| Wärmeleistung Heizung | kW | Wärme, die **gerade** in den Heizkreis geht |
| Wärmeleistung Warmwasser | kW | Wärme, die **gerade** ins Warmwasser geht |
| Wärmemenge Heizung | kWh | Zähler: Wärme im Heizbetrieb seit der Inbetriebnahme |
| Wärmemenge Warmwasser | kWh | Zähler: Wärme für das Warmwasser |
| Wärmemenge gesamt | kWh | Zähler: beides zusammen |

### Strom

| Wert | Einheit | Bedeutung |
|---|---|---|
| Stromzähler Heizung | kWh | elektrische Energie des Verdichters im Heizbetrieb |
| Stromzähler Warmwasser | kWh | elektrische Energie für das Warmwasser |
| Stromzähler gesamt | kWh | beides zusammen |

> Die Zähler laufen **nur vorwärts** und beginnen bei der Inbetriebnahme der Anlage.
> Über das Gateway lassen sie sich nicht zurücksetzen.

### Effizienz

| Wert | Einheit | Bedeutung |
|---|---|---|
| COP Heizung | – | Wärme geteilt durch Strom, nur Heizbetrieb |
| COP Warmwasser | – | Wärme geteilt durch Strom, nur Warmwasser |
| COP gesamt | – | Wärme geteilt durch Strom, alles zusammen |
| Momentan-COP | – | Wirkungsgrad des letzten Zeitfensters – diesen Wert **rechnet das Gateway** |

### Durchfluss und Freigabe

| Wert | Einheit | Bedeutung |
|---|---|---|
| Durchfluss Heizung | m³/h | Wassermenge, die der Regler für die Wärmemengenmessung erfasst |
| Durchfluss Warmwasser | m³/h | dasselbe für den Warmwasserkreis |
| Wärmemengenmessung | – | aus · ein · ein, Warmwasser separat |

---

## Was der COP aussagt

Der COP (die Leistungszahl) sagt, wie viel Wärme Sie aus einer Kilowattstunde
Strom bekommen. Ein COP von 3,5 heißt: aus 1 kWh Strom werden 3,5 kWh Wärme. Je
höher der Wert, desto besser.

Der Wert hängt vor allem an den Temperaturen:

| Einfluss | Wirkung |
|---|---|
| kalte Außenluft | schlechter, weil die Anlage mehr „heben" muss |
| niedrige Vorlauftemperatur (Fußbodenheizung) | besser |
| hohe Warmwasser-Solltemperatur | schlechter |
| Verschmutzung, Kältemittelmangel, defekte Fühler | schlechter |

### Was ein guter COP ist

Orientierung für eine Wärmepumpe mit Fußbodenheizung:

| COP Heizung | Bewertung |
|---|---|
| über 4,0 | sehr gut |
| 3,0 bis 4,0 | gut |
| 2,5 bis 3,0 | üblich bei hohen Vorlauftemperaturen oder Heizkörpern |
| unter 2,5 | zu niedrig – Anlage prüfen lassen |

| COP Warmwasser | Bewertung |
|---|---|
| 2,0 bis 3,0 | üblich – Warmwasser ist immer schlechter als Heizen |
| unter 1,8 | prüfen: hohe Solltemperaturen, Legionellenschaltung, Zusatzheizer |

> Diese Bereiche sind eine **Orientierung**, keine Grenzwerte. Ein Altbau mit
> Heizkörpern erreicht weniger als ein Neubau mit Flächenheizung. Wichtig ist
> nicht der einzelne Tag, sondern die Richtung über Monate.

### Die Geräte-COPs sind Durchschnittswerte

Der COP des Reglers ist ein **Mittelwert über die gesamte Laufzeit** – nicht der
Wert von heute. Er entsteht aus den beiden Gesamtzählern: Wärmemenge geteilt
durch Stromzähler.

Das hat drei Folgen:

| Folge | Bedeutung für Sie |
|---|---|
| ein schlechter erster Winter | drückt den Wert noch **jahrelang** |
| ein einzelner schlechter Tag | ist im Geräte-COP **nicht** sichtbar |
| ein langsamer Anstieg über Monate | die Anlage arbeitet besser als früher |

> **Am Anfang ist der Durchschnitt wenig aussagekräftig.** Solange beide Zähler
> klein sind, sagt das Verhältnis mehr über den Startpunkt als über die Anlage.

### Der Momentan-COP

Für den Blick auf „läuft es gerade gut?" rechnet das Gateway einen eigenen Wert:
den **Momentan-COP**. Er entsteht aus dem Zuwachs der beiden Gesamtzähler über
ein Zeitfenster und steht als eigener Wert unter `Status/Derived/CopInstant`
bereit – in der Web-Oberfläche und in Home Assistant als normaler Sensor.

| Eigenschaft | Momentan-COP | COP des Geräts |
|---|---|---|
| Zeitraum | letztes Zeitfenster (etwa eine Stunde) | gesamte Laufzeit |
| reagiert auf Änderungen | ja | nein, sehr träge |
| Herkunft | Gateway rechnet | Regler liefert |
| während die Anlage nicht heizt | „unbekannt" | letzter Durchschnitt |

> **„Unbekannt" ist kein Fehler.** Solange keine Wärme fließt, gibt es keinen
> Momentan-COP. Das Gateway schreibt dann bewusst „unbekannt" statt 0.

---

## Warum ohne Wärmemengenmessung nichts kommt

### Symptom

- Wärmemenge und COP fehlen in der Web-Oberfläche.
- Der Durchfluss steht auf 0.
- In Home Assistant sind die Energie-Entitäten „nicht verfügbar".
- In der Fehlerhistorie steht ein Hinweis zur Wärmemengenmessung.

### Ursache

Der Regler **rechnet** die Wärme, er misst sie nicht. Für diese Rechnung braucht
er drei Dinge:

1. die Freigabe **Wärmemengenmessung**,
2. einen **Durchflussfühler**,
3. die Temperaturfühler **Vorlauf** und **Rücklauf**.

Fehlt die Freigabe, rechnet der Regler nicht: Die Wärmemengen bleiben an ihrem
Startwert stehen und der COP bleibt leer. Das ist der häufigste Grund – und der
einzige, den Sie selbst beheben können.

### Lösung

1. Am Regler die Wärmemengenmessung einschalten.
2. Prüfen, ob der Durchfluss bei laufender Pumpe größer 0 ist.
3. Der Wärmemenge Zeit geben: Sie ist ein Zähler, der bei 0 beginnt.

---

## Wärmemengenmessung einschalten

Am Regler auf der Service- bzw. Inbetriebnahme-Ebene gibt es den Punkt
**Wärmemengenmessung** (Kapitel 6.3.14 im Technikerhandbuch, Datenpunkt 3340):

| Einstellung | Bedeutung |
|---|---|
| 0 | keine Wärmemengenmessung – keine Wärme, kein COP |
| 1 | Wärmemengenmessung ein – Heizung und Warmwasser gemeinsam |
| 2 | Wärmemengenmessung ein – Warmwasser separat |

Wählen Sie **1** oder **2**. Der Unterschied betrifft nur die Aufteilung:

| Einstellung | Durchflussfühler | Folge |
|---|---|---|
| 1 | ein Fühler im Heizkreis | Warmwasser wird aus der gemeinsamen Messung abgeleitet |
| 2 | zusätzlich ein Fühler im Warmwasserkreis | der Durchfluss Warmwasser zeigt einen eigenen Wert |

> Der Gateway schaltet die Messung **nicht** selbst ein. Ohne Durchflussfühler
> würde die Messung zwar „an" stehen, aber weiterhin 0 liefern – und der Fehler
> wäre schwerer zu finden als heute.

### Den Durchflussfühler prüfen

| Beobachtung | Bedeutung |
|---|---|
| Durchfluss größer 0, während die Pumpe läuft | der Fühler arbeitet |
| Durchfluss 0, obwohl die Pumpe läuft | Fühler fehlt, ist nicht angeschlossen oder defekt |
| Durchfluss schwankt stark | Luft im Kreis oder ein loses Kabel |
| Durchfluss Warmwasser bleibt 0 (bei Einstellung 2) | Warmwasser-Fühler prüfen |

> **Die Pumpe muss laufen.** Bei stehender Pumpe ist ein Durchfluss von 0 richtig.

### Was danach zu erwarten ist

| Wann | Was Sie sehen |
|---|---|
| sofort | der Durchfluss zeigt einen Wert, sobald die Pumpe läuft |
| nach wenigen Minuten | die Wärmeleistung ist größer 0, solange geheizt wird |
| nach Stunden | die Wärmemenge steigt, der COP füllt sich |
| nach Wochen | aussagekräftige Durchschnittswerte |

---

## Warum die Werte fehlen statt 0 zu zeigen

„0 kWh" und „COP 0" sind **gültige Werte**. Würde der Gateway sie schreiben,
entstünde in Home Assistant eine flache Statistik bei 0 – und sobald die Messung
später eingeschaltet wird, springt der Zähler und die Energie-Historie ist
dauerhaft verfälscht.

Deshalb gilt: **unbekannt ist nicht null.**

| Zustand | Was Sie sehen |
|---|---|
| Wärmemengenmessung aus | Energie-Werte nicht verfügbar, Hinweis in der Fehlerhistorie |
| Wärmemengenmessung ein, aber keine Werte gelesen | Energie-Werte nicht verfügbar |
| alles in Ordnung | Werte werden angezeigt und fortgeschrieben |

> Der Hinweis in der Fehlerhistorie erscheint **einmal pro Wechsel** – nicht bei
> jedem Abruf. Bleibt er dauerhaft stehen, ist die Ursache noch nicht behoben.

---

## Was der Gateway nicht kann

| Wunsch | Warum das nicht geht |
|---|---|
| die Wärmemenge schätzen | eine geschätzte Wärmemenge wäre eine falsche Abrechnungsgrundlage und würde bei echter Messung einen Sprung erzeugen |
| die Wärmemengenmessung einschalten | dafür braucht es einen Durchflussfühler am Gerät |
| die Zähler zurücksetzen | die Zähler gehören dem Regler |

---

## Prüfliste

| Schritt | Prüfen | Erwartung |
|---|---|---|
| 1 | Wärmemengenmessung am Regler | 1 oder 2, nicht 0 |
| 2 | Durchfluss bei laufender Pumpe | größer 0 |
| 3 | Wärmeleistung im Heizbetrieb | größer 0 |
| 4 | Vorlauf und Rücklauf | Rücklauf kälter als Vorlauf |
| 5 | Wärmemenge über einen Tag | ist gestiegen |

> Bleibt ein Punkt offen, finden Sie die nächsten Schritte unter
> [Fehlersuche](09-fehlersuche.md).

## Nächster Schritt

| Sie möchten … | Lesen Sie |
|---|---|
| wissen, warum ein Raum kalt bleibt | [Warum bleibt ein Raum kalt?](13-warum-bleibt-ein-raum-kalt.md) |
| die Werte in Home Assistant auswerten | [Home Assistant](05-home-assistant.md) |
| die Werte per MQTT weiterleiten | [MQTT](04-mqtt.md) |
