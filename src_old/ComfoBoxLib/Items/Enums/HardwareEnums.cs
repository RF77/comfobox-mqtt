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
    ///     Typ des Pufferspeichers (DP 805) und des Kuehlpufferspeichers
    ///     (DP 3918).
    /// </summary>
    /// <remarks>
    ///     Der Wert sagt, wie viele Fuehler der Speicher hat. Ohne Fuehler
    ///     liefert der Regler fuer die zugehoerigen Temperaturen den Sentinel
    ///     32767. Die Namen sind Teil des MQTT-Vertrags und duerfen nicht
    ///     geaendert werden.
    /// </remarks>
    public enum BufferTypes
    {
        [Description("Keine Funktion")] None = 0,
        [Description("Ein Fühler")] OneSensor = 1,
        [Description("Zwei Fühler")] TwoSensors = 11
    }

    /// <summary>
    ///     Hydraulik des Sonnenkollektors (DP 807).
    /// </summary>
    /// <remarks>
    ///     <c>None</c> heisst: es ist keine Solaranlage eingerichtet. Die
    ///     uebrigen Werte beschreiben, wohin die Kollektorpumpe foerdert.
    ///     Die Namen sind Teil des MQTT-Vertrags und duerfen nicht geaendert
    ///     werden.
    /// </remarks>
    public enum SolarHydraulicsTypes
    {
        [Description("Keine Funktion")] None = 0,
        [Description("Pumpe auf Pufferspeicher")] PumpToBuffer = 1,
        [Description("Pumpe auf WW-Speicher")] PumpToWarmWater = 2,
        [Description("Pumpe auf Pufferspeicher mit WT")] PumpToBufferWithHeatExchanger = 4,
        [Description("Pumpe auf WW, dann auf Pufferspeicher")] PumpWarmWaterThenBuffer = 21,
        [Description("Pumpe auf WW-Speicher mit WT")] PumpToWarmWaterWithHeatExchanger = 22,
        [Description("Pumpe auf WW, auf Pufferspeicher mit WT")] PumpWarmWaterThenBufferWithHeatExchanger = 24
    }

    /// <summary>
    ///     Ist eine Baugruppe vorhanden (DP 2800, Fuehler zu WP-Regelung)?
    /// </summary>
    public enum PresenceTypes
    {
        [Description("Nicht vorhanden")] NotInstalled = 0,
        [Description("Vorhanden")] Installed = 1
    }
}
