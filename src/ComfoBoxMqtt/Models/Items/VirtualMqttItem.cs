using System;
using System.Diagnostics;
using System.Reflection;
using ComfoBoxMqtt.Properties;
using log4net;

namespace ComfoBoxMqtt.Models.Items
{
    [DebuggerDisplay("{Topic}: {ItemValue.Value}")]
    public class VirtualMqttItem : MqttItemBase
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public VirtualMqttItem(string topic, string topicToSubscribe, Action<VirtualMqttItem, string> messageHandler)
        {
            Topic = $"{Settings.Default.BaseTopic}/{Settings.Default.VirtualTopic}/{topic}";
            MqttClient.On(topicToSubscribe, m =>
            {
                try
                {
                    messageHandler(this, m);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                }
            });
        }

        public void SendValue(object message)
        {
            MqttClient.Publish(Topic, message.ToString(), true);
        }
    }
}