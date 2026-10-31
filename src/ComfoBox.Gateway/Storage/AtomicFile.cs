namespace ComfoBox.Gateway.Storage;

/// <summary>
/// Das atomare Schreiben einer Datei.
/// </summary>
/// <remarks>
/// <para>
/// Das Muster ist immer gleich: erst in eine temporäre Datei schreiben, dann
/// die Zieldatei ersetzen. Ein Absturz mitten im Schreiben kann so die
/// bisherige Datei nicht zerstören.
/// </para>
/// <para>
/// <b>Windows-Falle:</b> Ein Virenscanner oder der Windows-Indexer kann die
/// Zieldatei für einen Moment sperren - dann schlägt das Ersetzen mit
/// „Access to the path is denied" fehl. Deshalb wird der Vorgang ein paar
/// Male wiederholt.
/// </para>
/// </remarks>
public static class AtomicFile
{
    /// <summary>
    /// Die Anzahl der Versuche.
    /// </summary>
    public const int MaxAttempts = 6;

    /// <summary>
    /// Die Wartezeit zwischen zwei Versuchen in Millisekunden.
    /// </summary>
    public const int RetryDelayMs = 25;

    /// <summary>
    /// Schreibt den Inhalt atomar in die Datei.
    /// </summary>
    /// <param name="path">Der Zielpfad.</param>
    /// <param name="content">Der Inhalt.</param>
    /// <param name="restrictPermissions">
    /// Soll der Dateimodus auf <c>0600</c> gesetzt werden (Unix)?
    /// </param>
    public static void WriteAllText(string path, string content, bool restrictPermissions = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);

        if (restrictPermissions)
        {
            RestrictPermissions(temporary);
        }

        Replace(temporary, path);
    }

    /// <summary>
    /// Ersetzt die Zieldatei durch die temporäre Datei.
    /// </summary>
    /// <param name="temporary">Die temporäre Datei.</param>
    /// <param name="path">Der Zielpfad.</param>
    private static void Replace(string temporary, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < MaxAttempts)
            {
                // Die Datei ist gerade gesperrt - kurz warten und erneut
                // versuchen.
                Thread.Sleep(RetryDelayMs);
            }
        }
    }

    /// <summary>
    /// Setzt den Dateimodus auf <c>0600</c> (nur unter Unix sinnvoll).
    /// </summary>
    private static void RestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Ohne die Rechte-Einschränkung läuft der Gateway weiter.
        }
    }
}
