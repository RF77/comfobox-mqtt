namespace ComfoBox.Bacnet;

/// <summary>
/// Die vom Gateway benötigte Teilmenge des BACnet-Clients.
/// </summary>
/// <remarks>
/// Die Schnittstelle ist die Testnaht: der Gateway-Test spricht mit einem
/// Attrapp-Client, nicht mit echter Hardware.
/// </remarks>
public interface IBacnetClient : IDisposable
{
    /// <summary>Zeitfenster für die Antwort einer Gegenstelle (ms).</summary>
    int Timeout { get; set; }

    /// <summary>Anzahl der Versuche, bis eine Anfrage als fehlgeschlagen gilt.</summary>
    int Retries { get; set; }

    /// <summary>Zeitfenster bis ein Frame auf die Schnittstelle geschrieben wurde (ms).</summary>
    int TransmitTimeout { get; set; }

    /// <summary>Priorität beim Schreiben einer Eigenschaft.</summary>
    uint WritePriority { get; set; }

    /// <summary>Wird ausgelöst, wenn eine Gegenstelle auf ein WhoIs geantwortet hat.</summary>
    event EventHandler<BacnetIamEventArgs>? IamReceived;

    /// <summary>Startet den Client und den Transport.</summary>
    void Start();

    /// <summary>Stoppt den Client und den Transport.</summary>
    Task StopAsync();

    /// <summary>Sendet ein WhoIs an alle Geräte im Bus.</summary>
    /// <param name="lowLimit">Untere Grenze der Geräte-Instanz oder -1.</param>
    /// <param name="highLimit">Obere Grenze der Geräte-Instanz oder -1.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    Task WhoIsAsync(int lowLimit = -1, int highLimit = -1, CancellationToken cancellationToken = default);

    /// <summary>Liest eine Eigenschaft.</summary>
    /// <param name="address">Adresse der Gegenstelle.</param>
    /// <param name="objectId">Objekt, dessen Eigenschaft gelesen wird.</param>
    /// <param name="propertyId">Eigenschaft.</param>
    /// <param name="arrayIndex">Array-Index oder <c>BACNET_ARRAY_ALL</c>.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    Task<IList<BacnetValue>> ReadPropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        uint arrayIndex = Serialize.ASN1.BACNET_ARRAY_ALL,
        CancellationToken cancellationToken = default);

    /// <summary>Schreibt eine Eigenschaft.</summary>
    /// <param name="address">Adresse der Gegenstelle.</param>
    /// <param name="objectId">Objekt, dessen Eigenschaft geschrieben wird.</param>
    /// <param name="propertyId">Eigenschaft.</param>
    /// <param name="values">Zu schreibende Werte.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    Task WritePropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        IEnumerable<BacnetValue> values,
        CancellationToken cancellationToken = default);
}
