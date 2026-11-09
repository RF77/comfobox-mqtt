namespace ComfoBox.Gateway.Security;

/// <summary>
/// Die Rechte eines API-Tokens.
/// </summary>
/// <remarks>
/// <para>
/// Die Werte sind aufsteigend geordnet - ein Token mit einem höheren Wert darf
/// alles, was ein Token mit einem niedrigeren Wert darf.
/// </para>
/// <list type="bullet">
/// <item><see cref="Read"/> - nur lesen (Status, Items, Diagnose);</item>
/// <item><see cref="Control"/> - zusätzlich Werte schreiben (Heizung regeln);</item>
/// <item><see cref="Admin"/> - zusätzlich Konfiguration, Token-Verwaltung und
/// Neustart ("Advanced Mode").</item>
/// </list>
/// </remarks>
public enum TokenScope
{
    /// <summary>
    /// Nur lesender Zugriff.
    /// </summary>
    Read = 0,

    /// <summary>
    /// Lesen und Schreiben von Werten (kein Experten-Modus, keine Verwaltung).
    /// </summary>
    Control = 1,

    /// <summary>
    /// Vollzugriff einschließlich Verwaltung und Neustart.
    /// </summary>
    Admin = 2,
}
