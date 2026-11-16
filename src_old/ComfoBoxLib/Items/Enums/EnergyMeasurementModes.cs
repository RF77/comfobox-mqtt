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
    ///     Betriebsart der Waermemengenmessung (DP 3340).
    /// </summary>
    /// <remarks>
    ///     Ohne Waermemengenmessung (None) rechnet der Regler keine Waerme -
    ///     die kWh-Zaehler und der COP bleiben ohne Aussage. Die Namen sind
    ///     Teil des MQTT-Vertrags und duerfen nicht geaendert werden.
    /// </remarks>
    public enum EnergyMeasurementModes
    {
        [Description("Aus")] None = 0,
        [Description("Ein")] On = 1,
        [Description("Ein, WW getrennt")] OnSeparateWarmWater = 2
    }
}
