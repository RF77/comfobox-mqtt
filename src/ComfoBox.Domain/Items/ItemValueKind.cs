namespace ComfoBox.Domain.Items;

/// <summary>
/// Der Werte-Kind eines Items.
/// </summary>
public enum ItemValueKind
{
    /// <summary>
    /// Eine Zahl mit Einheit und optionalen Grenzen.
    /// </summary>
    Analog,

    /// <summary>
    /// Eine Auswahl aus benannten Werten.
    /// </summary>
    Enum,

    /// <summary>
    /// Ein BACnet-Analogwert, dessen Wert nicht interpretiert wird.
    /// </summary>
    /// <remarks>
    /// Der Legacy-Code nutzte dafür den Werttyp <c>DateValue</c>. Der ist tot
    /// (Befund A20) und wird nicht wiederbelebt - der Wert bleibt leer. Das
    /// Item selbst bleibt im Katalog, damit die MQTT-Topics unverändert sind.
    /// </remarks>
    Date,
}
