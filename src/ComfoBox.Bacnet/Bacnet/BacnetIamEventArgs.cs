namespace ComfoBox.Bacnet;

/// <summary>
/// Empfangene IAm-Antwort (Identity Announcement).
/// </summary>
/// <param name="Address">Adresse der Gegenstelle.</param>
/// <param name="DeviceId">Geräte-Instanz des OBJECT_DEVICE.</param>
/// <param name="MaxApdu">Maximale APDU-Länge der Gegenstelle.</param>
/// <param name="Segmentation">Unterstützte Segmentierung.</param>
/// <param name="VendorId">Hersteller-Kennung.</param>
public sealed record BacnetIamEventArgs(
    BacnetAddress Address,
    uint DeviceId,
    uint MaxApdu,
    BacnetSegmentations Segmentation,
    ushort VendorId);
