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
using ComfoBoxLib.Items.Enums;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    public class Zone
    {
        /// <summary>
        ///     Betriebsart
        /// </summary>
        [Primary]
        public EnumValue<OperationModes> OperationMode => new EnumValue<OperationModes>(830);

        /// <summary>
        ///     Betriebsart Heizen und Kühlen
        /// </summary>
        [Primary]
        public EnumValue<HeatingCoolingModes> HeatingCoolingMode => new EnumValue<HeatingCoolingModes>(3078);

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird. Während der
        ///     eingestellten Zeit ist dann unabhängig der Schaltuhr der Raumsollwert „Normal“ gültig.
        /// </summary>
        [Secondary]
        public AnalogValue PartyTime => new AnalogValue(674, "h");

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird. Während der
        ///     eingestellten Zeit ist dann unabhängig der Schaltuhr der Raumsollwert „Reduziert“ bzw
        ///     „Frostschutz“ gültig(abhängig von der Betriebsart).
        /// </summary>
        [Secondary]
        public AnalogValue EcoTime => new AnalogValue(675, "h");

        /// <summary>
        ///     Um eine genauere Regelung zu ermöglichen, kann hier die reale, mit einem genauen Messgerät
        ///     ermittelte Raumtemperatur eingestellt werden.
        /// </summary>
        [Secondary]
        public AnalogValue CalibrateTemprature => new AnalogValue(75, "°C");

        /// <summary>
        ///     Die Adaption der Heizkennlinie korrigiert die Heizkurve bei der aktuellen Aussentemperatur. Die
        ///     Korrektur sollte jeweils bei tiefer und bei hoher Aussentemperatur durchgeführt werden.Diese
        ///     Funktion kann nur einmal täglich aufgerufen werden.
        /// </summary>
        [Secondary]
        public AnalogValue ManualAdoption => new AnalogValue(679, "K");

        /// <summary>
        ///     Raumsollwert normal 5.0..30.0
        /// </summary>
        [Primary]
        public AnalogValue SetPointNormal => new AnalogValue(62, "°C", 5, 30);

        /// <summary>
        ///     Raumsollwert reduziert 5.0..30.0
        /// </summary>
        [Secondary]
        public AnalogValue SetPointReduced => new AnalogValue(61, "°C", 5, 30);

        /// <summary>
        ///     Raumsollwert Frost 5.0..30.0
        /// </summary>
        [Secondary]
        public AnalogValue SetPointFrost => new AnalogValue(60, "°C", 5, 30);
    }
}