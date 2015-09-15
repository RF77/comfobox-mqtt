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
    public class Warmwater
    {
        /// <summary>
        ///     Einmalige Ladung des WW-Speichers
        ///     Ungeachtet der Schaltuhr kann durch aktivieren dieser Funktion eine Ladung des WW-Speichers
        ///     erzwungen werden.
        /// </summary>

        //TODO: Wie steurt man diesen Punkt an?
        public AnalogValue DoHeatWaterNow => new AnalogValue(2590, null) {IsReadOnly = true};

        /// <summary>
        ///     WW Sollwert normal
        /// </summary>
        [Secondary]
        public AnalogValue SetPointNormal => new AnalogValue(72, "°C", 5);

        /// <summary>
        ///     WW Sollwert reduziert
        /// </summary>
        [Secondary]
        public AnalogValue SetPointReduced => new AnalogValue(71, "°C", 5);

        /// <summary>
        ///     WW Sollwert Frost
        /// </summary>
        [Secondary]
        public AnalogValue SetPointFrost => new AnalogValue(70, "°C", 5);

        /// <summary>
        ///     WW Sollwert Legionellen
        /// </summary>
        [Secondary]
        public AnalogValue SetPointLegio => new AnalogValue(73, "°C", 5);

        /// <summary>
        ///     Read only values
        /// </summary>
        public class States
        {
            /// <summary>
            ///     Istwert Warmwasser
            /// </summary>
            [Primary]
            public AnalogValue Temperature => new AnalogValue(3, "°C") {IsReadOnly = true};

            /// <summary>
            ///     Aktuell gültiger WW-Sollwert
            /// </summary>
            [Primary]
            public AnalogValue CurrentTargetTemperature => new AnalogValue(207, "°C") {IsReadOnly = true};

            // Other values?: 2598, 397, 2599
        }
    }
}