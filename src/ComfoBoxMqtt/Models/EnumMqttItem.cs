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
using System.Linq;
using System.Reflection;
using ComfoBoxLib;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models
{
    public class EnumMqttItem : MqttItem
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public EnumMqttItem(IItemValue itemValue, RefreshPriority priority, ComfoBoxMqttClient mqttClient, string topic,
            Func<ComfoBoxClient> comfoBoxClientFunc)
            : base(itemValue, priority, mqttClient, topic, comfoBoxClientFunc)
        {
        }

        public override IEnumerable<string> Topics
        {
            get
            {
                if (SetAsNumberTopic != null)
                {
                    return base.Topics.Concat(new[] {AsNumberTopic, SetAsNumberTopic});
                }
                return base.Topics.Concat(new[] {AsNumberTopic});
            }
        }

        private string AsNumberTopic
        {
            get { return Topic + "/AsNumber"; }
        }

        private string SetAsNumberTopic
        {
            get
            {
                if (ItemValue.IsReadOnly) return null;
                return Topic + "/AsNumber/Set";
            }
        }

        protected override void SubscribeValues()
        {
            base.SubscribeValues();
            if (!ItemValue.IsReadOnly)
            {
                MqttClient.On[SetAsNumberTopic] = _ => { WriteValueIfChanged(_.Message); };
            }
        }

        protected override void WriteValueIfChanged(string message)
        {
            if (ItemValue.Value == null)
            {
                return;
            }
            float? currentVal = ItemValue.ConvertValueBack();
            IEnumValue enumValue = ItemValue as IEnumValue;
            float newValue;
            if (!float.TryParse(message, out newValue))
            {
                newValue = Convert.ToInt32(Enum.Parse(enumValue.GetEnumType(), message));
            }
            double TOLERANCE = 0.000001;
            if (currentVal != null && Math.Abs(newValue - currentVal.Value) > TOLERANCE)
            {
                var comfoBoxClient = _comfoBoxClientFunc?.Invoke();
                if (comfoBoxClient != null)
                {
                    ItemValue.SetNewValue(newValue);
                    comfoBoxClient.WriteValue(ItemValue);
                }
                else
                {
                    Logger.Error($"Couldn't write item {Topic}: {message}");
                }
            }
        }

        protected override void AdditionalActionForValueChanged()
        {
            var intValue = Convert.ToInt32(ItemValue.Value);
            Logger.Debug($"ValueChanged: {AsNumberTopic} = {intValue}");
            MqttClient.Publish(AsNumberTopic, intValue.ToString(), Settings.Default.UseMqttRetain);
        }
    }
}