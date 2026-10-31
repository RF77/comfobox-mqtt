using System.ComponentModel.DataAnnotations;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Einstellungen der Protokollierung.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Der Name des Konfigurationsabschnitts.
    /// </summary>
    public const string SectionName = "Logging";

    /// <summary>
    /// Die Obergrenze für die Kapazität des Ringpuffers.
    /// </summary>
    public const int MaxRingBufferCapacity = 100_000;

    /// <summary>
    /// Das Verzeichnis der Log-Dateien.
    /// </summary>
    /// <remarks>
    /// Im Container ist das ein beschreibbares Volume - ein schreibgeschützter
    /// Mount lässt den Container beim Start abbrechen.
    /// </remarks>
    [Required]
    public string Directory { get; init; } = "logs";

    /// <summary>
    /// Die Anzahl der aufbewahrten Log-Dateien.
    /// </summary>
    [Range(1, 365)]
    public int RetainedFileCount { get; init; } = 14;

    /// <summary>
    /// Der In-Memory-Ringpuffer für den Log-Viewer der Web-UI.
    /// </summary>
    public RingBufferOptions RingBuffer { get; init; } = new();
}

/// <summary>
/// Die Einstellungen des In-Memory-Ringpuffers.
/// </summary>
public sealed class RingBufferOptions
{
    /// <summary>
    /// Die Anzahl der Einträge im Puffer.
    /// </summary>
    /// <remarks>
    /// <c>0</c> schaltet den Puffer ab. Der Standard von 1000 Einträgen
    /// entspricht etwa 400 KB Speicher und deckt einen Betriebstag ab.
    /// </remarks>
    [Range(0, LoggingOptions.MaxRingBufferCapacity)]
    public int Capacity { get; init; } = 1000;

    /// <summary>
    /// Der minimale Level, der in den Puffer wandert.
    /// </summary>
    [Required]
    public string MinimumLevel { get; init; } = "Information";
}
