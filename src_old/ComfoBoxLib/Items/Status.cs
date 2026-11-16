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
            public AnalogValue FlowTemperature => new AnalogValue(14, "�C") {IsReadOnly = true};

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
            ///     Zonen�bersteuerung: �bersteuerungsquelle
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Zonen�bersteuerung: �bersteuerungsquelle")]
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
            ///     Warmwassertemperatur 2: WW-F�hler unten
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Warmwassertemperatur 2: WW-F�hler unten")]
            public AnalogValue TemperatureBottom => new AnalogValue(4, "�C") {IsReadOnly = true};

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
            ///     WW-�bersteuerung: �bersteuerungsquelle
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"WW-�bersteuerung: �bersteuerungsquelle")]
            public EnumValue<WarmWaterOverrides?> Override
                => new EnumValue<WarmWaterOverrides?>(2598) {IsReadOnly = true};
        }

        /// <summary>
        ///     W�rmepumpe
        /// </summary>
        [Description(@"W�rmepumpe")]
        public class HeatPump
        {
            /// <summary>
            ///     Energieerzeugertemperatur: Istwert der WP-Regelung
            /// </summary>
            [V2]
            [Primary]
            [Description(@"Energieerzeugertemperatur: Istwert der WP-Regelung")]
            public AnalogValue EnergyProducerTemperature => new AnalogValue(21, "�C") {IsReadOnly = true};

            /// <summary>
            ///     WP-R�cklauftemperatur: Istwert R�cklauf
            /// </summary>
            [V2]
            [Primary]
            [Description(@"WP-R�cklauftemperatur: Istwert R�cklauf")]
            public AnalogValue ReturnTemperature => new AnalogValue(3468, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Prim�rvorlauftemperatur: Vorlauf im Prim�rkreis
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Prim�rvorlauftemperatur: Vorlauf im Prim�rkreis")]
            public AnalogValue PrimaryFlowTemperature => new AnalogValue(3209, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Verdampfertemperatur: Verdampfer
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Verdampfertemperatur: Verdampfer")]
            public AnalogValue EvaporatorTemperature => new AnalogValue(660, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Sauggastemperatur: Sauggas
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Sauggastemperatur: Sauggas")]
            public AnalogValue SuctionGasTemperature => new AnalogValue(641, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Heissgastemperatur: Heissgas
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Heissgastemperatur: Heissgas")]
            public AnalogValue HotGasTemperature => new AnalogValue(3228, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Kondensatortemperatur: Kondensator
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Kondensatortemperatur: Kondensator")]
            public AnalogValue CondenserTemperature => new AnalogValue(666, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Kondensationstemperatur: aus Hochdrucksensor gerechnet
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kondensationstemperatur: aus Hochdrucksensor gerechnet")]
            public AnalogValue CondensationTemperature => new AnalogValue(3475, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Verdampfungstemperatur: aus Niederdrucksensor gerechnet
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Verdampfungstemperatur: aus Niederdrucksensor gerechnet")]
            public AnalogValue EvaporationTemperature => new AnalogValue(3598, "�C") {IsReadOnly = true};

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
            ///     Referenzdifferenz korrigiert: g�ltige dT f�r �berwachung
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Referenzdifferenz korrigiert: g�ltige dT f�r �berwachung")]
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
            public AnalogValue TemperatureTop => new AnalogValue(24, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Pufferspeichertemperatur unten
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Pufferspeichertemperatur unten")]
            public AnalogValue TemperatureBottom => new AnalogValue(25, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Pufferspeichersollwert
            /// </summary>
            [V2]
            [Secondary]
            [Description(@"Pufferspeichersollwert")]
            public AnalogValue SetPoint => new AnalogValue(281, "�C") {IsReadOnly = true};

            /// <summary>
            ///     K�hlpufferspeichertemperatur oben
            /// </summary>
            [V2]
            [Slow]
            [Description(@"K�hlpufferspeichertemperatur oben")]
            public AnalogValue CoolingTemperatureTop => new AnalogValue(3925, "�C") {IsReadOnly = true};

            /// <summary>
            ///     K�hlpufferspeichertemperatur unten
            /// </summary>
            [V2]
            [Slow]
            [Description(@"K�hlpufferspeichertemperatur unten")]
            public AnalogValue CoolingTemperatureBottom => new AnalogValue(3924, "�C") {IsReadOnly = true};
        }

        /// <summary>
        ///     Energie und COP
        /// </summary>
        /// <remarks>
        ///     Die Items mit [Energy] liefern nur mit aktiver
        ///     Waermemengenmessung (DP 3340) einen sinnvollen Wert.
        /// </remarks>
        [Description(@"Energie und COP")]
        public class Energy
        {
            /// <summary>
            ///     Heizleistung: aktuelle thermische Leistung Heizen
            /// </summary>
            [V2]
            [Energy]
            [Secondary]
            [Description(@"Heizleistung: aktuelle thermische Leistung Heizen")]
            public AnalogValue HeatingPower => new AnalogValue(3342, "kW") {IsReadOnly = true};

            /// <summary>
            ///     Warmwasserleistung: aktuelle thermische Leistung Warmwasser
            /// </summary>
            [V2]
            [Energy]
            [Secondary]
            [Description(@"Warmwasserleistung: aktuelle thermische Leistung Warmwasser")]
            public AnalogValue WarmWaterPower => new AnalogValue(3346, "kW") {IsReadOnly = true};

            /// <summary>
            ///     Waermemenge Heizen: thermischer Zaehler Heizen
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"W�rmemenge Heizen: thermischer Z�hler Heizen")]
            public AnalogValue ThermalEnergyHeating => new AnalogValue(3343, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Waermemenge Warmwasser: thermischer Zaehler Warmwasser
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"W�rmemenge Warmwasser: thermischer Z�hler Warmwasser")]
            public AnalogValue ThermalEnergyWarmWater => new AnalogValue(3347, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Waermemenge gesamt: thermischer Zaehler gesamt
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"W�rmemenge gesamt: thermischer Z�hler gesamt")]
            public AnalogValue ThermalEnergyTotal => new AnalogValue(3349, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Strommenge Heizen: elektrischer Zaehler Heizen
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"Strommenge Heizen: elektrischer Z�hler Heizen")]
            public AnalogValue ElectricalEnergyHeating => new AnalogValue(3344, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Strommenge Warmwasser: elektrischer Zaehler Warmwasser
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"Strommenge Warmwasser: elektrischer Z�hler Warmwasser")]
            public AnalogValue ElectricalEnergyWarmWater => new AnalogValue(3354, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     Strommenge gesamt: elektrischer Zaehler gesamt
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"Strommenge gesamt: elektrischer Z�hler gesamt")]
            public AnalogValue ElectricalEnergyTotal => new AnalogValue(3355, "kWh") {IsReadOnly = true};

            /// <summary>
            ///     COP Heizen: Guetezahl Heizen (Laufzeitmittel, kein Momentanwert)
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"COP Heizen: G�tezahl Heizen (Laufzeitmittel)")]
            public AnalogValue CopHeating => new AnalogValue(3345, null) {IsReadOnly = true};

            /// <summary>
            ///     COP Warmwasser: Guetezahl Warmwasser (Laufzeitmittel)
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"COP Warmwasser: G�tezahl Warmwasser (Laufzeitmittel)")]
            public AnalogValue CopWarmWater => new AnalogValue(3356, null) {IsReadOnly = true};

            /// <summary>
            ///     COP gesamt: Guetezahl gesamt (Laufzeitmittel)
            /// </summary>
            [V2]
            [Energy]
            [Slow]
            [Description(@"COP gesamt: G�tezahl gesamt (Laufzeitmittel)")]
            public AnalogValue CopTotal => new AnalogValue(3357, null) {IsReadOnly = true};

            /// <summary>
            ///     Waermemengenmessung: Betriebsart der Waermemengenmessung
            /// </summary>
            [V2]
            [Slow]
            [Description(@"W�rmemengenmessung: Betriebsart der W�rmemengenmessung")]
            public EnumValue<EnergyMeasurementModes?> MeasurementMode
                => new EnumValue<EnergyMeasurementModes?>(3340) {IsReadOnly = true};

            /// <summary>
            ///     Durchfluss Heizen: Volumenstrom des Waermemengenzaehlers
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Durchfluss Heizen: Volumenstrom des W�rmemengenz�hlers")]
            public AnalogValue FlowRate => new AnalogValue(3341, "m�/h") {IsReadOnly = true};

            /// <summary>
            ///     Durchfluss Warmwasser: Volumenstrom des WW-Waermemengenzaehlers
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Durchfluss Warmwasser: Volumenstrom des WW-W�rmemengenz�hlers")]
            public AnalogValue FlowRateWarmWater => new AnalogValue(3348, "m�/h") {IsReadOnly = true};
        }

        /// <summary>
        ///     Zaehler und Wartung
        /// </summary>
        [Description(@"Z�hler und Wartung")]
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
            [Description(@"Betriebsstunden aktives K�hlen")]
            public AnalogValue ActiveCoolingOperatingHours => new AnalogValue(3362, "h") {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden passives Kuehlen
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Betriebsstunden passives K�hlen")]
            public AnalogValue PassiveCoolingOperatingHours => new AnalogValue(3363, "h") {IsReadOnly = true};

            /// <summary>
            ///     Abtauungen: Anzahl der Abtauvorgaenge
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Abtauungen: Anzahl der Abtauvorg�nge")]
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
            [Description(@"Kollektortemperatur: F�hler am Kollektor")]
            public AnalogValue CollectorTemperature => new AnalogValue(28, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Kollektorwaermetauschertemperatur
            /// </summary>
            [V2]
            [Slow]
            [Description(@"Kollektorw�rmetauschertemperatur")]
            public AnalogValue CollectorHeatExchangerTemperature
                => new AnalogValue(3903, "�C") {IsReadOnly = true};

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
            [Description(@"Solarenergie gesamt: Ertragsz�hler gesamt")]
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
