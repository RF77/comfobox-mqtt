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

namespace ComfoBoxLib.Items.Enums
{
    /// <summary>
    ///     Verwendeter Raumsollwert (DP 398).
    /// </summary>
    /// <remarks>
    ///     Die Namen sind Teil des MQTT-Vertrags und duerfen nicht geaendert
    ///     werden.
    /// </remarks>
    public enum ZoneSetPointTypes
    {
        [Description("Frostschutz")] FrostProtection = 0,
        [Description("Reduziert")] Reduced = 1,
        [Description("Normal")] Normal = 2,
        [Description("Legionellen")] Legionella = 3
    }

    /// <summary>
    ///     Uebersteuerungsquelle der Zone (DP 676).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Die Luecken (12, 15, 16, 17, 19, 20) sind Vertrag: diese Werte
    ///         liefert der Regler fuer die Zone nicht. Sie duerfen nicht
    ///         aufgefuellt werden - sonst wuerde ein unbekannter Wert auf einen
    ///         falschen Namen abgebildet.
    ///     </para>
    /// </remarks>
    public enum ZoneOverrides
    {
        [Description("Ohne")] None = 0,
        [Description("Party")] Party = 1,
        [Description("Eco")] Eco = 2,
        [Description("Ferien")] Holiday = 3,
        [Description("Fernbedienung")] RemoteControl = 4,
        [Description("Externer Eingang")] ExternalInput = 5,
        [Description("Inbetriebnahme")] Commissioning = 6,
        [Description("Taupunktwaechter")] DewPointGuard = 7,
        [Description("Bodenaustrocknung")] FloorDrying = 8,
        [Description("WP-Stoerung")] HeatPumpFault = 9,
        [Description("Optimierung WP-Ausschaltung")] OptimizedHeatPumpShutdown = 10,
        [Description("Raumsoll-Ueberhoehung Solar")] SolarSetPointRise = 11,
        [Description("Sollwertschiebung dT")] SetPointShiftDeltaT = 13,
        [Description("Zeiten AUS")] TimesOff = 14,
        [Description("Aussentemperatur")] OutdoorTemperature = 18,
        [Description("Entlueftung Sekundaerkreis")] SecondaryCircuitVenting = 21,
        [Description("Nur Zusatzheizung")] AdditionalHeaterOnly = 22
    }
}
