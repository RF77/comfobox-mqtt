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
using ComfoBoxLib.Items.Enums;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    [Description(@"Zone")]
    public class Zone
    {
        /// <summary>
        ///     Betriebsart
        /// </summary>
        [Primary]
        [Description(@"Betriebsart")]
        public EnumValue<OperationModes?> OperationMode => new EnumValue<OperationModes?>(830);

        /// <summary>
        ///     Betriebsart Heizen und K�hlen
        /// </summary>
        [Primary]
        [Description(@"Betriebsart Heizen und K�hlen")]
        public EnumValue<HeatingCoolingModes?> HeatingCoolingMode => new EnumValue<HeatingCoolingModes?>(3078);

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der
        ///     eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Normal� g�ltig.
        /// </summary>
        [Secondary]
        [Description(@"Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Normal� g�ltig.")]
        public AnalogValue PartyTime => new AnalogValue(674, "h");

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der
        ///     eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Reduziert� bzw
        ///     �Frostschutz� g�ltig(abh�ngig von der Betriebsart).
        /// </summary>
        [Secondary]
        [Description(@"Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Reduziert� bzw. �Frostschutz� g�ltig(abh�ngig von der Betriebsart).")]
        public AnalogValue EcoTime => new AnalogValue(675, "h");

        /// <summary>
        ///     Um eine genauere Regelung zu erm�glichen, kann hier die reale, mit einem genauen Messger�t
        ///     ermittelte Raumtemperatur eingestellt werden.
        /// </summary>
        [Secondary]
        [Description(@"Um eine genauere Regelung zu erm�glichen, kann hier die reale, mit einem genauen Messger�t ermittelte Raumtemperatur eingestellt werden.")]
        public AnalogValue CalibrateTemperature => new AnalogValue(75, "�C");

        /// <summary>
        ///     Die Adaption der Heizkennlinie korrigiert die Heizkurve bei der aktuellen Aussentemperatur. Die
        ///     Korrektur sollte jeweils bei tiefer und bei hoher Aussentemperatur durchgef�hrt werden.Diese
        ///     Funktion kann nur einmal t�glich aufgerufen werden.
        /// </summary>
        [Secondary]
        [Description(@"Die Adaption der Heizkennlinie korrigiert die Heizkurve bei der aktuellen Aussentemperatur. Die Korrektur sollte jeweils bei tiefer und bei hoher Aussentemperatur durchgef�hrt werden. Diese Funktion kann nur einmal t�glich aufgerufen werden.")]
        public AnalogValue ManualAdoption => new AnalogValue(679, "K");

        /// <summary>
        ///     Raumsollwert normal 5.0..30.0
        /// </summary>
        [Primary]
        [Description(@"Raumsollwert normal 5.0..30.0")]
        public AnalogValue SetPointNormal => new AnalogValue(62, "�C", 5, 30);

        /// <summary>
        ///     Raumsollwert reduziert 5.0..30.0
        /// </summary>
        [Secondary]
        [Description(@"Raumsollwert reduziert 5.0..30.0")]
        public AnalogValue SetPointReduced => new AnalogValue(61, "�C", 5, 30);

        /// <summary>
        ///     Raumsollwert Frost 5.0..30.0
        /// </summary>
        [Secondary]
        [Description(@"Raumsollwert Frost 5.0..30.0")]
        public AnalogValue SetPointFrost => new AnalogValue(60, "�C", 5, 30);
    }
}