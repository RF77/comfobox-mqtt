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
using System.ComponentModel;
using System.IO.BACnet;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ComfoBoxLib.Annotations;
using log4net;

namespace ComfoBoxLib.Values
{
    public abstract class ItemValue<TValue> : IItemValue
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private TValue _value;
        private BacnetApplicationTags _tag;
        public BacnetObjectId BacnetObjectId { get; set; }

        public event Action<object, float> InitializedValueChanged;

        public TValue Value
        {
            get { return _value; }
            set
            {
                if (!Equals(value, _value))
                {
                    bool isInitialized = _value != null;
                    _value = value;
                    OnPropertyChanged();
                    if (isInitialized)
                    {
                        OnInitializedValueChanged();
                    }
                }
            }
        }

        private void OnInitializedValueChanged()
        {
            var f = ConvertValueBack();
            if (f != null) InitializedValueChanged?.Invoke(this, f.Value);
        }

        object IItemValue.Value => _value;

        public string Unit { get; protected internal set; }

        public bool IsReadOnly { get; internal set; }

        public BacnetApplicationTags Tag
        {
            get { return _tag; }
        }

        public async Task ReadValueAsync(ComfoBoxClient client)
        {
            //Temp: as long there is no async client.ReadValue
            await Task.Delay(1);
            BacnetValue value = client.ReadValue(this);
            _tag = value.Tag;
            if (value.Value != null)
            {
                ConvertValue(value.Value);
            }
        }

        public async Task WriteValueAsync(ComfoBoxClient client)
        {
            //Temp: as long there is no async client.ReadValue
            await Task.Delay(1);
            float? value = ConvertValueBack();
            if (value != null)
            {
                client.WriteValue(this);
            }
        }

        public abstract float? ConvertValueBack();
        public virtual void SetNewValue(object newValue)
        {
            Value = (TValue) newValue;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void ConvertValue(object readValue)
        {
            Value = (TValue) readValue;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}