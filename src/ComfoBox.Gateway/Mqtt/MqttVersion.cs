using System.Reflection;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Version des Gateways.
/// </summary>
/// <remarks>
/// Die Versionsangabe ist Teil des MQTT-Vertrags:
/// <c>ComfoBox/Version</c> trägt <c>Ver 1.0.0</c> (Altbestand),
/// <c>ComfoBox/Status/Version</c> trägt <c>1.0.0</c> (neu).
/// </remarks>
public static class MqttVersion
{
    /// <summary>
    /// Die Version ohne <c>Ver </c>-Präfix, z. B. <c>1.0.0</c>.
    /// </summary>
    public static string Current { get; } = ResolveVersion();

    /// <summary>
    /// Die Version im Format des Altbestands, z. B. <c>Ver 1.0.0</c>.
    /// </summary>
    public static string Legacy => $"Ver {Current}";

    /// <summary>
    /// Ermittelt die Version aus der Assembly.
    /// </summary>
    /// <returns>Die Version, z. B. <c>1.0.0</c>.</returns>
    private static string ResolveVersion()
    {
        var assembly = typeof(MqttVersion).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // "1.0.0+abc123" -> "1.0.0"
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            var dash = informational.IndexOf('-', StringComparison.Ordinal);
            var cut = plus < 0 ? dash : dash < 0 ? plus : Math.Min(plus, dash);

            return cut > 0 ? informational[..cut] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
