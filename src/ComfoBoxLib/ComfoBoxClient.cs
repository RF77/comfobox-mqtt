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

        public ComfoBoxClient(string portName, int baudrate, short sourceAddress = 3)
        {
            PortName = portName;
            Baudrate = baudrate;
            SourceAddress = sourceAddress;
            StartTimer();
        }

        private static bool WriteProtectionEnabled
        {
            get { return Settings.Default.MaxNumberOfWritesPer24h > 0; }
        }

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

        internal void ReadWriteExample()
        {
            BacnetValue Value;
            bool ret;

            StringBuilder sb = new StringBuilder(1000000);
            // Read Present_Value property on the object ANALOG_INPUT:1 provided by the device 1026
            // Scalar value only
            for (uint dp = 1; dp < 4100; dp++)
            {
                try
                {
                    Logger.Debug($"ReadWriteExample(): Try getting dp {dp}");
                    ret = ReadScalarValue(1, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, dp),
                        BacnetPropertyIds.PROP_PRESENT_VALUE, out Value);

                    if (ret)
                    {
                        string value = $"Read value {dp}: {Value.Value}";

                        sb.AppendLine(value);
                        Logger.Debug($"ReadWriteExample(): {value}");

                        // Write Present_Value property on the object ANALOG_OUTPUT:0 provided by the device 4000
                        //BacnetValue newValue = new BacnetValue(Convert.ToSingle(2));   // expect it's a float
                        //ret = WriteScalarValue(1, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 3078), BacnetPropertyIds.PROP_PRESENT_VALUE, newValue);

                        //Console.WriteLine("Write feedback : " + ret.ToString());
                    }
                    else
                        Logger.Debug($"ReadWriteExample(): Error somewhere for {dp} !");
                }
                catch (Exception ex)
                {
                    Logger.Error($"ReadWriteExample() has thrown an exception: {ex.Message} \r\n{ex.StackTrace}");
                }
            }
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

        public BacnetValue ReadValue(IItemValue item)
        {
            BacnetValue val;
            ReadScalarValue(Settings.Default.BacnetMasterId,
                item.BacnetObjectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                out val);
            return val;
        }

        public bool WriteValue(IItemValue item, object newItemValue)
        {
            BacnetValue val = new BacnetValue(item.Tag, item.ConvertValueBack(newItemValue));
            return WriteScalarValue(Settings.Default.BacnetMasterId,
                item.BacnetObjectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                val);
        }
    }
}