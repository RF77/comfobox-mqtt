using System.Text.Json;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die JSON-Einstellungen der API.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonSerializerDefaults.Web"/> sorgt für camelCase - die
/// DTO-Feldnamen der API sind damit <c>deviceId</c>, <c>uptimeSeconds</c> usw.
/// </para>
/// <para>
/// Die Optionen entsprechen den Vorgaben des ASP.NET-Core-Hosts
/// (<c>JsonSerializerDefaults.Web</c>) - der SSE-Stream serialisiert also
/// genauso wie die übrigen Endpunkte.
/// </para>
/// </remarks>
public static class ApiJson
{
    /// <summary>
    /// Die Serializer-Optionen (camelCase, <c>null</c>-Werte werden geschrieben).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
