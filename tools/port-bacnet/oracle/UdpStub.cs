// Stub für den Legacy-UDP-Transport. Der BACnet/IP-BVLC-Code des Legacy-Codecs
// braucht ihn nur zum Kompilieren - die MSTP-Ports berühren ihn nie.
using System.Net;

namespace System.IO.BACnet
{
    public class BacnetIpUdpProtocolTransport
    {
        public int SharedPort => 47808;

        public IPEndPoint LocalEndPoint => new IPEndPoint(IPAddress.Any, SharedPort);

        public void Send(byte[] buffer, int msg_length, IPEndPoint endpoint)
        {
            // vom Oracle nicht benutzt
        }

        public static void Convert(IPEndPoint endpoint, out BacnetAddress address)
        {
            address = new BacnetAddress(BacnetAddressTypes.IP, 0,
                endpoint.Address.GetAddressBytes());
        }
    }
}
