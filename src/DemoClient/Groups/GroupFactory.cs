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
using System.Collections.Generic;
using ComfoBoxLib;
using ComfoBoxLib.Items;
using ComfoBoxLib.Values;
using DemoClient.ViewModels;
using Fasterflect;

namespace DemoClient.Groups
{
    public class GroupFactory
    {
        public static IEnumerable<GroupViewModel> CreateGroups(Func<ComfoBoxClient> clientFunc)
        {
            var root = new GroupViewModel("ComfoBox");
            CreateGroupFromClass(root, typeof (Zone), clientFunc);
            CreateGroupFromClass(root, typeof (Warmwater), clientFunc);
            CreateGroupFromClass(root, typeof (States), clientFunc);
            CreateGroupFromClass(root, typeof (Time), clientFunc);
            CreateGroupFromClass(root, typeof (Config), clientFunc);

            return new[] {root};
        }

        private static GroupViewModel CreateGroupFromClass(GroupViewModel parent, Type classType, Func<ComfoBoxClient> clientFunc)
        {
            var group = new GroupViewModel(classType.Name);
            parent.Add(group);
            foreach (var subType in classType.GetNestedTypes())
            {
                CreateGroupFromClass(group, subType, clientFunc);
            }

            var instance = classType.CreateInstance();

            foreach (var propertyInfo in classType.Properties(Flags.Default))
            {
                var propertyValue = instance.GetPropertyValue(propertyInfo.Name) as IItemValue;
                if (propertyValue != null && propertyValue.IsReadOnly)
                {
                    group.Add(new ReadOnlyItemViewModel(propertyInfo.Name, clientFunc) {Item = propertyValue});
                }
                else if (propertyValue is AnalogValue || propertyValue is AnalogValue)
                {
                    group.Add(new AnalogValueItemViewModel(propertyInfo.Name, clientFunc) {Item = propertyValue});
                }
                else if (propertyValue is DateValue)
                {
                    group.Add(new ReadOnlyItemViewModel(propertyInfo.Name, clientFunc) {Item = propertyValue});
                }
                else
                {
                    var enumItem = propertyValue as IEnumValue;
                    group.Add(new EnumItemViewModel(propertyInfo.Name, clientFunc) {Item = propertyValue});
                }
            }

            return group;
        }
    }
}