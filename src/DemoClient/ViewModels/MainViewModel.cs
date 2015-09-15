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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.BACnet;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using ComfoBoxLib;
using ComfoBoxLib.Annotations;
using DemoClient.Groups;
using log4net;
using log4net.Config;

namespace DemoClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string MyPort = "COM4";
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ObservableCollection<string> _availablePorts;
        private int _baudrate;
        private ComfoBoxClient _client;
        private string _connectButtonText;
        private string _currentState;
        private IEnumerable<GroupViewModel> _groups;
        private string _port;

        public MainViewModel()
        {
            XmlConfigurator.Configure();
            Logger.Info($"Starting {WindowTitle}");
            ConnectButtonText = "Connect";
            Groups = GroupFactory.CreateGroups();
            Groups.First().ExpandAll();
            Baudrate = 76800;
            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
            Logger.Info($"Available Ports: {string.Join(";", AvailablePorts)}");

            if (AvailablePorts.Any())
            {
                Port = AvailablePorts.First();
                if (AvailablePorts.Contains(MyPort))
                {
                    Port = MyPort;
                }
            }

            BacnetTypes = new ObservableCollection<string>(Enum.GetNames(typeof (BacnetObjectTypes)));
        }

        public uint BacnetAddress { get; set; }

        public ObservableCollection<string> BacnetTypes { get; set; }

        public string BacnetType { get; set; }

        public object Result { get; set; }

        public string WindowTitle => $"ComfoBox Demo Client (Ver {FileVersion})";

        public string FileVersion
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = fvi.FileVersion;
                return version;
            }
        }

        public ObservableCollection<string> AvailablePorts
        {
            get { return _availablePorts; }
            set
            {
                if (Equals(value, _availablePorts)) return;
                _availablePorts = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<GroupViewModel> Groups
        {
            get { return _groups; }
            set
            {
                if (Equals(value, _groups)) return;
                _groups = value;
                OnPropertyChanged();
            }
        }

        public int Baudrate
        {
            get { return _baudrate; }
            set
            {
                if (value == _baudrate) return;
                _baudrate = value;
                OnPropertyChanged();
            }
        }

        public string CurrentState
        {
            get { return _currentState; }
            set
            {
                if (value == _currentState) return;
                _currentState = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<int> PossibleBaudrates => new[] {9600, 19200, 38400, 76800};

        public string Port
        {
            get { return _port; }
            set
            {
                if (value == _port) return;
                _port = value;
                OnPropertyChanged();
            }
        }

        public string ConnectButtonText
        {
            get { return _connectButtonText; }
            set
            {
                if (value == _connectButtonText) return;
                _connectButtonText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async void Connect()
        {
            try
            {
                if (_client != null)
                {
                    //Disconnect
                    _client.Stop();
                    _client = null;
                    ConnectButtonText = "Connect";
                    CurrentState = "Disconnected";
                    Logger.Info("Disconnected");
                }
                else
                {
                    //Connect
                    _client = new ComfoBoxClient(Port, Baudrate);
                    Logger.Info($"Connecting to Port={Port} with Baudrate={Baudrate}");
                    ConnectButtonText = "Disconnect";
                    CurrentState = "Connecting...";
                    await _client.StartAsync();
                    Logger.Info("Connected! Reading All Values...");
                    CurrentState = "Connected! Reading All Values...";
                    await ReadAllValuesAsync();
                    Logger.Info("Connected! All values read.");
                    CurrentState = "Connected! All values read.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Connect() has thrown an exception: {ex.Message}");
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private async Task ReadAllValuesAsync()
        {
            await Groups.First().ReadAllValuesAsync(_client);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async void ReadAllValues()
        {
            try
            {
                await ReadAllValuesAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"ReadAllValues() has thrown an exception: {ex.Message}");
                MessageBox.Show(ex.Message, "Error");
            }
        }

        public async void ReadValue()
        {
            try
            {
                BacnetValue val = new BacnetValue();
                await Task.Delay(1);
                _client.ReadScalarValue(1,
                    new BacnetObjectId((BacnetObjectTypes) Enum.Parse(typeof (BacnetObjectTypes), BacnetType),
                        BacnetAddress), BacnetPropertyIds.PROP_PRESENT_VALUE, out val);
                Result = val.Value;
            }
            catch (Exception ex)
            {
                Logger.Error($"ReadAllValues() has thrown an exception: {ex.Message}");
                MessageBox.Show(ex.Message, "Error");
            }
        }
    }
}