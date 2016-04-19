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
using System.ComponentModel;
using System.Linq;
using ComfoBoxLib;
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Items;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Models;
using ComfoBoxMqtt.Models.Items;
using ComfoBoxMqtt.Properties;
using Fasterflect;

namespace ComfoBoxMqtt.Groups
{
    public class ItemFactory
    {
        public static IEnumerable<MqttItem> CreateItems(ComfoBoxMqttClient client, Func<ComfoBoxClient> comfoBoxClientFunc)
        {
            string topicName = Settings.Default.BaseTopic;
            List<MqttItem> items = new List<MqttItem>();

            CreateItemsFromClass(items, typeof(Zone), topicName, client, comfoBoxClientFunc);
            CreateItemsFromClass(items, typeof(Tests), topicName, client, comfoBoxClientFunc);
            CreateItemsFromClass(items, typeof (Warmwater), topicName, client, comfoBoxClientFunc);
            CreateItemsFromClass(items, typeof (States), topicName, client, comfoBoxClientFunc);
            CreateItemsFromClass(items, typeof (Time), topicName, client, comfoBoxClientFunc);
            CreateItemsFromClass(items, typeof (Config), topicName, client, comfoBoxClientFunc);

            return items;
        }

        private static void CreateItemsFromClass(List<MqttItem> list, Type classType, string topicName, ComfoBoxMqttClient client, Func<ComfoBoxClient> comfoBoxClientFunc)
        {
            var topicSubName = classType.Name;
            var newTopicName = $"{topicName}/{topicSubName}";

            foreach (var subType in classType.GetNestedTypes())
            {
                CreateItemsFromClass(list, subType, newTopicName, client, comfoBoxClientFunc);
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

                MqttItem item;

                if (propertyValue is IEnumValue)
                {
                    item = new EnumMqttItem(propertyValue, RefreshPriority.None, client, topic, comfoBoxClientFunc);
                }
                else
                {
                    item = new MqttItem(propertyValue, RefreshPriority.None, client, topic, comfoBoxClientFunc);
                }

                var desc = propertyInfo.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute;
                if (desc != null)
                {
                    item.Description = desc.Description;
                }

                var expMode = propertyInfo.GetCustomAttributes(typeof(ExpertModeAttribute), false).FirstOrDefault() as ExpertModeAttribute;
                if (expMode != null)
                {
                    item.ExpertMode = true;
                }

                list.Add(item);
            }
        }
    }
}