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
using System.IO.BACnet;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ComfoBoxLib.Annotations;
using log4net;

namespace ComfoBoxLib.Values
{
    public class ItemValue<TValue> : INotifyPropertyChanged, IItemValue
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private TValue _value;
        internal BacnetObjectId BacnetObjectId { get; set; }

        public TValue Value
        {
            get { return _value; }
            set
            {
                if (!Equals(value, _value))
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        object IItemValue.Value => _value;

        public string Unit { get; protected internal set; }

        public bool IsReadOnly { get; internal set; }

        public async Task ReadValueAsync(ComfoBoxClient client)
        {
            await Task.Delay(1);
            object value = client.ReadValue(this);
            if (value != null)
            {
                ConvertValue(value);
            }
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