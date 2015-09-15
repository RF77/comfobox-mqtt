﻿// /*******************************************************************************
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
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    public class HeatPump
    {
        /// <summary>
        ///     Read only values
        /// </summary>
        public class States
        {
            /// <summary>
            ///     Aktuelle WP-Leistung
            /// </summary>
            [Secondary]
            public AnalogValue CurrentPower => new AnalogValue(45, "%") {IsReadOnly = true};

            /// <summary>
            ///     Sollwert der WP-Regelung
            /// </summary>
            [Secondary]
            public SpecialAnalogValue HeatPumpSetPoint => new SpecialAnalogValue(210, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kühlvorlauftemp: Istwert Kühlvorlauf bei stetiger Primärpumpe
            /// </summary>
            [Primary]
            public SpecialAnalogValue CoolingFlowTemperature => new SpecialAnalogValue(3098, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Primärtemperatur: Temperatur im Primärkreis
            /// </summary>
            [Primary]
            public SpecialAnalogValue PrimaryTemperature => new SpecialAnalogValue(602, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP-Vorlauftemp: Istwert des WP-Vorlauffühlers
            /// </summary>
            [Primary]
            public SpecialAnalogValue CurrentFlowTemperature => new SpecialAnalogValue(600, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP-Vor’temp Min/Max: Min/Max-Begrenzung auf WP-Vorlauffühler
            /// </summary>
            [Secondary]
            public SpecialAnalogValue MaxFlowTemperature => new SpecialAnalogValue(648, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Ansauglufttemp Aktuelle Ansauglufttemperatur
            /// </summary>
            [Primary]
            public SpecialAnalogValue Ansauglufttemp => new SpecialAnalogValue(3222, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Anzeige des Betriebszustandes der Stufe 1
            /// </summary>
            [Primary]
            public EnumValue<HeatPumpStatusEnums> HeatPumpStatus
                => new EnumValue<HeatPumpStatusEnums>(662) {IsReadOnly = true};

            // Other values?: 
        }
    }

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
        [Description("MOP-Funktion aktiv")] MOP = 44
        //[Description("Energieerzeugung")]
        //WpVorlaufZuHoch = 2,

        //MOP MOP-Funktion aktiv 44
        //Stillstand Umschaltung Stillstand während Umschaltung von Heizen
        //auf Abtauen resp. Kühlen
        //45
        //Kondensat‘frostschutz Kondensatortemperatur während Abtauung
        //unter Frostschutzstörgrenze
        //46
        //Taupunktwächter Taugefahr, Mischerkreis schliesst, direkte
        //Kreispumpe schaltet ab
        //47
        //Vent‘sperre durch Verd‘t Ventilator gesperrt aufgrund der
        //Verdampfertemperatur
        //48
        //Erfolglose Abtauung Abtauung nicht erfolgreich, zyklisches Abtauen
        //aktiv
        //49
        //Schwimmbad laden Schwimmbadladung aktiv 50
        //Vorlaufzeit Kond‘pumpe Vorlaufzeit Kondensatorpumpe 51
        //Kond’frostwarnung Kondensatortemperatur während Abtauung
        //unter Frostschutzwarngrenze
        //52
        //            Heissgas Warn Heissgaswarnschelle überschritten 53
        //Heissgas Stör Heissgasstörschwelle überschritten 54
        //Entlüftung Primärkreis Primärkreispumpe aktiv während
        //Entlüftungsfunktion
        //55
        //Nur Zusatzheizung Heizen nur mit Zusatzheizung 56
        //Offzeit Abtauen Abtauung mit Ventilator während Ruhephase
        //Kompressor
        //57
        //Verdi’min‘t Umsch Abt/K Minimale Laufzeit Verdichter während der
        //Umschaltung auf Abtauen resp. Kühlen
        //58
        //Kein Differenzdruck Differenzdruck zu klein für Umschaltung
        //Abtauen resp. Kühlen
        //59
        //WW nur mit Zusatzheizung Warmwasser nur mit Zusatzheizung 60
        //Hochdr’warnung Hochdruckwarnschwelle überchritten 61
        //Verdapferfüh defekt Verdampferfühler defekt 62
    }
}