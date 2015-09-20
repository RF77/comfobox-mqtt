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
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models
{
    public class EnumMqttItem : MqttItem
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public EnumMqttItem(IItemValue itemValue, RefreshPriority priority, ComfoBoxMqttClient mqttClient, string topic)
            : base(itemValue, priority, mqttClient, topic)
        {
        }

        public override IEnumerable<string> Topics
        {
            get { return base.Topics.Concat(new[] {AsNumberTopic}); }
        }

        protected override void AdditionalActionForValueChanged()
        {
            var intValue = Convert.ToInt32(ItemValue.Value);
            Logger.Debug($"ValueChanged: {AsNumberTopic} = {intValue}");
            MqttClient.Publish(AsNumberTopic, intValue.ToString(), Settings.Default.UseMqttRetain);
        }

        private string AsNumberTopic
        {
            get { return Topic + "/AsNumber"; }
        }
    }
}