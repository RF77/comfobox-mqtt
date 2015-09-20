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
    public class EnumValue<TValue> : ItemValue<TValue>, IEnumValue
    {
        public EnumValue(uint id)
        {
            BacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, id);
        }

        protected internal override float? ConvertValueBack()
        {
            if (Value == null) return null;
            var convertValueBack = Convert.ToInt32(Value);
            return convertValueBack;
        }

        protected override void ConvertValue(object readValue)
        {
            var enumType = GetEnumType();
            if (readValue != null)
            {
                int val = (int) (float)readValue;
                var enumVal = (TValue)Enum.ToObject(enumType, val);
                Value = enumVal;
            }
            else
            {
                Value = default(TValue);
            }
        }

        public void SetValueFromString(string value)
        {
            Value = (TValue) Enum.Parse(GetEnumType(), value);
        }

        public Type GetEnumType()
        {
            var enumType = Nullable.GetUnderlyingType(typeof (TValue));
            return enumType;
        }
    }
}