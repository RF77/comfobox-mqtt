﻿// /*******************************************************************************
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
using System.Collections.Generic;
using ComfoBoxLib.Values;

namespace DemoClient.ViewModels
{
    public class EnumItemViewModel : ItemViewModel
    {
        public EnumItemViewModel(string name) : base(name)
        {
            
        }

        private void Item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Item.Value))
            {
                EnumValue = Item.Value.ToString();
            }
        }

        public string[] EnumValues
        {
            get
            {
                return Enum.GetNames(Item.Value.GetType());
            }
        }

        private string _enumValue;

        public string EnumValue
        {
            get { return _enumValue; }
            set
            {
                if (_enumValue != value)
                {
                    _enumValue = value;
                    OnPropertyChanged();
                    var enumItem = Item as IEnumValue;
                    if (enumItem != null && _enumValue != Item.Value.ToString())
                    {
                        enumItem.SetValueFromString(value);
                    }
                }
            }
        }

        protected internal override void OnItemChanged()
        {
            Item.PropertyChanged += Item_PropertyChanged;
        }
    }
}