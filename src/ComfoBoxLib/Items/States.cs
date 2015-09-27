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
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Items.Enums;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    /// <summary>
    ///     Zustände
    /// </summary>
    [Description("Zustände")]
    public class States
    {
        /// <summary>
        ///     Regler
        /// </summary>
        [Description("Regler")]
        public class Controller
        {
            /// <summary>
            ///     Genauer Reglertyp
            /// </summary>
            [Once]
            [Description(@"Genauer Reglertyp")]
            public AnalogValue ControllerType => new AnalogValue(203, null) {IsReadOnly = true};

            /// <summary>
            ///     Angeschlossener Erweiterungstyp
            /// </summary>
            [Once]
            [Description(@"Angeschlossener Erweiterungstyp")]
            public AnalogValue ExtensionType => new AnalogValue(2044, null) {IsReadOnly = true};

            /// <summary>
            ///     Softwareversion
            /// </summary>
            [Once]
            [Description(@"Softwareversion")]
            public AnalogValue SoftwareVersion => new AnalogValue(2043, null) {IsReadOnly = true};

            /// <summary>
            ///     Das Inbetriebnahme-Datum der Reglers wird
            ///     beim Laden der Applikation gesetzt
            /// </summary>
            [Once]
            [Description(@"Das Inbetriebnahme-Datum der Reglers wird beim Laden der Applikation gesetzt")]
            public DateValue InitialOperationDate => new DateValue(3353, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden des Reglers
            /// </summary>
            [Secondary]
            [Description(@"Betriebsstunden des Reglers")]
            public AnalogValue OperatingHours => new AnalogValue(211, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen Regler-Speisung
            /// </summary>
            [Secondary]
            [Description(@"Anzahl Einschaltungen Regler-Speisung")]
            public AnalogValue NumberOfStartUps => new AnalogValue(212, null) {IsReadOnly = true};

            /// <summary>
            ///     Anzahl der geänderten Parameter seit
            ///     „Applikation laden…“
            /// </summary>
            [Secondary]
            [Description(@"Anzahl der geänderten Parameter seit „Applikation laden…“")]
            public AnalogValue NumberOfParameterChanges => new AnalogValue(617, null) {IsReadOnly = true};

            /// <summary>
            ///     Datum der letzten Parameteränderung
            /// </summary>
            [Secondary]
            [Description(@"Datum der letzten Parameteränderung")]
            public DateValue LastParameterChangeDate => new DateValue(618, null) {IsReadOnly = true};
        }

        /// <summary>
        ///     Zähler
        /// </summary>
        [Description("Zähler")]
        public class Counters
        {
            /// <summary>
            ///     Betriebsstunden Stufe 1
            /// </summary>
            [Secondary]
            [Description(@"Betriebsstunden Stufe 1")]
            public AnalogValue HeatPumpOperatingHours => new AnalogValue(30, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen Stufe 1
            /// </summary>
            [Secondary]
            [Description(@"Anzahl Einschaltungen Stufe 1")]
            public AnalogValue HeatPumpOperatingCycles => new AnalogValue(40, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden WW-elektrisch
            /// </summary>
            [Secondary]
            [Description(@"Betriebsstunden WW-elektrisch")]
            public AnalogValue ElectricalWarmWaterOperatingHours => new AnalogValue(37, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen WW-elektrisch
            /// </summary>
            [Secondary]
            [Description(@"Anzahl Einschaltungen WW-elektrisch")]
            public AnalogValue ElectricalWarmWaterCycles => new AnalogValue(47, null) {IsReadOnly = true};
        }

        /// <summary>
        ///     Wärmepumpe
        /// </summary>
        [Description("Wärmepumpe")]
        public class HeatPump
        {
            /// <summary>
            ///     Aktuelle WP-Leistung
            /// </summary>
            [Secondary]
            [Description(@"Aktuelle WP-Leistung")]
            public AnalogValue CurrentPower => new AnalogValue(45, "%") {IsReadOnly = true};

            /// <summary>
            ///     Sollwert der WP-Regelung
            /// </summary>
            [Secondary]
            [Description(@"Sollwert der WP-Regelung")]
            public AnalogValue HeatPumpSetPoint => new AnalogValue(210, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kühlvorlauftemp: Istwert Kühlvorlauf bei stetiger Primärpumpe
            /// </summary>
            [Primary]
            [Description(@"Kühlvorlauftemp: Istwert Kühlvorlauf bei stetiger Primärpumpe")]
            public AnalogValue CoolingFlowTemperature => new AnalogValue(3098, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Primärtemperatur: Temperatur im Primärkreis
            /// </summary>
            [Primary]
            [Description(@"Primärtemperatur: Temperatur im Primärkreis/Solekreis")]
            public AnalogValue PrimaryTemperature => new AnalogValue(602, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP-Vorlauftemp: Istwert des WP-Vorlauffühlers
            /// </summary>
            [Primary]
            [Description(@"WP-Vorlauftemp: Istwert des WP-Vorlauffühlers")]
            public AnalogValue CurrentFlowTemperature => new AnalogValue(600, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP-Vor’temp Min/Max: Min/Max-Begrenzung auf WP-Vorlauffühler
            /// </summary>
            [Secondary]
            [Description(@"WP-Vor’temp Min/Max: Min/Max-Begrenzung auf WP-Vorlauffühler")]
            public AnalogValue MaxFlowTemperature => new AnalogValue(648, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Ansauglufttemp Aktuelle Ansauglufttemperatur
            /// </summary>
            [Primary]
            [Description(@"Ansauglufttemp Aktuelle Ansauglufttemperatur")]
            public AnalogValue Ansauglufttemp => new AnalogValue(3222, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Anzeige des Betriebszustandes der Stufe 1
            /// </summary>
            [Primary]
            [Description(@"Anzeige des Betriebszustandes der Stufe 1")]
            public EnumValue<HeatPumpStatusEnums?> HeatPumpStatus
                => new EnumValue<HeatPumpStatusEnums?>(662) {IsReadOnly = true};
        }

        /// <summary>
        ///     Eingänge
        /// </summary>
        [Description(@"Eingänge")]
        public class Inputs
        {
            [Description("WP Rücklauftemperatur E7")]
            [Primary]
            public AnalogValue E7HpFlowBackTemp => new AnalogValue(2624, null) { IsReadOnly = true };


        }

        /// <summary>
        ///     Ausgänge
        /// </summary>
        [Description(@"Ausgänge")]
        public class Outputs
        {
            [Description("Primärpumpe Y1")]
            [Primary]
            public AnalogValue Y1PrimaryPump => new AnalogValue(2634, "%") {IsReadOnly = false};

            [Description("Y2")]
            [Primary]
            public AnalogValue Y2 => new AnalogValue(2635, "%") { IsReadOnly = false };

            [Description("Passivkühlen R1")]
            [Primary]
            public AnalogValue R1FreeCooling => new AnalogValue(2624, null) {IsReadOnly = true};

            //[Description("Passivkühlen inv")]
            //public AnalogValue R2 => new AnalogValue(2625, null);

            [Description("Verdichter R3")]
            [Primary]
            public AnalogValue R3Verdichter => new AnalogValue(2626, null) {IsReadOnly = true};

            [Description("Heizkreispumpe R4")]
            [Primary]
            public AnalogValue R4ZonePump => new AnalogValue(2627, null) {IsReadOnly = true};

            [Description("Warmwasser R5")]
            [Primary]
            public AnalogValue R5WarmWater => new AnalogValue(2628, null) {IsReadOnly = true};

            //[Description("Warmwasser invers")]
            //[Primary]
            //public AnalogValue R6 => new AnalogValue(2629, null);

            [Description("Warmwasser elektrisch R7")]
            [Primary]
            public AnalogValue R7ElectricalWarmWater => new AnalogValue(2630, null) {IsReadOnly = true};

            //[Description("Kein Funktion")]
            //public AnalogValue R8 => new AnalogValue(2631, null);

            //[Description("Kollektorpumpe")]
            //[Secondary]
            //public AnalogValue R9 => new AnalogValue(2632, null);

            [Description("Zusatzheizung R10")]
            [Primary]
            public AnalogValue R10AdditionalHeater => new AnalogValue(2633, null) {IsReadOnly = true};
        }

        /// <summary>
        ///     Warmwasser
        /// </summary>
        [Description(@"Warmwasser")]
        public class WarmWater
        {
            /// <summary>
            ///     Istwert Warmwasser
            /// </summary>
            [Primary]
            [Description(@"Istwert Warmwasser")]
            public AnalogValue Temperature => new AnalogValue(3, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Aktuell gültiger WW-Sollwert
            /// </summary>
            [Primary]
            [Description(@"Aktuell gültiger WW-Sollwert")]
            public AnalogValue CurrentTargetTemperature => new AnalogValue(207, "°C") {IsReadOnly = true};
        }

        /// <summary>
        ///     Zone
        /// </summary>
        [Description(@"Zone")]
        public class Zone
        {
            /// <summary>
            ///     Istwert Raum
            /// </summary>
            [Primary]
            [Description(@"Istwert Raum")]
            public AnalogValue RoomTemperature => new AnalogValue(12, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Aktueller Sollwert Raum
            /// </summary>
            [Primary]
            [Description(@"Aktueller Sollwert Raum")]
            public AnalogValue CurrentSetPoint => new AnalogValue(208, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Aktuell gemessene Vorlauftemperatur
            /// </summary>
            [Primary]
            [Description(@"Aktuell gemessene Vorlauftemperatur")]
            public AnalogValue FlowTemperature => new AnalogValue(14, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Berchnete Soll - Vorlauftemperatur
            /// </summary>
            [Primary]
            [Description(@"Berchnete Soll - Vorlauftemperatur")]
            public AnalogValue CalculatedFlowTemperature => new AnalogValue(209, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Aussentemperatur
            /// </summary>
            [Primary]
            [Description(@"Aussentemperatur")]
            public AnalogValue OutdoorTemperature => new AnalogValue(10, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Gebäudebezogene Aussentemperatur
            /// </summary>
            [Primary]
            [Description(@"ebäudebezogene Aussentemperatur")]
            public AnalogValue OutdoorTemperatureBuilding => new AnalogValue(205, "°C") {IsReadOnly = true};
        }
    }
}