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

using System;
using System.IO.BACnet;

namespace ComfoBoxLib.Values
{
    /// <summary>
    ///     Datum
    /// </summary>
    public class DateValue : ItemValue<DateTime>
    {
        public DateValue(uint id, string unit)
        {
            Unit = unit;
            BacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, id);
        }

        protected override void ConvertValue(object readValue)
        {
            //base.ConvertValue(readValue);
        }
    }
}