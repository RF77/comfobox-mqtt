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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Items.Enums;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Groups;
using ComfoBoxMqtt.Models.Items;
using ComfoBoxMqtt.Properties;
using log4net;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace ComfoBoxMqtt
{
	public class ComfoBoxMqttClient
	{
		private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private readonly ComfoBoxClient _comfoBoxClient;
		private CancellationTokenSource _cancellationTokenSource;
		private IEnumerable<MqttItem> _items;
		private MqttClient[] _mqttClients;

		private SpecialMqttItem<int?> _numberOfWritePer24hMqttItem;

		//private IEnumerable<VirtualMqttItem> _virtualItems;
		private IEnumerable<ISpecialItem> _specialItems;

		private readonly Dictionary<string, Action<string>> _subscriptions = new Dictionary<string, Action<string>>();


		public ComfoBoxMqttClient(string[] brokerHostNames, ComfoBoxClient comfoBoxClient)
		{
			_comfoBoxClient = comfoBoxClient;

			_mqttClients = brokerHostNames.Select(i => new MqttClient(i)).ToArray();
			Logger.Info($"MQTT Brokers: {string.Join(", ", brokerHostNames)}");
		}

		private void InitMqttClient(string brokerHostName, int brokerPort, string username, string password)
		{
			var client = new MqttClient(brokerHostName, brokerPort, false, MqttSslProtocols.None, null, null);
			client.Connect($"ComfoBox{DateTime.Now.Ticks}", username, password);
			_mqttClients = new[] {client};
		}

		public async void Client_ConnectionClosed(object sender, EventArgs e)
		{
			Logger.Info("Mqtt Connection closed -> reconnect soon...");
			await ReconnectAsync();
		}

		private async Task ReconnectAsync()
		{
			try
			{
				await Task.Delay(5000);
				ConnectMqtt();
			}
			catch (Exception ex)
			{
				Logger.Error($"Couldn't connect to MQTT broker, try again..., reason:{ex.Message} ");
				await ReconnectAsync();
			}
		}

		private void ConnectMqtt()
		{
			foreach (var client in _mqttClients)
				if (client.IsConnected == false)
				{
					Logger.Info($"Connect MQTT Client {client}");
					client.Connect($"ComfoBox{DateTime.Now.Ticks}", null, null, true, 2, true, $"{Settings.Default.BaseTopic}/Connected", "0", true, 60);
					client.Publish($"{Settings.Default.BaseTopic}/Connected", Encoding.UTF8.GetBytes("1"));
					client.Publish($"{Settings.Default.BaseTopic}/Version", Encoding.UTF8.GetBytes(Settings.Default.Version));
				}
		}

		public async Task StartAsync()
		{
			_cancellationTokenSource = new CancellationTokenSource();
			await Task.Delay(1);
			_items = ItemFactory.CreateItems(this, () => _comfoBoxClient);
			_specialItems = CreateSpecialItems();
#if DEBUG
			if (Settings.Default.WriteTopicsToFile)
			{
				var contents = string.Join("\r\n", _items.OrderBy(i => i.Topics.First()).Select(i => i.DescriptionString()));
				File.WriteAllText(Settings.Default.WriteTopicsJsonFilePath, SerializeItemsToJson(), Encoding.UTF8);
				File.WriteAllText(Settings.Default.WriteTopicsFilePath, $@"#Topics

Writable items have a /Set topic. Take care and write only values you exactly know! Some of the values cannot be changed until the config value ExpertMode is set to true.
I'm sorry about the english/german mix. Finally only german names would be better due to german source documents.

{contents}");
			}
#endif
			PollSpecialItems();
			await _comfoBoxClient.StartAsync();

			foreach (var mqttClient in _mqttClients)
			{
				mqttClient.MqttMsgPublishReceived += MqttClientOnMqttMsgPublishReceived;
				mqttClient.ConnectionClosed += Client_ConnectionClosed;
			}
			await ReconnectAsync();
			//_virtualItems = CreateVirtualItems();
		}

		private string SerializeItemsToJson()
		{
			var list = new List<ItemMetadata>();

			foreach (var item in _items)
			{
				var m = new ItemMetadata
				{
					Description = item.Description,
					PublishTopic = item.SetTopic,
					SubscribeTopic = item.Topic
				};
				var itemVal = item.ItemValue;
				m.IsReadOnly = itemVal.IsReadOnly;
				var a = itemVal as AnalogValue;
				if (a != null)
				{
					m.Min = a.Min;
					m.Max = a.Max;
					m.Unit = a.Unit;
					m.ValueType = "float?";
				}
				else
				{
					var e = itemVal as IEnumValue;
					if (e != null)
						m.ValueType = $"{e.GetEnumType()}?";
					else
						m.ValueType = "DateTime?";
				}
				list.Add(m);
			}
			return JsonConvert.SerializeObject(list);
		}

		private void MqttClientOnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs mqttMsgPublishEventArgs)
		{
			Action<string> action;
			if (_subscriptions.TryGetValue(mqttMsgPublishEventArgs.Topic, out action))
				try
				{
					var message = Encoding.UTF8.GetString(mqttMsgPublishEventArgs.Message);
					Logger.Info($"Received for topic {mqttMsgPublishEventArgs.Topic}: {message}");
					action(message);
				}
				catch (Exception ex)
				{
					Logger.Error($"Exception while received value for topic {mqttMsgPublishEventArgs.Topic}: {ex.Message}");
				}
		}


		private IEnumerable<VirtualMqttItem> CreateVirtualItems()
		{
			var items = new List<VirtualMqttItem>();
			items.Add(new VirtualMqttItem("IsHeatingMode", _items.First(i => i.Topic.Contains("HeatingCoolingMode")).Topic,
				(vi, m) =>
				{
					if (HeatingCoolingModes.OnlyHeating.ToString() == m)
						vi.SendValue(1);
					else
						vi.SendValue(0);
				}));
			items.Add(new VirtualMqttItem("IsCoolingMode", _items.First(i => i.Topic.Contains("HeatingCoolingMode")).Topic,
				(vi, m) =>
				{
					if (HeatingCoolingModes.OnlyCooling.ToString() == m)
						vi.SendValue(1);
					else
						vi.SendValue(0);
				}));

			items.Add(new VirtualMqttItem("IsHeatingWithHeatPump", _items.First(i => i.Topic.Contains("HeatPumpStatus")).Topic,
				(vi, m) =>
				{
					if (HeatPumpStatusEnums.HeatingWithHeatPump.ToString() == m)
						vi.SendValue(1);
					else
						vi.SendValue(0);
				}));
			items.Add(new VirtualMqttItem("WarmWater", _items.First(i => i.Topic.Contains("HeatPumpStatus")).Topic,
				(vi, m) =>
				{
					if (HeatPumpStatusEnums.WarmWater.ToString() == m)
						vi.SendValue(1);
					else
						vi.SendValue(0);
				}));

			return items;
		}

		private IEnumerable<ISpecialItem> CreateSpecialItems()
		{
			var items = new List<ISpecialItem>();
			_numberOfWritePer24hMqttItem = new SpecialMqttItem<int?>(this, "NumberOfWritesPer24h");
			items.Add(_numberOfWritePer24hMqttItem);
			return items;
		}

		private async void PollSpecialItems()
		{
			while (!_cancellationTokenSource.Token.IsCancellationRequested)
				try
				{
					_numberOfWritePer24hMqttItem.Value = ComfoBoxClient.CurrentNumberOfWrites;
					await Task.Delay(Settings.Default.PollSpecialValuesIntervalInMs);
				}
				catch (Exception ex)
				{
					Logger.Error($"PollSpecialItems() has thrown an exception: {ex.Message}");
					await Task.Delay(Settings.Default.PollSpecialValuesIntervalInMs);
				}
		}

		public async Task StartPollingAsync()
		{
			while (true)
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

		public void On(string topic, Action<string> action)
		{
			_subscriptions[topic] = action;
			foreach (var mqttClient in _mqttClients)
				mqttClient.Subscribe(new[] {topic}, new[] {MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});
		}

		public void Stop()
		{
			try
			{
				_comfoBoxClient.Stop();
				foreach (var mqttClient in _mqttClients)
				{
					mqttClient.Publish($"{Settings.Default.BaseTopic}/Connected", Encoding.UTF8.GetBytes("0"));

					mqttClient.Disconnect();
					mqttClient.MqttMsgPublishReceived -= MqttClientOnMqttMsgPublishReceived;
				}
				_subscriptions.Clear();
			}
			finally
			{
				_cancellationTokenSource.Cancel();
			}
		}

		public void Publish(string topic, string message, bool useMqttRetain)
		{
			foreach (var mqttClient in _mqttClients)
				if (mqttClient.IsConnected)
					mqttClient.Publish(topic, Encoding.UTF8.GetBytes(message), 0, useMqttRetain);
		}
	}
}