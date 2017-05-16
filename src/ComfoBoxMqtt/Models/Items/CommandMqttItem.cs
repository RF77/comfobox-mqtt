using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComfoBoxLib;
using ComfoBoxLib.Values;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models.Items
{
	public class CommandMqttItem : MqttItem
	{
		private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public CommandMqttItem(IItemValue itemValue, RefreshPriority priority, ComfoBoxMqttClient mqttClient, string topic,
			Func<ComfoBoxClient> comfoBoxClientFunc)
			: base(itemValue, priority, mqttClient, topic, comfoBoxClientFunc)
		{
		}

		protected override void WriteValueIfChanged(string message)
		{
			try
			{
				Logger.Info($"Write command with value = {message}, topic = {Topic}");
				var comfoBoxClient = _comfoBoxClientFunc?.Invoke();
				if (comfoBoxClient != null)
				{
					comfoBoxClient.WriteValueObj(ItemValue, float.Parse(message));
				}
				else
				{
					Logger.Error($"Couldn't write item {Topic}: {message}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"WriteValueIfChanged() has thrown an exception: {ex.Message}");
			}
		}
	}
}