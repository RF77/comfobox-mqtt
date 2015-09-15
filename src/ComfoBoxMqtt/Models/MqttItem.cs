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
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models
{
    [DebuggerDisplay("{Topic}: {ItemValue.Value}")]
    public class MqttItem
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MqttItem(IItemValue itemValue, RefreshPriority priority, ComfoBoxMqttClient mqttClient, string topic)
        {
            ItemValue = itemValue;
            Priority = priority;
            MqttClient = mqttClient;
            Topic = topic;
            ItemValue.PropertyChanged += ItemValue_PropertyChanged;
        }

        private IItemValue ItemValue { get; set; }
        internal RefreshPriority Priority { get; private set; }

        private ComfoBoxMqttClient MqttClient { get; set; }

        public string Topic { get; private set; }

        private void ItemValue_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ItemValue.Value))
            {
                Logger.Debug($"ItemValue_PropertyChanged(): {Topic} = {ItemValue.Value}");
                PublishValue();
            }
        }

        private void PublishValue()
        {
            MqttClient.Publish(Topic, ItemValue.Value.ToString(), Settings.Default.UseMqttRetain);
        }

        public async Task ReadAsync(ComfoBoxClient comfoBoxClient)
        {
            await ItemValue.ReadValueAsync(comfoBoxClient);
        }
    }
}