using ComfoBox.Bacnet;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Die zur Laufzeit geltenden BACnet-Parameter.
/// </summary>
/// <remarks>
/// <para>
/// Diese Klasse ist - wie <see cref="SerialPortSettings"/> für den Port - der
/// einzige Ort, an dem die **aktuelle** Geräte-ID und das Zeitfenster je Item
/// stehen. Die Werte kommen beim Start aus der Konfiguration und können in der
/// Oberfläche geändert werden, ohne dass der Gateway neu startet.
/// </para>
/// <para>
/// <b>Warum eigene Klasse?</b> Geräte-ID und Zeitfenster werden bei **jedem**
/// Lese- und Schreibzugriff gebraucht. Der <see cref="BacnetDevice"/> liest sie
/// deshalb bei jedem Aufruf - ein Neustart für eine geänderte Geräte-ID wäre
/// bei einem Tippfehler in der Adresse unnötig.
/// </para>
/// </remarks>
public sealed class BacnetRuntimeSettings
{
    /// <summary>Die kleinste zulässige Geräte-ID.</summary>
    public const int MinDeviceId = 0;

    /// <summary>Die größte zulässige Geräte-ID (255 ist reserviert).</summary>
    public const int MaxDeviceId = 254;

    /// <summary>Das kleinste zulässige Zeitfenster in Millisekunden.</summary>
    public const int MinItemTimeoutMs = 100;

    /// <summary>Das größte zulässige Zeitfenster in Millisekunden.</summary>
    public const int MaxItemTimeoutMs = 60_000;

    private int _deviceId;
    private int _itemTimeoutMs;

    /// <summary>
    /// Erzeugt die Einstellungen.
    /// </summary>
    /// <param name="deviceId">Die Geräte-Instanz der ComfoBox.</param>
    /// <param name="itemTimeoutMs">Das Zeitfenster je Item in Millisekunden.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn ein Wert außerhalb des Bereichs liegt.</exception>
    public BacnetRuntimeSettings(int deviceId, int itemTimeoutMs)
    {
        Update(deviceId, itemTimeoutMs);
    }

    /// <summary>
    /// Die Geräte-Instanz der ComfoBox (ELESTA: <c>1</c>).
    /// </summary>
    public int DeviceId => Volatile.Read(ref _deviceId);

    /// <summary>
    /// Das Zeitfenster je Item in Millisekunden.
    /// </summary>
    public int ItemTimeoutMs => Volatile.Read(ref _itemTimeoutMs);

    /// <summary>
    /// Das Zeitfenster als <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ItemTimeout => TimeSpan.FromMilliseconds(ItemTimeoutMs);

    /// <summary>
    /// Die BACnet-Adresse der ComfoBox auf dem MS/TP-Bus.
    /// </summary>
    /// <remarks>
    /// Auf MS/TP entspricht die MAC-Adresse der Geräte-Instanz - die ComfoBox
    /// ist mit <c>DeviceId</c> direkt adressierbar.
    /// </remarks>
    public BacnetAddress DeviceAddress =>
        new(BacnetAddressTypes.MSTP, 0, [(byte)DeviceId]);

    /// <summary>
    /// Setzt die neuen Werte.
    /// </summary>
    /// <param name="deviceId">Die Geräte-Instanz der ComfoBox.</param>
    /// <param name="itemTimeoutMs">Das Zeitfenster je Item in Millisekunden.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn ein Wert außerhalb des Bereichs liegt.</exception>
    public void Update(int deviceId, int itemTimeoutMs)
    {
        if (deviceId is < MinDeviceId or > MaxDeviceId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deviceId),
                deviceId,
                $"Die Geräte-ID muss zwischen {MinDeviceId} und {MaxDeviceId} liegen.");
        }

        if (itemTimeoutMs is < MinItemTimeoutMs or > MaxItemTimeoutMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(itemTimeoutMs),
                itemTimeoutMs,
                $"Das Zeitfenster muss zwischen {MinItemTimeoutMs} und {MaxItemTimeoutMs} ms liegen.");
        }

        Volatile.Write(ref _deviceId, deviceId);
        Volatile.Write(ref _itemTimeoutMs, itemTimeoutMs);
    }
}
