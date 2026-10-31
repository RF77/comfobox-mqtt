using System.Text.Json;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Anfrage zum Schreiben eines Item-Werts.
/// </summary>
/// <remarks>
/// <para>
/// Der Wert wird bewusst als <see cref="JsonElement"/> angenommen: so kann der
/// Endpunkt selbst entscheiden, ob eine Zahl oder ein Text (Enum-Name)
/// geliefert wurde - und einen unpassenden Typ **ablehnen**, statt ihn zu
/// raten.
/// </para>
/// <para>
/// Erlaubt sind:
/// </para>
/// <list type="bullet">
/// <item><c>{ "value": 21.5 }</c> - Zahl (InvariantCulture, Dezimalkomma ungültig);</item>
/// <item><c>{ "value": "Normal" }</c> - Enum-Name (case-insensitiv);</item>
/// <item><c>{ "value": 6 }</c> - Enum-Zahl.</item>
/// </list>
/// </remarks>
public sealed class WriteValueRequest
{
    /// <summary>
    /// Der zu schreibende Wert.
    /// </summary>
    public JsonElement Value { get; set; }
}
