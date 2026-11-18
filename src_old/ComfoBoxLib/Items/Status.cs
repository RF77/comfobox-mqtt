// /*******************************************************************************
//  * Copyright (c) 2016 by RF77 (https://github.com/RF77)
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
    ///     Zustaende der Anlage - rein lesende V2-Topics.
    /// </summary>
    /// <remarks>
    ///     Alle Items dieser Klasse gehoeren zum additiven V2-Vertrag
    ///     (docs/topics-v2.md) und sind fest read-only. Die Topic-Adresse
    ///     ergibt sich aus Status/&lt;Klasse&gt;/&lt;Property&gt;.
    /// </remarks>
    [Description(@"Status")]
    public class Status
    {
        /// <summary>
        ///     Zone
        /// </summary>
        [Description(@"Zone")]
        public class Zone
        {
            /// <summary>
            ///     Vorlauftemperatur: Istwert Vorlauf
            /// </summary>
            [V2]
            [Primary]
            [Description(@"Vorlauftemperatur: Istwert Vorlauf")]
            public AnalogValue FlowTemperature => new AnalogValue(14, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Zustand Zone: Regelungs-Zustand (Werte nicht dokumentiert)
            /// </summary>
            [V2]
            [Primary]
            [Description(@"Zustand Zone: Regelungs-Zustand (Werte nicht dokumentiert)")]
            public AnalogValue State => new AnalogValue(59, null) {IsReadOnly = true};

            /// <summary>
            ///     Raumsollwerttyp: verwendeter Sollwert
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Raumsollwerttyp: verwendeter Sollwert")]
            public EnumValue<ZoneSetPointTypes?> SetPointType
                => new EnumValue<ZoneSetPointTypes?>(398) {IsReadOnly = true};

            /// <summary>
            ///     Zonenübersteuerung: Übersteuerungsquelle
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Zonenübersteuerung: Übersteuerungsquelle")]
            public EnumValue<ZoneOverrides?> Override
                => new EnumValue<ZoneOverrides?>(676) {IsReadOnly = true};
        }

        /// <summary>
        ///     Warmwasser
        /// </summary>
        [Description(@"Warmwasser")]
        public class WarmWater
        {
            /// <summary>
            ///     Warmwassertemperatur 2: WW-Fühler unten
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Warmwassertemperatur 2: WW-Fühler unten")]
            public AnalogValue TemperatureBottom => new AnalogValue(4, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WW-Sollwert (Typ)
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"WW-Sollwert (Typ)")]
            public EnumValue<WarmWaterSetPointTypes?> SetPointType
                => new EnumValue<WarmWaterSetPointTypes?>(397) {IsReadOnly = true};

            /// <summary>
            ///     WW-Betrieb: Zustand der Warmwasserbereitung
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"WW-Betrieb: Zustand der Warmwasserbereitung")]
            public EnumValue<WarmWaterStates?> State
                => new EnumValue<WarmWaterStates?>(2599) {IsReadOnly = true};

            /// <summary>
            ///     WW-Übersteuerung: Übersteuerungsquelle
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"WW-Übersteuerung: Übersteuerungsquelle")]
            public EnumValue<WarmWaterOverrides?> Override
                => new EnumValue<WarmWaterOverrides?>(2598) {IsReadOnly = true};
        }

        /// <summary>
        ///     Wärmepumpe
        /// </summary>
        [Description(@"Wärmepumpe")]
        public class HeatPump
        {
            /// <summary>
            ///     Energieerzeugertemperatur: Istwert der WP-Regelung
            /// </summary>
            [V2]
            [Primary]
            [Description(@"Energieerzeugertemperatur: Istwert der WP-Regelung")]
            public AnalogValue EnergyProducerTemperature => new AnalogValue(21, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP-Rücklauftemperatur: Istwert Rücklauf
            /// </summary>
            [V2]
            [Primary]
            [Description(@"WP-Rücklauftemperatur: Istwert Rücklauf")]
            public AnalogValue ReturnTemperature => new AnalogValue(3468, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Primärvorlauftemperatur: Vorlauf im Primärkreis
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Primärvorlauftemperatur: Vorlauf im Primärkreis")]
            public AnalogValue PrimaryFlowTemperature => new AnalogValue(3209, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Verdampfertemperatur: Verdampfer
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Verdampfertemperatur: Verdampfer")]
            public AnalogValue EvaporatorTemperature => new AnalogValue(660, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Sauggastemperatur: Sauggas
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Sauggastemperatur: Sauggas")]
            public AnalogValue SuctionGasTemperature => new AnalogValue(641, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Heissgastemperatur: Heissgas
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Heissgastemperatur: Heissgas")]
            public AnalogValue HotGasTemperature => new AnalogValue(3228, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kondensatortemperatur: Kondensator
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Kondensatortemperatur: Kondensator")]
            public AnalogValue CondenserTemperature => new AnalogValue(666, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kondensationstemperatur: aus Hochdrucksensor gerechnet
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kondensationstemperatur: aus Hochdrucksensor gerechnet")]
            public AnalogValue CondensationTemperature => new AnalogValue(3475, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Verdampfungstemperatur: aus Niederdrucksensor gerechnet
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Verdampfungstemperatur: aus Niederdrucksensor gerechnet")]
            public AnalogValue EvaporationTemperature => new AnalogValue(3598, "°C") {IsReadOnly = true};

            /// <summary>
            ///     WP2: Betriebszustand Stufe 2
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"WP2: Betriebszustand Stufe 2")]
            public EnumValue<HeatPumpStatusEnums?> HeatPumpStatusStage2
                => new EnumValue<HeatPumpStatusEnums?>(663) {IsReadOnly = true};

            /// <summary>
            ///     Aktuelle Abtaudifferenz: aktuell gerechnete Abtaudifferenz
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Aktuelle Abtaudifferenz: aktuell gerechnete Abtaudifferenz")]
            public AnalogValue DefrostDeltaActual => new AnalogValue(644, "K") {IsReadOnly = true};

            /// <summary>
            ///     Referenzdifferenz gemessen: dT bei Referenzaufnahme
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Referenzdifferenz gemessen: dT bei Referenzaufnahme")]
            public AnalogValue DefrostDeltaReference => new AnalogValue(3110, "K") {IsReadOnly = true};

            /// <summary>
            ///     Referenzdifferenz korrigiert: gültige dT für Überwachung
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Referenzdifferenz korrigiert: gültige dT für Überwachung")]
            public AnalogValue DefrostDeltaCorrected => new AnalogValue(643, "K") {IsReadOnly = true};
        }

        /// <summary>
        ///     Pufferspeicher
        /// </summary>
        [Description(@"Pufferspeicher")]
        public class Buffer
        {
            /// <summary>
            ///     Pufferspeichertemperatur oben
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Pufferspeichertemperatur oben")]
            public AnalogValue TemperatureTop => new AnalogValue(24, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Pufferspeichertemperatur unten
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Pufferspeichertemperatur unten")]
            public AnalogValue TemperatureBottom => new AnalogValue(25, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Pufferspeichersollwert
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Pufferspeichersollwert")]
            public AnalogValue SetPoint => new AnalogValue(281, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kühlpufferspeichertemperatur oben
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kühlpufferspeichertemperatur oben")]
            public AnalogValue CoolingTemperatureTop => new AnalogValue(3925, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kühlpufferspeichertemperatur unten
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kühlpufferspeichertemperatur unten")]
            public AnalogValue CoolingTemperatureBottom => new AnalogValue(3924, "°C") {IsReadOnly = true};
        }

        /// <summary>
        ///     Zaehler und Wartung
        /// </summary>
        [Description(@"Zähler und Wartung")]
        public class Counters
        {
            /// <summary>
            ///     Betriebsstunden Stufe 2
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden Stufe 2")]
            public AnalogValue Stage2OperatingHours => new AnalogValue(31, "h") {IsReadOnly = true};

            /// <summary>
            ///     Einschaltungen Stufe 2
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Einschaltungen Stufe 2")]
            public AnalogValue Stage2Cycles => new AnalogValue(41, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden Zusatzheizung
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden Zusatzheizung")]
            public AnalogValue AdditionalHeaterOperatingHours
                => new AnalogValue(33, "h") {IsReadOnly = true};

            /// <summary>
            ///     Einschaltungen Zusatzheizung
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Einschaltungen Zusatzheizung")]
            public AnalogValue AdditionalHeaterCycles => new AnalogValue(43, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden Heizen
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden Heizen")]
            public AnalogValue HeatingOperatingHours => new AnalogValue(3060, "h") {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden Warmwasser
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden Warmwasser")]
            public AnalogValue WarmWaterOperatingHours => new AnalogValue(3061, "h") {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden aktives Kuehlen
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden aktives Kühlen")]
            public AnalogValue ActiveCoolingOperatingHours => new AnalogValue(3362, "h") {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden passives Kuehlen
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden passives Kühlen")]
            public AnalogValue PassiveCoolingOperatingHours => new AnalogValue(3363, "h") {IsReadOnly = true};

            /// <summary>
            ///     Abtauungen: Anzahl der Abtauvorgaenge
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Abtauungen: Anzahl der Abtauvorgänge")]
            public AnalogValue DefrostCycles => new AnalogValue(3485, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden Abtauen
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden Abtauen")]
            public AnalogValue DefrostOperatingHours => new AnalogValue(3484, "h") {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden seit dem letzten Service
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden seit dem letzten Service")]
            public AnalogValue ServiceHours => new AnalogValue(3366, "h") {IsReadOnly = true};
        }

        /// <summary>
        ///     Sonnenkollektor
        /// </summary>
        [Description(@"Sonnenkollektor")]
        public class Solar
        {
            /// <summary>
            ///     Kollektortemperatur: Fuehler am Kollektor
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Kollektortemperatur: Fühler am Kollektor")]
            public AnalogValue CollectorTemperature => new AnalogValue(28, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kollektorwaermetauschertemperatur
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kollektorwärmetauschertemperatur")]
            public AnalogValue CollectorHeatExchangerTemperature
                => new AnalogValue(3903, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Kollektor-Differenztemperatur (dT)
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Kollektor-Differenztemperatur (dT)")]
            public AnalogValue CollectorDeltaTemperature => new AnalogValue(264, "K") {IsReadOnly = true};

            /// <summary>
            ///     Solarenergie gesamt: Ertragszahler gesamt
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Solarenergie gesamt: Ertragszähler gesamt")]
            public AnalogValue EnergyTotal => new AnalogValue(36, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Solarenergie heute: Tagesertrag
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Solarenergie heute: Tagesertrag")]
            public AnalogValue EnergyToday => new AnalogValue(3911, "kWh") {IsReadOnly = true};
        }
    }
}
