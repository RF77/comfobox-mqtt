using Microsoft.Extensions.Configuration;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Konfigurationsquellen des Gateways und ihre Präzedenz.
/// </summary>
/// <remarks>
/// <para>
/// Die Reihenfolge (die letzte gewinnt):
/// </para>
/// <code>
/// appsettings.json            (im Image, Defaults)
/// appsettings.{Environment}.json
/// Umgebungsvariablen COMFOBOX_*
/// config.json                (vom Assistenten geschrieben - höchste Priorität)
/// </code>
/// <para>
/// <b>Warum hat die Datei Vorrang?</b> Passwörter in Umgebungsvariablen
/// landen sichtbar in <c>docker inspect</c>. Die ausdrückliche Wahl des
/// Benutzers im Assistenten darf außerdem nicht von einer Umgebungsvariable
/// überschrieben werden.
/// </para>
/// </remarks>
public static class GatewayConfiguration
{
    /// <summary>
    /// Das Präfix der Umgebungsvariablen, z. B. <c>COMFOBOX_Gateway__BaseTopic</c>.
    /// </summary>
    public const string EnvironmentPrefix = "COMFOBOX_";

    /// <summary>
    /// Ermittelt das Datenverzeichnis.
    /// </summary>
    /// <param name="configuration">Die Konfiguration.</param>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad.</param>
    /// <returns>Der absolute Pfad des Datenverzeichnisses.</returns>
    public static string ResolveDataDirectory(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configured = configuration.GetSection(GatewayOptions.SectionName)[nameof(GatewayOptions.DataDirectory)]
            ?? new GatewayOptions().DataDirectory;

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(contentRootPath, configured));
    }

    /// <summary>
    /// Fügt die Umgebungsvariablen und die <c>config.json</c> als
    /// Konfigurationsquellen hinzu.
    /// </summary>
    /// <param name="configuration">Die Konfiguration des Hosts.</param>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad.</param>
    /// <param name="onWarning">
    /// Wird aufgerufen, wenn die Optionen des Add-ons nicht lesbar sind
    /// (z. B. eine kaputte <c>options.json</c>).
    /// </param>
    /// <remarks>
    /// <para>
    /// Die Reihenfolge der Aufrufe ist die Präzedenz: erst die
    /// Umgebungsvariablen, dann die Datei.
    /// </para>
    /// <para>
    /// Die Datei wird **einmal** beim Start gelesen; Änderungen daran werden
    /// erst nach einem Neustart wirksam (außer über <c>PUT /api/config</c>,
    /// das die Datei selbst schreibt).
    /// </para>
    /// <para>
    /// <b>Add-on-Betrieb:</b> Die <c>options.json</c> des Home-Assistant-
    /// Supervisors wird **zuletzt** hinzugefügt. Sie überschreibt damit sowohl
    /// die <c>config.json</c> als auch die Umgebungsvariablen - das Image
    /// setzt <c>COMFOBOX_Bacnet__Port</c> als Vorgabe, und die Portwahl des
    /// Add-ons muss stärker sein.
    /// </para>
    /// </remarks>
    public static void AddGatewaySources(
        ConfigurationManager configuration,
        string contentRootPath,
        Action<string>? onWarning = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        // COMFOBOX_Gateway__BaseTopic → Gateway:BaseTopic
        configuration.AddEnvironmentVariables(EnvironmentPrefix);

        var directory = ResolveDataDirectory(configuration, contentRootPath);
        var path = Path.Combine(directory, ConfigStore.FileName);

        configuration.AddJsonFile(path, optional: true, reloadOnChange: true);

        // Die Optionen des Home-Assistant-Add-ons (optional).
        var optionsPath = ResolveAddOnOptionsPath(configuration, contentRootPath);
        var provider = new AddOnOptionsProvider(optionsPath, key => configuration[key], onWarning);

        // Der ConfigurationManager implementiert Add(IConfigurationSource) nur
        // explizit - deshalb der Cast.
        ((IConfigurationBuilder)configuration).Add(new AddOnOptionsSource(provider));
    }

    /// <summary>
    /// Ermittelt den Pfad der Optionsdatei des Add-ons.
    /// </summary>
    /// <param name="configuration">Die Konfiguration.</param>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad.</param>
    /// <returns>
    /// Der absolute Pfad der <c>options.json</c> (oder der Standardpfad,
    /// wenn nichts konfiguriert ist).
    /// </returns>
    public static string ResolveAddOnOptionsPath(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configured = configuration.GetSection(GatewayOptions.SectionName)[nameof(GatewayOptions.AddOnOptionsPath)];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = new GatewayOptions().AddOnOptionsPath;
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(contentRootPath, configured));
    }
}
