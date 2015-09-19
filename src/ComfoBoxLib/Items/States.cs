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
            public AnalogValue ControllerType => new AnalogValue(203, null) {IsReadOnly = true};

            /// <summary>
            ///     Angeschlossener Erweiterungstyp
            /// </summary>
            [Once]
            public AnalogValue ExtensionType => new AnalogValue(2044, null) {IsReadOnly = true};

            /// <summary>
            ///     Sofwareversion
            /// </summary>
            [Once]
            public AnalogValue SoftwareVersion => new AnalogValue(2043, null) {IsReadOnly = true};

            /// <summary>
            ///     Das Inbetriebnahme-Datum der Reglers wird
            ///     beim Laden der Applikation gesetzt
            /// </summary>
            [Once]
            public DateValue InitialOperationDate => new DateValue(3353, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden des Reglers
            /// </summary>
            [Secondary]
            public AnalogValue OperatingHours => new AnalogValue(211, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen Regler-Speisung
            /// </summary>
            [Secondary]
            public AnalogValue NumberOfStartUps => new AnalogValue(212, null) {IsReadOnly = true};

            /// <summary>
            ///     Anzahl der geänderten Parameter seit
            ///     „Applikation laden…“
            /// </summary>
            [Secondary]
            public AnalogValue NumberOfParameterChanges => new AnalogValue(617, null) {IsReadOnly = true};

            /// <summary>
            ///     Datum der letzten Parameteränderung
            /// </summary>
            [Secondary]
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
            public AnalogValue HeatPumpOperatingHours => new AnalogValue(30, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen Stufe 1
            /// </summary>
            [Secondary]
            public AnalogValue HeatPumpOperatingCycles => new AnalogValue(40, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden WW-elektrisch
            /// </summary>
            [Secondary]
            public AnalogValue ElectricalWarmWaterOperatingHours => new AnalogValue(37, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen WW-elektrisch
            /// </summary>
            [Secondary]
            public AnalogValue ElectricalWarmWaterCycles => new AnalogValue(47, null) {IsReadOnly = true};
        }

        /// <summary>
        /// Wärmepumpe
        /// </summary>
        [Description("Wärmepumpe")]
        public class HeatPump
        {
            
                /// <summary>
                ///     Aktuelle WP-Leistung
                /// </summary>
                [Secondary]
                public AnalogValue CurrentPower => new AnalogValue(45, "%") { IsReadOnly = true };

                /// <summary>
                ///     Sollwert der WP-Regelung
                /// </summary>
                [Secondary]
                public SpecialAnalogValue HeatPumpSetPoint => new SpecialAnalogValue(210, "°C") { IsReadOnly = true };

                /// <summary>
                ///     Kühlvorlauftemp: Istwert Kühlvorlauf bei stetiger Primärpumpe
                /// </summary>
                [Primary]
                public SpecialAnalogValue CoolingFlowTemperature => new SpecialAnalogValue(3098, "°C") { IsReadOnly = true };

                /// <summary>
                ///     Primärtemperatur: Temperatur im Primärkreis
                /// </summary>
                [Primary]
                public SpecialAnalogValue PrimaryTemperature => new SpecialAnalogValue(602, "°C") { IsReadOnly = true };

                /// <summary>
                ///     WP-Vorlauftemp: Istwert des WP-Vorlauffühlers
                /// </summary>
                [Primary]
                public SpecialAnalogValue CurrentFlowTemperature => new SpecialAnalogValue(600, "°C") { IsReadOnly = true };

                /// <summary>
                ///     WP-Vor’temp Min/Max: Min/Max-Begrenzung auf WP-Vorlauffühler
                /// </summary>
                [Secondary]
                public SpecialAnalogValue MaxFlowTemperature => new SpecialAnalogValue(648, "°C") { IsReadOnly = true };

                /// <summary>
                ///     Ansauglufttemp Aktuelle Ansauglufttemperatur
                /// </summary>
                [Primary]
                public SpecialAnalogValue Ansauglufttemp => new SpecialAnalogValue(3222, "°C") { IsReadOnly = true };

                /// <summary>
                ///     Anzeige des Betriebszustandes der Stufe 1
                /// </summary>
                [Primary]
                public EnumValue<HeatPumpStatusEnums> HeatPumpStatus
                    => new EnumValue<HeatPumpStatusEnums>(662) { IsReadOnly = true };

        }

        /// <summary>
        /// Ausgänge
        /// </summary>
        public class Outputs
        {
            [Description("Primärpumpe Y1")]
            [Primary]
            public AnalogValue PrimaryPump => new AnalogValue(2634, "%") { IsReadOnly = true };

            [Description("Passivkühlen R1")]
            [Primary]
            public AnalogValue FreeCooling => new AnalogValue(2624, null) { IsReadOnly = true };

            //[Description("Passivkühlen inv")]
            //public AnalogValue R2 => new AnalogValue(2625, null);

            [Description("Verdichter R3")]
            [Primary]
            public AnalogValue Verdichter => new AnalogValue(2626, null) { IsReadOnly = true };

            [Description("Heizkreispumpe R4")]
            [Primary]
            public AnalogValue ZonePump => new AnalogValue(2627, null) { IsReadOnly = true };

            [Description("Warmwasser R5")]
            [Primary]
            public AnalogValue WarmWater => new AnalogValue(2628, null) { IsReadOnly = true };

            //[Description("Warmwasser invers")]
            //[Primary]
            //public AnalogValue R6 => new AnalogValue(2629, null);

            [Description("Warmwasser elektrisch R7")]
            [Primary]
            public AnalogValue ElectricalWarmWater => new AnalogValue(2630, null) { IsReadOnly = true };

            //[Description("Kein Funktion")]
            //public AnalogValue R8 => new AnalogValue(2631, null);

            //[Description("Kollektorpumpe")]
            //[Secondary]
            //public AnalogValue R9 => new AnalogValue(2632, null);

            [Description("Zusatzheizung R10")]
            [Primary]
            public AnalogValue AdditionalHeater => new AnalogValue(2633, null) { IsReadOnly = true };
        }

        /// <summary>
        ///     Warmwasser
        /// </summary>
        public class WarmWater
        {
            /// <summary>
            ///     Istwert Warmwasser
            /// </summary>
            [Primary]
            public AnalogValue Temperature => new AnalogValue(3, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Aktuell gültiger WW-Sollwert
            /// </summary>
            [Primary]
            public AnalogValue CurrentTargetTemperature => new AnalogValue(207, "°C") { IsReadOnly = true };

            // Other values?: 2598, 397, 2599
        }

        /// <summary>
        ///     Zone
        /// </summary>
        public class Zone
        {
            /// <summary>
            ///     Istwert Raum
            /// </summary>
            [Primary]
            public AnalogValue RoomTemperature => new AnalogValue(12, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Aktueller Sollwert Raum
            /// </summary>
            [Primary]
            public AnalogValue CurrentSetPoint => new AnalogValue(208, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Aktuell gemessene Vorlauftemperatur
            /// </summary>
            [Primary]
            public AnalogValue FlowTemperature => new AnalogValue(14, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Berchnete Soll - Vorlauftemperatur
            /// </summary>
            [Primary]
            public AnalogValue CalculatedFlowTemperature => new AnalogValue(209, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Aussentemperatur
            /// </summary>
            [Primary]
            public AnalogValue OutdoorTemperature => new AnalogValue(10, "°C") { IsReadOnly = true };

            /// <summary>
            ///     Gebäudebezogene Aussentemperatur
            /// </summary>
            [Primary]
            public AnalogValue OutdoorTemperatureBuilding => new AnalogValue(205, "°C") { IsReadOnly = true };

            // Other values?: 59, 398, 676
        }

    }
}