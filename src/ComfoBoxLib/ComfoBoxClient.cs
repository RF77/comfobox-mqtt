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
using System.IO.BACnet;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ComfoBoxLib.Properties;
using ComfoBoxLib.Values;
using log4net;

namespace ComfoBoxLib
{
    public class ComfoBoxClient : IDisposable
    {
        public static int CurrentNumberOfWrites;
        public static bool TimerIsRunning;
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<BacNode> _devicesList = new List<BacNode>();
        private BacnetClient _bacnetClient;
        private TaskCompletionSource<bool> _starTaskCompletionSource;

        public ComfoBoxClient(string portName, int baudrate, short sourceAddress)
        {
            PortName = portName;
            Baudrate = baudrate;
            SourceAddress = sourceAddress;
            StartTimer();
        }

        private static bool WriteProtectionEnabled => Settings.Default.MaxNumberOfWritesPer24h > 0;

        public string PortName { get; }
        public int Baudrate { get; }
        public short SourceAddress { get; }

        public bool Initialized { get; set; }

        public event Action ValuesChanged;

        public void Dispose()
        {
            ValuesChanged = null;
            _bacnetClient?.Dispose();
        }

        private async void StartTimer()
        {
            if (!TimerIsRunning && WriteProtectionEnabled)
            {
                while (true)
                {
                    TimerIsRunning = true;
                    await Task.Delay(24*60*60*1000); //24h
                    CurrentNumberOfWrites = 0;
                }
            }
        }

        public async Task StartAsync()
        {
            var task = StartInternalAsync();
            var timeOutTask = Task.Delay(Settings.Default.MqttNoResponseTimeout);
            await Task.WhenAny(task, timeOutTask);
            if (!task.IsCompleted)
            {
                string message =
                    $"StartAsync(): Didn't get any messages from the Bacnet Master device. This could be a wrong port address.";
                Logger.Error(message);
                throw new TimeoutException(message);
            }
        }

        private Task StartInternalAsync()
        {
            if (!Initialized)
            {
                _starTaskCompletionSource = new TaskCompletionSource<bool>();
                _bacnetClient = new BacnetClient(new BacnetMstpProtocolTransport(PortName, Baudrate, SourceAddress));

                _bacnetClient.Start(); // go

                // Send WhoIs in order to get back all the Iam responses :  
                _bacnetClient.OnIam += OnIam;
                _bacnetClient.WhoIs();
                Logger.Debug($"StartAsync(): Bacnet Client started / WhoIs sent. Waiting for IAm");
            }
            return _starTaskCompletionSource.Task;
        }

        public void Stop()
        {
            Logger.Info($"Stop(): ComfoClient is stopping...");
            Dispose();
        }

        public bool ReadScalarValue(int deviceId, BacnetObjectId bacnetObjet, BacnetPropertyIds property,
            out BacnetValue value)
        {
            lock (this)
            {
                IList<BacnetValue> noScalarValue;

                value = new BacnetValue(null);

                // Looking for the device
                var adr = DeviceAddr((uint) deviceId);
                if (adr == null) return false; // not found

                // Property Read
                if (_bacnetClient.ReadPropertyRequest(adr, bacnetObjet, property, out noScalarValue) == false)
                    return false;

                value = noScalarValue[0];
                return true;
            }
        }



        public bool WriteScalarValue(int deviceId, BacnetObjectId bacnetObjet, BacnetPropertyIds property,
            BacnetValue value)
        {
            lock (this)
            {
                if (!WriteProtectionEnabled || CurrentNumberOfWrites < Settings.Default.MaxNumberOfWritesPer24h)
                {

                    // Looking for the device
                    var adr = DeviceAddr((uint) deviceId);
                    if (adr == null) return false; // not found

                    Logger.Debug($"WriteScalarValue(): {bacnetObjet.Instance} = {value}");

                    bool retValue = _bacnetClient.WritePropertyRequest(adr, bacnetObjet, property, new[] {value});
                    CurrentNumberOfWrites++;
                    ValuesChanged?.Invoke();
                    return retValue;
                }
                Logger.Error(
                    $"WriteScalarValue(): Number of Writes per 24h exceeded. Change the number MaxNumberOfWritesPer24h if requried.");
                return false;
            }
        }


        private BacnetAddress DeviceAddr(uint deviceId)
        {
            BacnetAddress ret;

            lock (_devicesList)
            {
                foreach (BacNode bn in _devicesList)
                {
                    ret = bn.GetAdd(deviceId);
                    if (ret != null) return ret;
                }
                // not in the list
                return null;
            }
        }

        private void OnIam(BacnetClient sender, BacnetAddress adr, uint deviceId, uint maxApdu,
            BacnetSegmentations segmentation, ushort vendorId)
        {
            lock (_devicesList)
            {
                Logger.Debug($"OnIam(): DeviceId = {deviceId}");
                // Device already registred ?
                if (_devicesList.Any(bn => bn.GetAdd(deviceId) != null))
                {
                    return; // Yes
                }

                // Not already in the list
                _devicesList.Add(new BacNode(adr, deviceId)); // add it
                Initialized = true;
                if (deviceId == Settings.Default.BacnetMasterId)
                {
                    _starTaskCompletionSource.TrySetResult(true);
                }
            }
        }

        public void ReadMultipleValues()
        {
            lock (this)
            {
                throw new NotImplementedException();
                //IList<BacnetValue> noScalarValue;

                //value = new BacnetValue(null);

                //// Looking for the device
                //var adr = DeviceAddr((uint)deviceId);
                //if (adr == null) return false; // not found

                //List<BacnetReadAccessSpecification> items = new List<BacnetReadAccessSpecification>();


                //// Property Read
                //if (_bacnetClient.ReadPropertyMultipleRequest(adr, bacnetObjet, property, out noScalarValue) == false)
                //    return false;

                //value = noScalarValue[0];
                //return true;
            }
        }

        public BacnetValue ReadValue(IItemValue item)
        {
            BacnetValue val;
            ReadScalarValue(Settings.Default.BacnetMasterId,
                item.BacnetObjectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                out val);
            return val;
        }

        public bool WriteValue(IItemValue item, object newItemValue, BacnetPropertyIds propertyType = BacnetPropertyIds.PROP_PRESENT_VALUE)
        {
            BacnetValue val = new BacnetValue(item.Tag, item.ConvertValueBack(newItemValue));
            return WriteScalarValue(Settings.Default.BacnetMasterId,
                item.BacnetObjectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                val);
        }

	    public bool WriteValueObj(IItemValue item, object newItemValue, BacnetPropertyIds propertyType = BacnetPropertyIds.PROP_PRESENT_VALUE)
	    {
		    BacnetValue val = new BacnetValue(item.Tag, newItemValue);
		    var result = WriteScalarValue(Settings.Default.BacnetMasterId,
			    item.BacnetObjectId,
			    BacnetPropertyIds.PROP_PRESENT_VALUE,
			    val);
			Logger.Info($"WriteValueObj success = {result}, value = {newItemValue}");
		    return result;
	    }
	}
}