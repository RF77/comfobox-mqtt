namespace ComfoBoxMqtt
{
	public class ItemMetadata
	{
		public string SubscribeTopic { get; set; }
		public string PublishTopic { get; set; }
		public string Description { get; set; }
		public string ValueType { get; set; }
		public string Unit { get; set; }
		public bool IsReadOnly { get; set; }

		public float? Min { get; set; }
		public float? Max { get; set; }
	}
}