---
title: Warum bleibt ein Raum kalt?
description: Übersteuerung, Zustand und Sollwertart verstehen
order: 130
icon: pi pi-question-circle
tags:
  - diagnose
  - zone
---

# Warum bleibt ein Raum kalt?

Die Anlage läuft, die Pumpe arbeitet – und trotzdem wird es nicht warm. Fast
immer hat das eine von vier Ursachen:

| Ursache | Erkennungszeichen |
|---|---|
| die Betriebsart schließt Heizen aus | Standby oder Nur Warmwasser |
| der Regler rechnet mit einem niedrigen Sollwert | Sollwertart Frostschutz oder reduziert |
| eine Übersteuerung hebt die Vorgabe auf | Ferien, Eco, Heizzeiten aus, Störung der Wärmepumpe |
| die Wärme kommt nicht an | Vorlauf und Rücklauf fast gleich |

Das Gateway zeigt die Werte, mit denen Sie diese Fälle auseinanderhalten.

> **Die ComfoBox regelt einen Heizkreis.** Bleibt ein **einzelner** Raum kalt,
> während andere warm werden, ist das kein Regelungsproblem des Reglers – dann
> liegt es an der Verteilung (Heizkörper, Ventile, Abgleich). Siehe
> „Wenn nur ein Raum kalt bleibt".

---

## Die Werte, mit denen Sie prüfen

| Wert | Was er Ihnen sagt |
|---|---|
| Betriebsart | ob die Anlage überhaupt heizen **darf** |
| Sollwertart | mit welchem Sollwert der Regler gerade rechnet |
| Übersteuerung | was gerade **Vorrang** vor dem Zeitprogramm hat |
| Zustand | interner Zustand des Reglers – als Verlauf nutzbar |
| Raumtemperatur und aktueller Sollwert | ob die Regelung überhaupt eine Differenz sieht |
| Vorlauf und Rücklauf | ob die erzeugte Wärme abgegeben wird |
| Warmwasser-Zustand und Warmwasser-Übersteuerung | ob das Warmwasser die Heizung blockiert |

---

## 1. Betriebsart

Die Betriebsart bestimmt, ob die Anlage heizt.

| Betriebsart | Bedeutung |
|---|---|
| Manuell | keine Automatik – die Anlage folgt der manuellen Vorgabe |
| Standby | Anlage aus, nur Frostschutz bleibt aktiv |
| Nur Warmwasser | Heizung aus, Warmwasser wird bereitet |
| Normal/Frost | Heizbetrieb nach Zeitprogramm, außerhalb der Zeiten Frostschutz |
| Normal/Reduziert | Zeitprogramm wechselt zwischen normal und reduziert |
| Dauernd normal | Heizbetrieb ohne Zeitprogramm |
| Dauernd reduziert | nur reduzierter Sollwert |

**Was Sie tun:** Steht die Betriebsart auf **Standby** oder **Nur Warmwasser**,
ist der kalte Raum erklärt. Wählen Sie die gewünschte Betriebsart, z. B.
*Normal/Reduziert*.

---

## 2. Sollwertart

Die Sollwertart sagt, welchen Sollwert der Regler **gerade** verwendet.

| Sollwertart | Bedeutung |
|---|---|
| Frostschutz | nur der Frostschutz-Sollwert – es wird kaum geheizt |
| Reduziert | der abgesenkte Sollwert (Nacht, Abwesenheit) |
| Normal | der normale Sollwert |
| Legionellen | der erhöhte Sollwert für die Legionellen-Aufheizung (nur Warmwasser) |

**Was Sie tun:** Stellen Sie den Sollwert der passenden Art neben die
Raumtemperatur.

| Beobachtung | Deutung |
|---|---|
| Raumtemperatur liegt deutlich unter dem Sollwert | die Anlage müsste heizen – Ursache liegt woanders |
| Raumtemperatur liegt am oder über dem Sollwert | kein Fehler: der Sollwert ist zu niedrig gewählt |
| Sollwertart ist Frostschutz oder reduziert | Erklärung gefunden – Zeitprogramm bzw. Betriebsart prüfen |

---

## 3. Übersteuerung – der häufigste Grund

Die Übersteuerung sagt, was gerade **Vorrang** vor dem normalen Zeitprogramm hat.
Steht dort etwas anderes als „ohne", gilt nicht der Sollwert, den Sie eingestellt
haben.

### Übersteuerungen der Heizung

| Übersteuerung | Bedeutung | Was Sie tun |
|---|---|---|
| ohne | keine Übersteuerung – normaler Betrieb | – |
| Party | der Sollwert wird für die Partyzeit angehoben | endet von selbst |
| Eco | der Sollwert wird zum Sparen abgesenkt | Eco-Zeit prüfen oder beenden |
| Ferien | die Anlage fährt auf Frostschutz | Ferienzeit prüfen und beenden |
| Fernbedienung | ein Bediengerät gibt den Sollwert vor | Bediengerät prüfen |
| Externer Eingang | ein Kontakt am Gerät gibt etwas vor | belegten Eingang prüfen |
| Inbetriebnahme | die Anlage steht im Handbetrieb | Inbetriebnahme beenden |
| Taupunktwächter | Heizen wird gesperrt, damit kein Kondensat entsteht | betrifft den Kühlbetrieb |
| Bodenaustrocknung | das Estrich-Programm fährt den Vorlauf hoch | Programmende abwarten |
| Störung der Wärmepumpe | der Verdichter ist gesperrt | Fehlerspeicher am Display lesen, Fachbetrieb |
| Optimierung Wärmepumpen-Ausschaltung | der Regler hält die Wärmepumpe zurück | meist kurzzeitig, beobachten |
| Raumsoll-Überhöhung Solar | die Solaranlage hebt den Sollwert an | kein Fehler |
| Sollwertschiebung | der Sollwert wird um einen Betrag verschoben | Verschiebung prüfen |
| Heizzeiten aus | das Zeitprogramm hat das Heizen abgeschaltet | Zeitprogramm prüfen |
| Außentemperatur | der Regler hält Heizen für unnötig | Heizgrenze prüfen |
| Entlüftung Sekundärkreis | die Anlage entlüftet, das Heizen pausiert | endet von selbst |
| Nur Zusatzheizung | die Wärmepumpe ist gesperrt, es heizt nur der elektrische Heizstab | Fehlerspeicher lesen, Fachbetrieb |

> **Typische Reihenfolge:** Ferien → Heizzeiten aus → Eco → Außentemperatur.
> Diese vier erklären die meisten kalten Räume im Winter.

---

## 4. Warmwasser blockiert die Heizung

Solange Warmwasser bereitet wird, pausiert das Heizen oft – das ist Absicht
(Vorrang). Bei knapper Leistung kann das Warmwasser die Heizung längere Zeit
übersteuern.

### Zustand des Warmwassers

| Zustand | Bedeutung |
|---|---|
| aus | Warmwasser ist abgeschaltet |
| Warmwasser mit Energieerzeuger | die Wärmepumpe lädt den Speicher |
| Warmwasser elektrisch | der elektrische Heizstab lädt |
| Warmwasser mit Zusatzheizung | Wärmepumpe und Heizstab laden gemeinsam |
| Warmwasser-Entladeschutz | der Speicher wird gegen Auskühlen geschützt |
| Sperre Energieerzeuger | die Wärmepumpe darf nicht laden |

### Übersteuerung des Warmwassers

| Übersteuerung | Bedeutung |
|---|---|
| ohne | keine Übersteuerung – normaler Betrieb |
| Ferien | Ferienabsenkung aktiv |
| Party | der Warmwasser-Sollwert wird angehoben |
| Externer Eingang | ein Kontakt am Gerät gibt eine Ladung vor |
| Störung der Wärmepumpe | der Verdichter ist gesperrt |
| Optimierung | der Regler verschiebt die Ladung |
| Sollwertschiebung | der Sollwert wird verschoben |
| Heizzeiten aus | die Ladung ist abgeschaltet |
| Vorlauf zu hoch | die Ladung pausiert, weil der Vorlauf über dem Grenzwert liegt |
| Warmwasser elektrisch | es lädt nur der Heizstab |
| Optimierung Warmwasser elektrisch | der Heizstab wird zeitlich verschoben |
| Sperre Energieerzeuger | die Wärmepumpe ist gesperrt |
| Nur Zusatzheizung | es lädt nur der Heizstab |

**Was Sie tun:** Ist das Warmwasser kalt, prüfen Sie den Warmwasser-Zustand und
die Übersteuerung. Steht die Heizung wegen einer dauerhaften Warmwasser-Ladung
still, liegt es häufig an einem hohen Warmwasser-Sollwert oder an einer
Legionellen-Aufheizung.

> **Legionellen-Aufheizung:** Sie läuft nur zeitweise und hebt den Sollwert stark
> an. In dieser Zeit kann das Heizen pausieren.

---

## 5. Vorlauf und Rücklauf prüfen

Die beiden Temperaturen zeigen, ob die erzeugte Wärme überhaupt abgegeben wird.

| Temperatur | Bedeutung |
|---|---|
| Vorlauf | das Wasser, das vom Erzeuger in den Heizkreis geht |
| Rücklauf | das Wasser, das aus dem Heizkreis zurückkommt |

| Beobachtung | Deutung | Nächster Schritt |
|---|---|---|
| Vorlauf deutlich über Rücklauf (etwa 5 bis 15 K) | Wärme wird abgegeben – normal | – |
| Vorlauf und Rücklauf fast gleich, obwohl geheizt werden soll | es fließt kein Wasser | Pumpe, Mischer und Ventile prüfen |
| Rücklauf wärmer als Vorlauf | Fühler vertauscht oder Pumpe steht | Fachbetrieb |
| beide kalt, Sollwert hoch | die Anlage erzeugt keine Wärme | Betriebsart, Übersteuerung, Fehlerspeicher |
| Vorlauf sehr hoch, Raum bleibt kalt | die Wärme kommt nicht an | Verteilung prüfen, siehe nächster Abschnitt |

> **Ohne Volumenstrom keine Wärmeabgabe.** Auch eine perfekte Vorlauftemperatur
> kommt nicht im Raum an, wenn die Pumpe steht oder ein Ventil geschlossen ist.

---

## 6. Der Zustand des Reglers

Der Zustand ist eine interne Zahl des Reglers; ihre Bedeutung ist nicht
dokumentiert und die Zahlen sind nicht über alle Firmware-Stände gleich.

Nutzen Sie ihn deshalb als **Verlauf**, nicht als Anzeige:

| Beobachtung | Deutung |
|---|---|
| der Zustand ändert sich, wenn die Anlage heizen soll | der Regler arbeitet ein Programm ab |
| der Zustand bleibt über Stunden gleich, obwohl es kalt ist | die Anlage steht – Betriebsart und Übersteuerung prüfen |

---

## 7. Was Sie zuerst prüfen

| Reihenfolge | Prüfung | Wenn das die Ursache ist |
|---|---|---|
| 1 | Betriebsart | Standby oder Nur Warmwasser beenden |
| 2 | Übersteuerung | Ferien, Eco oder Heizzeiten aus beenden |
| 3 | Sollwertart und Sollwert | Zeitprogramm und Raumsollwert anpassen |
| 4 | Warmwasser-Zustand | Warmwasser-Sollwert senken, Ladung abwarten |
| 5 | Vorlauf und Rücklauf | Pumpe und Ventile prüfen |
| 6 | Fehlerspeicher am Display | Störung notieren und Fachbetrieb rufen |

> **Bevor Sie an Sollwerten drehen:** Prüfen Sie zuerst die Übersteuerung. Ein
> höherer Sollwert hilft nicht, wenn die Anlage wegen Ferien oder Heizzeiten aus
> gar nicht heizt.

---

## Wenn nur ein Raum kalt bleibt

Die ComfoBox regelt **einen** Heizkreis – sie sieht keine einzelnen Räume. Bleibt
ein Raum kalt, obwohl die Vorlauftemperatur stimmt:

| Ursache | Erkennungszeichen |
|---|---|
| Heizkörper-Thermostat geschlossen | andere Räume werden warm, dieser nicht |
| Ventil hängt oder ist abgesperrt | Vor- und Rücklauf am Heizkörper kalt |
| hydraulischer Abgleich fehlt | entfernte Räume werden schlechter versorgt |
| Luft im Heizkörper | Heizkörper wird oben warm, unten kalt |

Diese Fälle löst der Fachbetrieb – über das Gateway sind sie nicht behebbar.

---

## Wann Sie den Fachbetrieb brauchen

| Symptom | Grund |
|---|---|
| Übersteuerung „Störung der Wärmepumpe" bleibt stehen | der Verdichter ist gesperrt |
| Rücklauf wärmer als Vorlauf | Fühler vertauscht oder Pumpe defekt |
| Vorlauf und Rücklauf gleich, Pumpe läuft hörbar | Mischer oder Ventil defekt |
| Heizbetrieb, aber keine Wärmeleistung über Stunden | Kältekreis oder Regelung |
| Fehlerspeicher am Display meldet einen Fehler | Gerätefehler, siehe [Fehlersuche](09-fehlersuche.md) |

> **Bereiten Sie den Anruf vor:** Betriebsart, Übersteuerung, Vorlauf- und
> Rücklauftemperatur sowie die Meldung im Display nennen – damit ist die Ursache
> meist in einem Termin geklärt.

## Nächster Schritt

| Sie möchten … | Lesen Sie |
|---|---|
| Verbrauch und Effizienz verstehen | [Verbrauch und Effizienz](12-verbrauch.md) |
| Sollwerte und Betriebsart ändern | [Bedienung](06-bedienung.md) |
| systematisch Fehler suchen | [Fehlersuche](09-fehlersuche.md) |
