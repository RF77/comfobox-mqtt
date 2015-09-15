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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComfoBoxLib;

namespace DemoClient.ViewModels
{
    public class GroupViewModel : TreeItemViewModel
    {
        public GroupViewModel(string name) : base(name)
        {
        }

        public List<TreeItemViewModel> Children { get; } = new List<TreeItemViewModel>();

        public void Add(GroupViewModel @group)
        {
            Children.Add(group);
        }

        public void Add(ItemViewModel item)
        {
            Children.Add(item);
        }

        public void ExpandAll()
        {
            foreach (var group in Children.OfType<GroupViewModel>())
            {
                group.ExpandAll();
            }
            foreach (var item in Children.OfType<ItemViewModel>())
            {
                item.IsExpanded = true;
            }
            IsExpanded = true;
        }

        public override async Task ReadAllValuesAsync(ComfoBoxClient client)
        {
            foreach (var child in Children)
            {
                await child.ReadAllValuesAsync(client);
            }
        }
    }
}