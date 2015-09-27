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
using System.Diagnostics;
using System.Reflection;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models.Items
{
    [DebuggerDisplay("{Topic}: {Value}")]
    public class SpecialMqttItem<T> : ISpecialItem
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private T _value;

        public SpecialMqttItem(ComfoBoxMqttClient mqttClient, string topic)
        {
            MqttClient = mqttClient;
            Topic = $"{Settings.Default.BaseTopic}/{Settings.Default.SpecialSubTopic}/{topic}";
        }

        protected ComfoBoxMqttClient MqttClient { get; set; }

        public T Value
        {
            get { return _value; }
            set
            {
                if (!Equals(value, _value))
                {
                    _value = value;
                    PublishValue();
                }
            }
        }

        public string Topic { get; private set; }

        public virtual IEnumerable<string> Topics
        {
            get { return new[] {Topic}; }
        }

        private void PublishValue()
        {
            Logger.Debug($"PublishValue(): {Topic} = {Value}");

            MqttClient.Publish(Topic, Value.ToString(), Settings.Default.UseMqttRetain);
        }
    }
}