using System.Globalization;

namespace ComfoBox.Gateway.Hardware;

/// <summary>
/// Formuliert aus den Rechte-Angaben eine überprüfbare Fehlermeldung.
/// </summary>
/// <remarks>
/// <para>
/// „Keine Berechtigung" allein hilft niemandem: die Dateirechte und die
/// Device-Cgroup des Containers erzeugen denselben Fehler, brauchen aber
/// entgegengesetzte Lösungen. Deshalb nennt die Meldung die Fakten (Modus,
/// uid, Gruppen) und daraus abgeleitet **die** wahrscheinliche Ursache.
/// </para>
/// </remarks>
public static class SerialAccessMessages
{
    /// <summary>
    /// Die kurze Meldung für die Oberfläche.
    /// </summary>
    /// <param name="port">Der betroffene Port.</param>
    /// <returns>Die Meldung.</returns>
    public static string PermissionDenied(string port)
    {
        return $"Keine Berechtigung für den Port '{port}'. Der Gateway darf die Gerätedatei nicht öffnen.";
    }

    /// <summary>
    /// Die Einzelheiten zum Zugriffsfehler.
    /// </summary>
    /// <param name="port">Der betroffene Port.</param>
    /// <param name="access">Die ermittelten Rechte.</param>
    /// <returns>Die Zeilen für die Anzeige.</returns>
    public static IReadOnlyList<string> PermissionDeniedDetails(string port, SerialPortAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        var details = new List<string>(5);

        if (!access.Exists)
        {
            details.Add($"Die Gerätedatei '{port}' gibt es in diesem Container nicht.");
            details.Add("Der Adapter ist entweder nicht eingesteckt oder '/dev' wurde nicht durchgereicht.");
            return details;
        }

        details.Add(access.ModeText is null
            ? $"Gerätedatei: {port}"
            : $"Gerätedatei: {port} · {access.ModeText} · Modus {access.Mode}");

        details.Add(access.ProcessGroups.Count == 0
            ? $"Gateway-Prozess: uid {access.Uid?.ToString(CultureInfo.InvariantCulture) ?? "?"} · Gruppen nicht lesbar"
            : $"Gateway-Prozess: uid {access.Uid?.ToString(CultureInfo.InvariantCulture) ?? "?"} · Gruppen "
              + string.Join(", ", access.ProcessGroups.Select(gid => gid.ToString(CultureInfo.InvariantCulture))));

        if (!access.HasDialoutGroup)
        {
            details.Add(
                "Ursache: Dem Gateway fehlt die Gruppe der Gerätedatei. Die Gruppe 20 (dialout) ist "
                + "nicht dabei - fast immer fehlt 'group_add' im Container.");
            details.Add(
                "Lösung: In der Compose-Datei 'group_add: [\"<GID von dialout>\"]' setzen (Raspberry Pi OS "
                + "und Debian: 20) und den Container neu erzeugen. Beim Installationsskript macht das "
                + "'install.sh' von selbst.");
        }
        else
        {
            details.Add(
                "Ursache: Die Gruppe 20 (dialout) ist vorhanden - dann blockiert die Device-Cgroup des "
                + "Containers. '/dev:/dev' macht die Gerätedatei nur sichtbar, nicht benutzbar.");
            details.Add(
                "Lösung: In der Compose-Datei 'device_cgroup_rules' mit 'c 188:* rwm' (ttyUSB) und "
                + "'c 166:* rwm' (ttyACM) setzen und den Container neu erzeugen.");
        }

        details.Add(
            "Prüfen auf dem Host: 'ls -l " + port + "' zeigt Besitzer und Gruppe, "
            + "'getent group dialout' die GID. Das Skript 'serial-port-check.sh' prüft alles auf einmal.");

        return details;
    }

    /// <summary>
    /// Die Einzelheiten zu einem Port, der von einem anderen Prozess gehalten wird.
    /// </summary>
    /// <param name="port">Der betroffene Port.</param>
    /// <returns>Die Zeilen für die Anzeige.</returns>
    public static IReadOnlyList<string> BusyDetails(string port)
    {
        return
        [
            $"Der Port '{port}' ist vorhanden, aber belegt.",
            "Meist hält ihn ein anderer Prozess (z. B. ein zweiter Gateway oder ein Terminal-Programm).",
            "Prüfen auf dem Host: 'sudo fuser -v " + port + "' oder 'sudo lsof " + port + "'.",
        ];
    }
}
