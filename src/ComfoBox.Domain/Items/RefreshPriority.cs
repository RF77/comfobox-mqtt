namespace ComfoBox.Domain.Items;

/// <summary>
/// Wie oft ein Item gelesen wird.
/// </summary>
/// <remarks>
/// Im Legacy-Code wurden die Prioritäts-Attribute gelesen und verworfen -
/// alle Items bekamen <c>RefreshPriority.None</c> (Befund A15). Hier werden
/// sie endlich ausgewertet.
/// </remarks>
public enum RefreshPriority
{
    /// <summary>
    /// Das Item trug kein Prioritäts-Attribut.
    /// </summary>
    None,

    /// <summary>
    /// Einmal beim Start lesen (z. B. Gerätetyp, Softwareversion).
    /// </summary>
    Once,

    /// <summary>
    /// Häufig lesen - der Nutzer sieht diese Werte zuerst.
    /// </summary>
    Primary,

    /// <summary>
    /// Seltener lesen.
    /// </summary>
    Secondary,
}
