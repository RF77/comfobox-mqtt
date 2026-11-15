using System.Text;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Bildet Item-Ids auf ASCII-taugliche Discovery-Kennungen ab.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum das nötig ist:</b> Die Item-Ids enthalten Umlaute
/// (<c>config.energy_comsumer.zone.gebäudeträgheit</c>) - sie stammen aus dem
/// Legacy-Katalog und sind Teil des Topic-Vertrags. Die
/// <b>Discovery-Themen und <c>unique_id</c>s</b> dürfen dagegen nur
/// <c>a-z</c>, <c>A-Z</c>, <c>0-9</c>, <c>-</c> und <c>_</c> enthalten; die
/// MQTT-Integration von Home Assistant meldet sonst
/// „Discovery topic contains invalid characters".
/// </para>
/// <para>
/// Die Umlaute werden <b>transliteriert</b> (<c>ä</c> → <c>ae</c>) und nicht
/// entfernt: so bleiben die Kennungen lesbar und stabil. Für reine
/// ASCII-Ids ist <see cref="Slug"/> eine Identität - bestehende Entitäten
/// behalten ihre <c>unique_id</c>.
/// </para>
/// </remarks>
public static class DiscoveryId
{
    /// <summary>
    /// Wandelt eine Id in eine ASCII-taugliche Kennung um.
    /// </summary>
    /// <param name="value">Die Id, z. B. <c>zone.gebäudeträgheit</c>.</param>
    /// <returns>Die Kennung, z. B. <c>zone.gebaeudetraegheit</c>.</returns>
    public static string Slug(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length + 8);

        foreach (var character in value)
        {
            switch (character)
            {
                case 'ä': builder.Append("ae"); break;
                case 'ö': builder.Append("oe"); break;
                case 'ü': builder.Append("ue"); break;
                case 'Ä': builder.Append("Ae"); break;
                case 'Ö': builder.Append("Oe"); break;
                case 'Ü': builder.Append("Ue"); break;
                case 'ß': builder.Append("ss"); break;
                default:
                    builder.Append(IsAllowed(character) ? character : '_');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ist das Zeichen in einer Discovery-Kennung zulässig?
    /// </summary>
    private static bool IsAllowed(char character)
        => character is (>= 'a' and <= 'z')
            or (>= 'A' and <= 'Z')
            or (>= '0' and <= '9')
            or '_'
            or '-';
}
