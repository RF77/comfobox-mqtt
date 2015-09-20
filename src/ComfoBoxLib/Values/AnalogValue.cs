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

using System.IO.BACnet;

namespace ComfoBoxLib.Values
{
    public class AnalogValue : ItemValue<float?>
    {
        public AnalogValue(uint id, string unit, float? min = null, float? max = null)
        {
            Min = min;
            Max = max;
            Unit = unit;
            BacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, id);
        }

        public float? Min { get; private set; }
        public float? Max { get; private set; }
        protected internal override float? ConvertValueBack()
        {
            return Value;
        }
    }
}