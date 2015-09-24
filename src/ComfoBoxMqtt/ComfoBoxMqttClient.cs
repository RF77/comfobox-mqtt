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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Charlotte;
using ComfoBoxLib;
using ComfoBoxLib.Properties;
using ComfoBoxMqtt.Groups;
using ComfoBoxMqtt.Models;
using log4net;
using Settings = ComfoBoxMqtt.Properties.Settings;

namespace ComfoBoxMqtt
{
    public class ComfoBoxMqttClient : MqttModule
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ComfoBoxClient _comfoBoxClient;
        private IEnumerable<MqttItem> _items;

        public ComfoBoxMqttClient(string brokerHostName, int brokerPort, string username, string password,
            ComfoBoxClient comfoBoxClient) : base(brokerHostName, brokerPort, username, password)
        {
            _comfoBoxClient = comfoBoxClient;
        }

        public ComfoBoxMqttClient(string brokerHostName, int brokerPort, ComfoBoxClient comfoBoxClient)
            : base(brokerHostName, brokerPort)
        {
            _comfoBoxClient = comfoBoxClient;
        }

        public ComfoBoxMqttClient(string brokerHostName, ComfoBoxClient comfoBoxClient) : base(brokerHostName)
        {
            _comfoBoxClient = comfoBoxClient;
        }

        public async Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await Task.Delay(1);
            _items = ItemFactory.CreateItems(this, () => _comfoBoxClient);
#if DEBUG
            if (Settings.Default.WriteTopicsToFile)
            {
                File.WriteAllText(Settings.Default.WriteTopicsFilePath, string.Join("\r\n", _items.SelectMany(i => i.Topics)));
            }
#endif
            Connect();
            await _comfoBoxClient.StartAsync();
        }

        public new Mqtt On => base.On;

        public async Task StartPollingAsync()
        {
            while (true)
            {
                try
                {
                    Logger.Info($"StartPolling(): Read all values");
                    foreach (var mqttItem in _items)
                    {
                        await mqttItem.ReadAsync(_comfoBoxClient);
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }

                    await Task.Delay(60000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Logger.Info($"StartPolling(): Stop Requested");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error($"StartPolling() has thrown an exception: {ex.Message}");
                    _comfoBoxClient.Dispose();
                    throw;
                }
            }
        }

        public void Stop()
        {
            _comfoBoxClient.Stop();
            Disconnect();
            _cancellationTokenSource.Cancel();
        }
    }
}