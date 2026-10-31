namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Ergebnis eines Lesevorgangs auf einem seriellen Port.
/// </summary>
/// <param name="Status">Status des Lesevorgangs.</param>
/// <param name="BytesRead">Anzahl der gelesenen Bytes (nur bei <see cref="SerialReadStatus.Data"/>).</param>
public readonly record struct SerialReadResult(SerialReadStatus Status, int BytesRead)
{
    /// <summary>Es wurden <paramref name="bytesRead"/> Bytes gelesen.</summary>
    public static SerialReadResult Data(int bytesRead) => new(SerialReadStatus.Data, bytesRead);

    /// <summary>Innerhalb der Wartezeit ist nichts angekommen.</summary>
    public static SerialReadResult Timeout => new(SerialReadStatus.Timeout, 0);

    /// <summary>Der Port ist geschlossen.</summary>
    public static SerialReadResult Closed => new(SerialReadStatus.Closed, 0);

    /// <summary>Der Port hat einen Fehler gemeldet.</summary>
    public static SerialReadResult Error => new(SerialReadStatus.Error, 0);
}
