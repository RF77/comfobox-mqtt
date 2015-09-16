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
using System.Linq;
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Items;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Models;
using ComfoBoxMqtt.Properties;
using Fasterflect;

namespace ComfoBoxMqtt.Groups
{
    public class ItemFactory
    {
        public static IEnumerable<MqttItem> CreateItems(ComfoBoxMqttClient client)
        {
            string topicName = Settings.Default.BaseTopic;
            List<MqttItem> items = new List<MqttItem>();

            CreateItemsFromClass(items, typeof (Zone), topicName, client);
            CreateItemsFromClass(items, typeof (Warmwater), topicName, client);
            CreateItemsFromClass(items, typeof (HeatPump), topicName, client);
            CreateItemsFromClass(items, typeof (Counters), topicName, client);
            CreateItemsFromClass(items, typeof (Controller), topicName, client);
            CreateItemsFromClass(items, typeof (Time), topicName, client);
            CreateItemsFromClass(items, typeof (Outputs), topicName, client);
            CreateItemsFromClass(items, typeof (Config), topicName, client);

            return items;
        }

        private static void CreateItemsFromClass(List<MqttItem> list, Type classType, string topicName,
            ComfoBoxMqttClient client)
        {
            var topicSubName = classType.Name;
            var newTopicName = $"{topicName}/{topicSubName}";

            foreach (var subType in classType.GetNestedTypes())
            {
                CreateItemsFromClass(list, subType, newTopicName, client);
            }

            var instance = classType.CreateInstance();

            foreach (var propertyInfo in classType.Properties(Flags.Default))
            {
                var subTopicName = propertyInfo.Name;
                var prioAttribute = propertyInfo.Attributes.Attributes().OfType<PriorityAttribute>().FirstOrDefault();
                var propertyValue = instance.GetPropertyValue(propertyInfo.Name) as IItemValue;
                var topic = $"{newTopicName}/{subTopicName}";

                if (propertyValue.IsReadOnly)
                {
                    //if (prioAttribute == )
                }

                list.Add(new MqttItem(propertyValue, RefreshPriority.None, client, topic));
            }
        }
    }
}