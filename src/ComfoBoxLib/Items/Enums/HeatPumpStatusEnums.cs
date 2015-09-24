// /*******************************************************************************
//  * Copyright (c) 2015 by RF77 (https://github.com/RF77)
//  * All rights reserved. This program and the accompanying materials
//  * are made available under the terms of the Eclipse Public License v1.0
//  * which accompanies this distribution, and is available at
//  * http://www.eclipse.org/legal/epl-v10.html
//  *
//  * Contributors:
//  *    RF77 - initial API and implementation and/or initial documentation
//  *******************************************************************************/ 

using System.ComponentModel;

namespace ComfoBoxLib.Items.Enums
{
    //TODO: English translations?
    public enum HeatPumpStatusEnums
    {
        [Description("Keine Energieerzeugung")] WpOff = 0,
        [Description("WP ist aufgrund Frostschutz ausgeschaltet, Energieerzeugung mit alternativer Energie")] WpFrostschutz = 1,
        [Description("Maximalbegrenzung überschritten")] WpVorlaufZuHoch = 2,
        [Description("WP gesperrt durch Aussentemperatur")] SperreDurchBivalenz = 3,
        [Description("WP-Temperatur zu tief Minimalbegrenzung beim Kühlen unterschritten")] WpTempZuTief = 4,
        [Description("Freie Kühlung Kühlung mit Lüftung")] AirCooling = 5,
        [Description("Verdampfertemp zu tief Minimale Verdapfertemperatur unterschritten")] VerdampfertempZuTief = 6,
        [Description("Passive Kühlung aktiv")] FreecoolingActive = 9,
        [Description("WP im Heizbetrieb")] HeatingWithHeatPump = 10,
        [Description("Hochdruckeingang aktiv, noch keine Störung")] Hochdruckstoerung = 11,
        [Description("Niederdruckeingang aktiv, noch keine Störung")] Niederdruckstoerung = 12,
        [Description("Eingang Sicherheitskette aktiv, noch keine Störung")] Sicherheitskettenstoerung = 13,
        [Description("Eingang Wärmequellenstörung aktiv, noch keine Störung")] Wärmequellenstoerung = 14,
        [Description("Einschaltverzögerung nach Stromausfall")] StartDelayAfterBlackout = 15,
        [Description("Wiedereinschaltverzögerung Kompressor")] StartDelayHeatPump = 16,
        [Description("EW-Sperre aktiv")] EwSperre = 17,
        [Description("Vorlaufzeit der Primärpumpe resp. des Verdampfer-Ventilators")] PreRunningPrimaryPump = 18,
        [Description("Nachlaufzeit der Primärpumpe resp. des Verdampfer-Ventilators")] PostRunningPrimaryPump = 19,
        [Description("Vereisung wird überwacht")] AbtauUeberwachung = 20,
        [Description("Abtauung nicht möglich, Verzögerung aktiv")] Abtauverzoegerung = 21,
        [Description("Abtauung im Gange")] Abtauen = 22,
        [Description("Stillstandszeit nach einr Abtauung (Abtropfzeit)")] Abtaustillstand = 23,
        [Description("Zeit für die Aufnahme der ΔT Referenz")] DelayForReferenceMeasurement = 24,
        [Description("Abtausperrzeit nach einer Abtauung")] Abtausperrzeit = 25,
        [Description("Abtauung manuell ausgelöst")] ManuellAbtauen = 26,
        [Description("Abtauung durch externen Kontakt")] ExtAbtauen = 27,
        [Description(" Abtauen mit dem Ventilator")] AbtauenMitVentilator = 28,
        [Description("Abtauen mit Ventilator durch externen Kontakt")] ExtAbtauenMitVenti = 29,
        [Description("Aktive Kühlung")] ActiveCooling = 30,
        [Description("Aktive Kühlung und gleichzeitige passive WWLadung")] ACtiveCoolingWithPassiveWarmWater = 31,
        [Description("Warmwasserladung mit WP")] WarmWater = 40,
        [Description("Frostgefahr in WP, WP ist ausgeschaltet")] Frostgefahr = 41,
        [Description("Absauggfunktion aktiv")] Absaugung = 42,
        [Description("Entlastungsfunktion aktiv")] Entlastung = 43,
        [Description("MOP-Funktion aktiv")] MOP = 44,
        [Description("Stillstand Umschaltung: Stillstand während Umschaltung von Heizen auf Abtauen resp. Kühlen")] StillstandUmschaltung = 45,
        [Description("Kondensat‘frostschutz: Kondensatortemperatur während Abtauung unter Frostschutzstörgrenze")] KondensatFrostschutz = 46,
        [Description("Taupunktwächter Taugefahr, Mischerkreis schliesst, direkte Kreispumpe schaltet ab")] TaupunktwaechterTaugefahr = 47,
        [Description("Ventilator gesperrt aufgrund der Verdampfertemperatur")] VentSperreDurchVerdampferTemp = 48,
        [Description("Abtauung nicht erfolgreich, zyklisches Abtauen aktiv")] ErfolgloseAbtauung = 49,
        [Description("Schwimmbadladung aktiv")] SchwimmbadLaden = 50,
        [Description("Vorlaufzeit Kondensatorpumpe")] VorlaufzeitKondPumpe = 51,
        [Description("Kondensatortemperatur während Abtauung unter Frostschutzwarngrenze")] KondFrostWarnung = 52,
        [Description("Heissgaswarnschelle überschritten")] HeissgasWarn = 53,
        [Description("Heissgasstörschwelle überschritten")] HeissgasStoer = 54,
        [Description("Primärkreispumpe aktiv während Entlüftungsfunktion")] EntlueftungPrimaerkreis = 55,
        [Description("Heizen nur mit Zusatzheizung")] NurZusatzheizung = 56,
        [Description("Abtauung mit Ventilator während Ruhephase Kompressor")] OffzeitAbtauen = 57,
        [Description("Minimale Laufzeit Verdichter während der Umschaltung auf Abtauen resp. Kühlen")] VerdiMinTUmschAbtK = 58,
        [Description("Differenzdruck zu klein für Umschaltung Abtauen resp. Kühlen")] KeinDifferenzdruck = 59,
        [Description("Warmwasser nur mit Zusatzheizung")] WwNurMitZusatzheizung = 60,
        [Description("Hochdruckwarnschwelle überchritten")] HochdruckWarnung = 61,
        [Description("Energieerzeugung")] VerdampferfuehlerDefekt = 62
    }
}