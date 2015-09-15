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
    public class Outputs
    {
        [Description("Primärpumpe Y1")]
        [Primary]
        public AnalogValue PrimaryPump => new AnalogValue(2634, "%") {IsReadOnly = true};

        [Description("Passivkühlen R1")]
        [Primary]
        public AnalogValue FreeCooling => new AnalogValue(2624, null) {IsReadOnly = true};

        //[Description("Passivkühlen inv")]
        //public AnalogValue R2 => new AnalogValue(2625, null);

        [Description("Verdichter R3")]
        [Primary]
        public AnalogValue Verdichter => new AnalogValue(2626, null) {IsReadOnly = true};

        [Description("Heizkreispumpe R4")]
        [Primary]
        public AnalogValue ZonePump => new AnalogValue(2627, null) {IsReadOnly = true};

        [Description("Warmwasser R5")]
        [Primary]
        public AnalogValue WarmWater => new AnalogValue(2628, null) {IsReadOnly = true};

        //[Description("Warmwasser invers")]
        //[Primary]
        //public AnalogValue R6 => new AnalogValue(2629, null);

        [Description("Warmwasser elektrisch R7")]
        [Primary]
        public AnalogValue ElectricalWarmWater => new AnalogValue(2630, null) {IsReadOnly = true};

        //[Description("Kein Funktion")]
        //public AnalogValue R8 => new AnalogValue(2631, null);

        //[Description("Kollektorpumpe")]
        //[Secondary]
        //public AnalogValue R9 => new AnalogValue(2632, null);

        [Description("Zusatzheizung R10")]
        [Primary]
        public AnalogValue AdditionalHeater => new AnalogValue(2633, null) {IsReadOnly = true};
    }
}