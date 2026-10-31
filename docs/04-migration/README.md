# 04 – Migration

## 1. Überblick

Die Migration verfolgt zwei Ziele gleichzeitig:

1. **Verhaltensgleichheit** – der Gateway verhält sich gegenüber MQTT-Subscribern und Home Assistant
   wie bisher (Anforderung 4).
2. **Betriebssicherheit** – die im Ist-Zustand vorhandenen Fehler (A5, A6, A7, A8, A10) sind behoben.

> **Der wichtigste Grundsatz:** Der BACnet-Protokollstack wird **nachweislich** verhaltensgleich
> portiert (Golden-File-Tests), und die Item-/Topic-Zuordnung wird per **Contract-Test** gegen
> `docs/topics.json` festgeschrieben. Ohne diese beiden Tests ist eine Modernisierung nicht
> abnahmefähig.

## 2. Dokumente

| Dokument | Inhalt |
|---|---|
| [roadmap.md](roadmap.md) | Phasen, Aufwände, Abhängigkeiten, Meilensteine |
| [mqtt-kompatibilitaet.md](mqtt-kompatibilitaet.md) | Zusicherung: alle Topic-Adressen bleiben gültig |
| [teststrategie.md](teststrategie.md) | Testpyramide, Golden Files, Contract-Tests |
| [risiken.md](risiken.md) | Risikoregister mit Gegenmaßnahmen |
| [checkliste.md](checkliste.md) | Abnahme-Checklisten pro Phase |

## 3. Zeitschätzung (Gesamt)

| Phase | Dauer (Personentage) |
|---|---|
| 0 – Repo-Hygiene & CI | 2 |
| 1 – BACnet-Portierung + Tests | 8 |
| 2 – Domain & Gateway-Kern | 6 |
| 3 – MQTT-Migration & HA-Discovery | 6 |
| 4 – REST/SSE-API | 4 |
| 5 – Angular/Optimus UI | 8 |
| 6 – Container & Synology | 4 |
| 7 – Parallelbetrieb & Umschaltung | 3 |
| **Summe** | **≈ 41 Personentage** (≈ 8–10 Wochen à 5 Tage) |

> **Puffer:** +25 % für Unbekanntes im BACnet-Stack und für Rückfragen.
> **Realistisch:** 10–13 Wochen.
