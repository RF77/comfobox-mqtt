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
    public class Zone
    {
        /// <summary>
        ///     Betriebsart
        /// </summary>
        [Primary]
        public EnumValue<OperationModes> OperationMode => new EnumValue<OperationModes>(830);

        /// <summary>
        ///     Betriebsart Heizen und K�hlen
        /// </summary>
        [Primary]
        public EnumValue<HeatingCoolingModes> HeatingCoolingMode => new EnumValue<HeatingCoolingModes>(3078);

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der
        ///     eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Normal� g�ltig.
        /// </summary>
        [Secondary]
        public AnalogValue PartyTime => new AnalogValue(674, "h");

        /// <summary>
        ///     Die Funktion wird gestartet, indem die gew�nschte Dauer eingestellt wird. W�hrend der
        ///     eingestellten Zeit ist dann unabh�ngig der Schaltuhr der Raumsollwert �Reduziert� bzw
        ///     �Frostschutz� g�ltig(abh�ngig von der Betriebsart).
        /// </summary>
        [Secondary]
        public AnalogValue EcoTime => new AnalogValue(675, "h");

        /// <summary>
        ///     Um eine genauere Regelung zu erm�glichen, kann hier die reale, mit einem genauen Messger�t
        ///     ermittelte Raumtemperatur eingestellt werden.
        /// </summary>
        [Secondary]
        public AnalogValue CalibrateTemprature => new AnalogValue(75, "�C");

        /// <summary>
        ///     Die Adaption der Heizkennlinie korrigiert die Heizkurve bei der aktuellen Aussentemperatur. Die
        ///     Korrektur sollte jeweils bei tiefer und bei hoher Aussentemperatur durchgef�hrt werden.Diese
        ///     Funktion kann nur einmal t�glich aufgerufen werden.
        /// </summary>
        [Secondary]
        public AnalogValue ManualAdoption => new AnalogValue(679, "K");

        /// <summary>
        ///     Raumsollwert normal 5.0..30.0
        /// </summary>
        [Primary]
        public AnalogValue SetPointNormal => new AnalogValue(62, "�C", 5, 30);

        /// <summary>
        ///     Raumsollwert reduziert 5.0..30.0
        /// </summary>
        [Secondary]
        public AnalogValue SetPointReduced => new AnalogValue(61, "�C", 5, 30);

        /// <summary>
        ///     Raumsollwert Frost 5.0..30.0
        /// </summary>
        [Secondary]
        public AnalogValue SetPointFrost => new AnalogValue(60, "�C", 5, 30);

        /// <summary>
        ///     Read only values
        /// </summary>
        public class States
        {
            /// <summary>
            ///     Istwert Raum
            /// </summary>
            [Primary]
            public AnalogValue RoomTemperature => new AnalogValue(12, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Aktueller Sollwert Raum
            /// </summary>
            [Primary]
            public AnalogValue CurrentSetPoint => new AnalogValue(208, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Aktuell gemessene Vorlauftemperatur
            /// </summary>
            [Primary]
            public AnalogValue FlowTemperature => new AnalogValue(14, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Berchnete Soll - Vorlauftemperatur
            /// </summary>
            [Primary]
            public AnalogValue CalculatedFlowTemperature => new AnalogValue(209, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Aussentemperatur
            /// </summary>
            [Primary]
            public AnalogValue OutdoorTemperature => new AnalogValue(10, "�C") {IsReadOnly = true};

            /// <summary>
            ///     Geb�udebezogene Aussentemperatur
            /// </summary>
            [Primary]
            public AnalogValue OutdoorTemperatureBuilding => new AnalogValue(205, "�C") {IsReadOnly = true};

            // Other values?: 59, 398, 676
        }
    }

    public enum OperationModes : uint
    {
        Manual = 1,
        Standby,
        OnlyWarmwater,
        NormalAndFrost,
        NormalAndReduced,
        Normal,
        Reduced
    }

    public enum HeatingCoolingModes : uint
    {
        Auto = 1,
        OnlyCooling,
        OnlyHeating,
        Neutral = 5
    }
}