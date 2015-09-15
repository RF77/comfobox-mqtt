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

using ComfoBoxLib.Attributes;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    public class Counters
    {
        /// <summary>
        ///     Read only values
        /// </summary>
        public class States
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


            // Other values?: 
        }
    }
}