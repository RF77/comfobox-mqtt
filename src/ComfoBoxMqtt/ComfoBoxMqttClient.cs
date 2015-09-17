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
using ComfoBoxMqtt.Groups;
using ComfoBoxMqtt.Models;
using log4net;

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
            _items = ItemFactory.CreateItems(this);
            File.WriteAllText(@"c:\temp\topics.txt", string.Join("\r\n", _items.Select(i => i.Topic)));
            Connect();
            await _comfoBoxClient.StartAsync();
        }

        public async Task StartPollingAsync()
        {
            //bool reconnect = false;
            while (true)
            {
                try
                {
                    //if (reconnect)
                    //{
                    //    reconnect = false;
                    //    _comfoBoxClient.Dispose();
                    //    _comfoBoxClient = new ComfoBoxClient(_comfoBoxClient.PortName, _comfoBoxClient.Baudrate, _comfoBoxClient.SourceAddress);
                    //    await _comfoBoxClient.StartAsync();
                    //    await Task.Delay(1000);
                    //}
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
            _cancellationTokenSource.Cancel();
        }
    }
}