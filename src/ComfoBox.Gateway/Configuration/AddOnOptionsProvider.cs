using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Konfigurationsquelle des Home-Assistant-Add-ons.
/// </summary>
/// <remarks>
/// <para>
/// Der Supervisor von Home Assistant schreibt die validierten Optionen des
/// Add-ons nach <c>/data/options.json</c>. Diese Datei bildet das
/// Options-Formular des Add-ons ab - sie ist damit die Quelle der Wahl des
/// seriellen Ports.
/// </para>
/// <para>
/// <b>Präzedenz:</b> Die Datei wird <b>zuletzt</b> hinzugefügt. Sie
/// überschreibt damit sowohl die <c>config.json</c> als auch die
/// Umgebungsvariablen.
/// </para>
/// <para>
/// <b>Warum die Umgebungsvariablen überstimmt werden:</b> Das Image setzt
/// <c>COMFOBOX_Bacnet__Port=/dev/ttyUSB0</c> als Vorgabe. Wäre die
/// Umgebungsvariable stärker, würde die Portwahl des Add-ons (der Kern des
/// Add-ons) wirkungslos. Die Kette <c>Umgebung → config.json → options.json</c>
/// ist damit durchgängig aufsteigend.
/// </para>
/// <para>
/// <b>Leere Werte werden ignoriert:</b> Der Supervisor schreibt **alle**
/// Optionen - auch die, die der Nutzer nicht angefasst hat (mit dem
/// Standardwert). Ein leerer Broker-Host würde einen Broker ohne Host erzeugen
/// und der Gateway würde wegen <c>ValidateOnStart</c> nicht starten.
/// </para>
/// </remarks>
public sealed class AddOnOptionsProvider : ConfigurationProvider
{
    /// <summary>
    /// Der Standardpfad der Optionen des Add-ons.
    /// </summary>
    public const string DefaultPath = "/data/options.json";

    private readonly string _path;
    private readonly Func<string, string?> _existingValue;
    private readonly Action<string>? _onWarning;

    /// <summary>
    /// Erzeugt die Quelle.
    /// </summary>
    /// <param name="path">Der Pfad der Optionsdatei.</param>
    /// <param name="existingValue">
    /// Liefert den aktuellen Wert eines Konfigurationsschlüssels. Damit kann
    /// der Provider entscheiden, ob die Broker-Optionen überhaupt sinnvoll
    /// sind (ein Broker muss existieren).
    /// </param>
    /// <param name="onWarning">Wird bei Problemen aufgerufen.</param>
    public AddOnOptionsProvider(
        string path,
        Func<string, string?>? existingValue = null,
        Action<string>? onWarning = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
        _existingValue = existingValue ?? (static _ => null);
        _onWarning = onWarning;
    }

    /// <summary>
    /// Die abgebildeten Konfigurationsschlüssel (nur zum Nachsehen).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values =>
        new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Liest die Optionsdatei ein (falls sie existiert).
    /// </summary>
    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_path))
        {
            // Kein Add-on-Betrieb - die Datei fehlt einfach. Das ist **kein**
            // Fehler (manueller Betrieb, Docker Compose, Synology-Paket).
            return;
        }

        try
        {
            using var stream = File.OpenRead(_path);
            using var document = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

            Map(document.RootElement, Data, _existingValue, _onWarning);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Eine kaputte Datei darf den Gateway nicht stoppen - sie wird
            // ignoriert und der Betrieb läuft mit der übrigen Konfiguration.
            _onWarning?.Invoke(
                $"Die Add-on-Optionen konnten nicht gelesen werden ({_path}): {ex.Message}");
        }
    }

    /// <summary>
    /// Bildet die Optionen des Add-ons auf die Konfigurationsschlüssel ab.
    /// </summary>
    /// <param name="options">Das JSON-Objekt der Optionen.</param>
    /// <param name="target">Die Zielschlüssel (die Konfigurationsdaten).</param>
    /// <param name="existingValue">Liefert den aktuellen Wert eines Schlüssels.</param>
    /// <param name="onWarning">Wird bei Problemen aufgerufen.</param>
    public static void Map(
        JsonElement options,
        IDictionary<string, string?> target,
        Func<string, string?>? existingValue = null,
        Action<string>? onWarning = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (options.ValueKind != JsonValueKind.Object)
        {
            onWarning?.Invoke("Die Add-on-Optionen sind kein JSON-Objekt - sie werden ignoriert.");
            return;
        }

        var lookup = existingValue ?? (static _ => null);

        // ---------------------------------------------------------------
        // BACnet
        // ---------------------------------------------------------------
        MapString(options, "device", "Bacnet:Port", target);
        MapInt(options, "baudrate", "Bacnet:Baudrate", target, onWarning);

        // ---------------------------------------------------------------
        // Gateway
        // ---------------------------------------------------------------
        MapString(options, "base_topic", "Gateway:BaseTopic", target);

        // ---------------------------------------------------------------
        // MQTT
        //
        // Die Broker-Optionen werden nur übernommen, wenn ein Broker
        // benannt ist - sonst entstünde ein Broker ohne Host und der
        // Gateway würde nicht starten (ValidateOnStart).
        // ---------------------------------------------------------------
        var brokerHost = ReadString(options, "broker_host");
        var hasBroker = !string.IsNullOrWhiteSpace(brokerHost)
            || !string.IsNullOrWhiteSpace(lookup("Mqtt:Brokers:0:Host"));

        if (hasBroker)
        {
            MapString(options, "broker_host", "Mqtt:Brokers:0:Host", target);
            MapInt(options, "broker_port", "Mqtt:Brokers:0:Port", target, onWarning);
            MapString(options, "broker_username", "Mqtt:Brokers:0:Username", target);
            MapString(options, "broker_password", "Mqtt:Brokers:0:Password", target);
            MapBool(options, "use_tls", "Mqtt:Brokers:0:UseTls", target, onWarning);
        }

        MapBool(options, "enable_discovery", "Mqtt:EnableDiscovery", target, onWarning);

        // ---------------------------------------------------------------
        // Logging
        // ---------------------------------------------------------------
        MapString(options, "log_level", "Logging:Level", target);
    }

    /// <summary>
    /// Liest einen Textwert (leere Werte werden ignoriert).
    /// </summary>
    private static void MapString(
        JsonElement options,
        string option,
        string key,
        IDictionary<string, string?> target)
    {
        var value = ReadString(options, option);

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        target[key] = value;
    }

    /// <summary>
    /// Liest einen Zahlenwert.
    /// </summary>
    private static void MapInt(
        JsonElement options,
        string option,
        string key,
        IDictionary<string, string?> target,
        Action<string>? onWarning)
    {
        if (!options.TryGetProperty(option, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value))
        {
            onWarning?.Invoke(
                $"Die Add-on-Option '{option}' ist keine ganze Zahl - sie wird ignoriert.");
            return;
        }

        target[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Liest einen Wahrheitswert.
    /// </summary>
    private static void MapBool(
        JsonElement options,
        string option,
        string key,
        IDictionary<string, string?> target,
        Action<string>? onWarning)
    {
        if (!options.TryGetProperty(option, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            onWarning?.Invoke(
                $"Die Add-on-Option '{option}' ist kein Wahrheitswert - sie wird ignoriert.");
            return;
        }

        target[key] = element.GetBoolean() ? "true" : "false";
    }

    /// <summary>
    /// Liest einen Textwert aus den Optionen (oder <c>null</c>).
    /// </summary>
    private static string? ReadString(JsonElement options, string option)
    {
        if (!options.TryGetProperty(option, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString();
    }
}
