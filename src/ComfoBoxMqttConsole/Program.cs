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
using System.Reflection;
using ComfoBoxLib;
using ComfoBoxLib.Properties;
using ComfoBoxMqtt;
using log4net;
using log4net.Config;
using Nito.AsyncEx;

namespace ComfoBoxMqttConsole
{
    internal class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            Run();
        }

        private static void Run()
        {
            AsyncContext.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var client = new ComfoBoxMqttClient(Settings.Default.MqttBrokerAddress, new ComfoBoxClient(Settings.Default.Port, Settings.Default.Baudrate, Settings.Default.BacnetClientId));
                        await client.StartAsync();
                        await client.StartPollingAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Run() has thrown an exception: {ex.Message}");
                    }
                }
            });
            Console.ReadLine();
        }
    }
}