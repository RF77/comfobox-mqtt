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
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Properties;
using ComfoBoxMqtt;
using log4net;
using log4net.Config;
using Nito.AsyncEx;

namespace ComfoboxService
{
    public partial class ComfoboxService : ServiceBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ComfoBoxMqttClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _stop;

        public ComfoboxService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task.Run(() =>
            {
                AsyncContext.Run(async () =>
                {
                    XmlConfigurator.Configure();
                    while (!_stop)
                    {
                        try
                        {
                            _client?.Stop();
                            _client = new ComfoBoxMqttClient(Settings.Default.MqttBrokerAddress, new ComfoBoxClient(Settings.Default.Port, Settings.Default.Baudrate, Settings.Default.BacnetClientId));
                            await _client.StartAsync();
                            await _client.StartPollingAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            Logger.Info($"OnStart(): Received Cancel");
                            return;
                        }
                        catch (Exception ex)
                        {
                            string message = $"ComfoBoxService(): Excpetion {ex.Message},\r\n{ex.StackTrace}}}";
                            Logger.Error(message);
                            await Task.Delay(1000);
                        }
                    }
                });
            }, _cancellationTokenSource.Token);
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            _client.Stop();
            _stop = true;
            _cancellationTokenSource.Cancel();
            base.OnStop();
        }
    }
}