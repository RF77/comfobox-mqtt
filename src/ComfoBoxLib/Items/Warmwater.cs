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
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{

    [Description(@"")]
    public class Warmwater
    {
        /// <summary>
        ///     Einmalige Ladung des WW-Speichers
        ///     Ungeachtet der Schaltuhr kann durch aktivieren dieser Funktion eine Ladung des WW-Speichers
        ///     erzwungen werden.
        /// </summary>

        //TODO: Wie steurt man diesen Punkt an?
        [Description(@"Einmalige Ladung des WW-Speichers. Ungeachtet der Schaltuhr kann durch aktivieren dieser Funktion eine Ladung des WW-Speichers erzwungen werden.")]
        public CommandValue DoHeatWaterNow => new CommandValue(2590);

        /// <summary>
        ///     WW Sollwert normal
        /// </summary>
        [Secondary]
        [Description(@"WW Sollwert normal")]
        public AnalogValue SetPointNormal => new AnalogValue(72, "°C", 5);

        /// <summary>
        ///     WW Sollwert reduziert
        /// </summary>
        [Secondary]
        [Description(@"WW Sollwert reduziert")]
        public AnalogValue SetPointReduced => new AnalogValue(71, "°C", 5);

        /// <summary>
        ///     WW Sollwert Frost
        /// </summary>
        [Secondary]
        [Description(@"WW Sollwert Frost")]
        public AnalogValue SetPointFrost => new AnalogValue(70, "°C", 5);

        /// <summary>
        ///     WW Sollwert Legionellen
        /// </summary>
        [Secondary]
        [Description(@"WW Sollwert Legionellen")]
        public AnalogValue SetPointLegio => new AnalogValue(73, "°C", 5);

    }
}