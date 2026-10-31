namespace ComfoBox.Bacnet;

/// <summary>
/// Empfangener BACnet-Frame (NPDU/APDU) ohne MS/TP-Header.
/// </summary>
/// <param name="Payload">Nutzlast, beginnt mit dem NPDU.</param>
/// <param name="RemoteAddress">Adresse der sendingen Gegenstelle.</param>
public sealed record BacnetFrameReceivedEventArgs(byte[] Payload, BacnetAddress RemoteAddress);
