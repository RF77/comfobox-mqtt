namespace ComfoBoxMqtt.Models.Items
{
    public class MqttItemBase
    {
        protected ComfoBoxMqttClient MqttClient { get; set; }
        public string Topic { get; protected set; }
    }
}