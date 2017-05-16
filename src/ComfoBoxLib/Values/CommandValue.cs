using System.IO.BACnet;
using System.Reflection;
using log4net;

namespace ComfoBoxLib.Values
{
	public class CommandValue : ItemValue<float?>, ICommandValue
	{
		public CommandValue(uint id)
		{
			BacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, id);
		}

		public override float? ConvertValueBack(object value)
		{
			Logger.Info($"Convert Command Value {value}");
			return (float?)value;
		}

		private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
	}
}