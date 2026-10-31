namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Status eines Lesevorgangs auf einem seriellen Port.
/// </summary>
public enum SerialReadStatus
{
    /// <summary>Es wurden Daten gelesen.</summary>
    Data,

    /// <summary>Innerhalb der Wartezeit ist nichts angekommen.</summary>
    Timeout,

    /// <summary>Der Port wurde geschlossen (oder ist nie offen gewesen).</summary>
    Closed,

    /// <summary>Unbekannter Portfehler (z. B. Adapter abgezogen).</summary>
    Error,
}
