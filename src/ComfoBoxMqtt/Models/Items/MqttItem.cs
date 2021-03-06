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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models.Items
{
    [DebuggerDisplay("{Topic}: {ItemValue.Value}")]
    public class MqttItem : MqttItemBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected readonly Func<ComfoBoxClient> _comfoBoxClientFunc;

        public MqttItem(IItemValue itemValue, RefreshPriority priority, ComfoBoxMqttClient mqttClient, string topic,
            Func<ComfoBoxClient> comfoBoxClientFunc)
        {
            _comfoBoxClientFunc = comfoBoxClientFunc;
            ItemValue = itemValue;
            Priority = priority;
            MqttClient = mqttClient;
            Topic = topic;
            ItemValue.PropertyChanged += ItemValue_PropertyChanged;
            SubscribeValues();
        }

        public IItemValue ItemValue { get; set; }
        internal RefreshPriority Priority { get; private set; }

        public string SetTopic
        {
            get
            {
                if (ItemValue.IsReadOnly) return null;
                return Topic + "/Set";
            }
        }

        public string Description { get; set; }
        public bool ExpertMode { get; set; }

        public virtual IEnumerable<string> Topics
        {
            get
            {
                if (SetTopic != null)
                {
                    return new[] {Topic, SetTopic};
                }
                return new[] {Topic};
            }
        }

        public string DescriptionString()
        {
            var analogValue = ItemValue as AnalogValue;
            string minMax = string.Empty;
            if (analogValue != null)
            {
                if (analogValue.Min != null && analogValue.Max != null)
                {
                    minMax = $@"Min: {analogValue.Min.Value}, Max: {analogValue.Max.Value}, Needs Expert Mode: {ExpertMode}

"; 
                }
            }

            return $@"## {Topics.First().Replace("/",".")}
### MQTT Topics
{string.Join("\r\n", Topics.Select(t => $"* {t}"))}

### Description
{minMax}{Description}

";
        }

        public virtual string MinMaxDescription => "\r\n";

        protected virtual void SubscribeValues()
        {
            if (!ItemValue.IsReadOnly)
            {
                MqttClient.On(SetTopic, async m =>
                {
                    WriteValueIfChanged(m);
                    await ReadAsync(_comfoBoxClientFunc());
                });
            }
        }

        protected virtual void WriteValueIfChanged(string message)
        {
            try
            {
                Logger.Debug($"SubscribeValues(): topic = {Topic}, Value ={message}");
                if (ItemValue.Value == null)
                {
                    return;
                }
                float val = float.Parse(message);
                double TOLERANCE = 0.000001;
                if (Math.Abs(val - (float) ItemValue.Value) > TOLERANCE)
                {
                    var comfoBoxClient = _comfoBoxClientFunc?.Invoke();
                    if (comfoBoxClient != null)
                    {
                        comfoBoxClient.WriteValue(ItemValue, val);
                    }
                    else
                    {
                        Logger.Error($"Couldn't write item {Topic}: {val}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Value received: Exception: {ex.Message}");
            }
            //Log("Data received from {0} (in {1}): {2}", _.sensor, _.room, _.Message);
        }

        private void ItemValue_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ItemValue.Value))
            {
                Logger.Debug($"ValueChanged: {Topic} = {ItemValue.Value}");
                PublishValue();
                AdditionalActionForValueChanged();
            }
        }

        protected virtual void AdditionalActionForValueChanged()
        {
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

