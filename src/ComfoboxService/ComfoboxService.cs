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
using System.Reflection;
using System.ServiceProcess;
using ComfoBoxLib;
using ComfoBoxMqtt;
using log4net;

namespace ComfoboxService
{
    public partial class ComfoboxService : ServiceBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ComfoBoxMqttClient _client;

        public ComfoboxService()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            while (true)
            {
                try
                {
                    _client = new ComfoBoxMqttClient("localhost", new ComfoBoxClient("COM4", 76800));
                    await _client.StartAsync();
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
                }
            }
        }

        protected override void OnStop()
        {
            _client.Stop();
        }
    }
}