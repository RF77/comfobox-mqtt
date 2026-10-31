using System.Text.Json;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Storage;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Der Zugriff auf die persistente Konfiguration (<c>config.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Die Datei liegt im Datenverzeichnis und hat den Modus <c>0600</c> - in ihr
/// stehen die gehashten Passwörter und die API-Token.
/// </para>
/// <para>
/// Die Datei hat **Vorrang** vor den Umgebungsvariablen: der Assistent
/// schreibt die ausdrückliche Wahl des Benutzers hinein, und ein
/// <c>docker inspect</c> darf sie nicht überschreiben.
/// </para>
/// </remarks>
public sealed class ConfigStore
{
    /// <summary>
    /// Der Name der Konfigurationsdatei im Datenverzeichnis.
    /// </summary>
    public const string FileName = "config.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    private JsonObject? _cached;
    private long _version;

    /// <summary>
    /// Erzeugt den Speicher.
    /// </summary>
    /// <param name="dataDirectory">Das Datenverzeichnis.</param>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    public ConfigStore(string dataDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _timeProvider = timeProvider ?? TimeProvider.System;
        DataDirectory = Path.GetFullPath(dataDirectory);
        FilePath = Path.Combine(DataDirectory, FileName);
    }

    /// <summary>
    /// Das Datenverzeichnis.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Der vollständige Pfad der Konfigurationsdatei.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Die Version der Datei - sie steigt bei jedem Schreibvorgang.
    /// </summary>
    /// <remarks>
    /// Damit erkennt ein Leser, dass sich die Datei geändert hat, ohne sie
    /// erneut lesen zu müssen.
    /// </remarks>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// Existiert die Konfigurationsdatei?
    /// </summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Liest die Konfiguration.
    /// </summary>
    /// <returns>
    /// Der Inhalt der Datei - ein leeres Objekt, wenn die Datei nicht
    /// existiert oder nicht lesbar ist.
    /// </returns>
    public JsonObject Read()
    {
        lock (_sync)
        {
            return ReadUnlocked();
        }
    }

    /// <summary>
    /// Ändert die Konfiguration und schreibt sie zurück.
    /// </summary>
    /// <param name="mutation">
    /// Die Änderung. Sie bekommt den aktuellen Inhalt und darf ihn verändern.
    /// </param>
    /// <remarks>
    /// Lesen, Ändern und Schreiben passieren unter einer Sperre - so
    /// überschreiben sich zwei parallele Änderungen nicht gegenseitig.
    /// </remarks>
    public void Mutate(Action<JsonObject> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        lock (_sync)
        {
            var config = ReadUnlocked();
            mutation(config);
            WriteUnlocked(config);
        }
    }

    /// <summary>
    /// Schreibt die Konfiguration (ersetzt den gesamten Inhalt).
    /// </summary>
    /// <param name="config">Der neue Inhalt.</param>
    public void Write(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_sync)
        {
            WriteUnlocked(config);
        }
    }

    /// <summary>
    /// Holt (oder erzeugt) einen Abschnitt in einer zu schreibenden Konfiguration.
    /// </summary>
    /// <param name="config">Die Konfiguration.</param>
    /// <param name="name">Der Name des Abschnitts.</param>
    /// <returns>Der Abschnitt.</returns>
    /// <remarks>
    /// Wichtig: der Aufrufer darf **keine** verschachtelten
    /// <see cref="Mutate"/>-Aufrufe machen - das innere Schreiben würde vom
    /// äußeren überschrieben.
    /// </remarks>
    public static JsonObject EnsureSection(JsonObject config, string name)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (config[name] is JsonObject existing)
        {
            return existing;
        }

        var section = new JsonObject();
        config[name] = section;
        return section;
    }

    /// <summary>
    /// Liest die Datei (ohne Sperre).
    /// </summary>
    private JsonObject ReadUnlocked()
    {
        if (_cached is not null)
        {
            return (JsonObject)_cached.DeepClone();
        }

        JsonObject? result = null;

        try
        {
            if (File.Exists(FilePath))
            {
                var text = File.ReadAllText(FilePath);
                result = JsonNode.Parse(text) as JsonObject;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Eine kaputte Datei darf den Gateway nicht stoppen - sie wird
            // wie eine leere Datei behandelt.
            result = null;
        }

        _cached = result ?? new JsonObject();
        return (JsonObject)_cached.DeepClone();
    }

    /// <summary>
    /// Schreibt die Datei (ohne Sperre).
    /// </summary>
    private void WriteUnlocked(JsonObject config)
    {
        Directory.CreateDirectory(DataDirectory);

        var json = config.ToJsonString(WriteOptions);

        AtomicFile.WriteAllText(FilePath, json, restrictPermissions: true);

        _cached = (JsonObject)config.DeepClone();
        Interlocked.Increment(ref _version);
    }
}
