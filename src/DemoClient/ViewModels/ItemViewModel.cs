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
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Values;

namespace DemoClient.ViewModels
{
    public class ItemViewModel : TreeItemViewModel
    {
        private readonly Func<ComfoBoxClient> _clientFunc;
        private IItemValue _item;

        public ItemViewModel(string name, Func<ComfoBoxClient> clientFunc) : base(name)
        {
            _clientFunc = clientFunc;
        }

        public IItemValue Item
        {
            get { return _item; }
            set
            {
                if (_item != value)
                {
                    _item = value;
                    OnItemChanged();
                    if (_item != null)
                    {
                        _item.InitializedValueChanged += _item_InitializedValueChanged;
                    }
                }
            }
        }

        private void _item_InitializedValueChanged(object sender, float newValue)
        {
            if (_clientFunc != null) Item.WriteValueAsync(_clientFunc());
        }

        protected internal virtual void OnItemChanged()
        {
        }

        public override async Task ReadAllValuesAsync(ComfoBoxClient client)
        {
            await Item.ReadValueAsync(client);
        }
    }
}