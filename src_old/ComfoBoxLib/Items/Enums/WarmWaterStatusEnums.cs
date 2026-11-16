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
    ///     Verwendeter Warmwasser-Sollwert (DP 397).
    /// </summary>
    /// <remarks>
    ///     Die Namen sind Teil des MQTT-Vertrags und duerfen nicht geaendert
    ///     werden.
    /// </remarks>
    public enum WarmWaterSetPointTypes
    {
        [Description("Frostschutz")] FrostProtection = 0,
        [Description("Reduziert")] Reduced = 1,
        [Description("Normal")] Normal = 2,
        [Description("Legionellen")] Legionella = 3
    }

    /// <summary>
    ///     Betriebszustand der Warmwasserbereitung (DP 2599).
    /// </summary>
    public enum WarmWaterStates
    {
        [Description("AUS")] Off = 0,
        [Description("WW mit Energieerzeuger")] WithEnergyProducer = 1,
        [Description("WW-elektrisch")] Electrical = 2,
        [Description("WW mit Zusatzheizung")] WithAdditionalHeater = 3,
        [Description("WW-Entladeschutz")] DischargeProtection = 4,
        [Description("EW-Sperre")] EnergyProducerLock = 5
    }

    /// <summary>
    ///     Uebersteuerungsquelle der Warmwasserbereitung (DP 2598).
    /// </summary>
    /// <remarks>
    ///     Die Luecken (2, 4, 6, 7, 8, 11, 12) sind Vertrag: diese Werte
    ///     liefert der Regler fuer die Warmwasserbereitung nicht.
    /// </remarks>
    public enum WarmWaterOverrides
    {
        [Description("Ohne")] None = 0,
        [Description("Party")] Party = 1,
        [Description("Ferien")] Holiday = 3,
        [Description("Externer Eingang")] ExternalInput = 5,
        [Description("WP-Stoerung")] HeatPumpFault = 9,
        [Description("Optimierung")] Optimization = 10,
        [Description("Sollwertschiebung")] SetPointShiftDeltaT = 13,
        [Description("Zeiten AUS")] TimesOff = 14,
        [Description("WP-Vorlauf zu hoch")] FlowTemperatureTooHigh = 15,
        [Description("WW-elektrisch")] Electrical = 17,
        [Description("Optimierung WW-elektrisch")] OptimizationElectrical = 19,
        [Description("EW-Sperre")] EnergyProducerLock = 20,
        [Description("Nur Zusatzheizung")] AdditionalHeaterOnly = 22
    }
}
