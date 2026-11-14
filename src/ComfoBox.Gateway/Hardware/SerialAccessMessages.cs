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
    /// Der Fehlercode zu einer fehlgeschlagenen Portöffnung.
    /// </summary>
    /// <param name="access">Die ermittelten Rechte.</param>
    /// <returns><c>busy</c> oder <c>permission_denied</c>.</returns>
    /// <remarks>
    /// <para>
    /// Unter Linux meldet .NET für **zwei** verschiedene Lagen dieselbe
    /// <c>UnauthorizedAccessException</c>: fehlende Rechte und einen Port,
    /// den ein anderer Prozess bereits exklusiv geöffnet hat (das ist der
    /// Normalfall, wenn der Gateway läuft und ein zweiter Test ihn öffnen
    /// will).
    /// </para>
    /// <para>
    /// Die Gruppen sagen, welcher der beiden Fälle vorliegt: liegt die
    /// Gruppe der Gerätedatei vor, können die Rechte nicht die Ursache sein.
    /// </para>
    /// </remarks>
    public static string DeniedHint(SerialPortAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        return access.HasRequiredGroup ? "busy" : "permission_denied";
    }

    /// <summary>
    /// Die überprüfbaren Fakten zu einer Gerätedatei - ohne Deutung.
    /// </summary>
    /// <param name="port">Der betroffene Port.</param>
    /// <param name="access">Die ermittelten Rechte.</param>
    /// <returns>Die Zeilen für die Anzeige.</returns>
    /// <remarks>
    /// Diese Angaben sind auch dann nützlich, wenn der Fehler **nicht** an
    /// den Rechten liegt: sie zeigen, wer die Datei öffnen darf und in
    /// welchen Gruppen der Gateway läuft. Deshalb enthalten sie - anders als
    /// <see cref="PermissionDeniedDetails"/> - keine Ursachen-Deutung.
    /// </remarks>
    public static IReadOnlyList<string> AccessFacts(string port, SerialPortAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        return access.Exists
            ? [DescribeDevice(port, access), DescribeProcess(access)]
            : [$"Die Gerätedatei '{port}' gibt es hier nicht."];
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

        if (!access.Exists)
        {
            return
            [
                $"Die Gerätedatei '{port}' gibt es hier nicht.",
                "Der Adapter ist entweder nicht eingesteckt oder '/dev' wurde nicht durchgereicht.",
            ];
        }

        var details = new List<string>(5)
        {
            DescribeDevice(port, access),
            DescribeProcess(access),
        };

        if (access.HasRequiredGroup)
        {
            details.Add(
                "Ursache: Die Rechte sind vorhanden - damit liegt es nicht an der Gruppe, sondern an "
                + "einem anderen Halter des Ports. Unter Linux meldet .NET dieselbe Ausnahme, wenn ein "
                + "Prozess den Port bereits exklusiv geöffnet hat.");
            details.Add(
                $"Lösung: Den Halter suchen ('sudo fuser -v {port}' oder 'sudo lsof {port}'). Läuft ein "
                + "zweiter Gateway - etwa der Container und ein Entwicklungsstart parallel -, den "
                + "überzähligen beenden.");
        }
        else if (access.IsContainer)
        {
            details.Add(
                $"Ursache: Dem Gateway fehlt die Gruppe '{access.RequiredGroupName}' "
                + $"({access.RequiredGid.ToString(CultureInfo.InvariantCulture)}) der Gerätedatei.");
            details.Add(
                $"Lösung: In der Compose-Datei 'group_add: [\"{access.RequiredGid.ToString(CultureInfo.InvariantCulture)}\"]' setzen "
                + "und den Container neu erzeugen. 'install.sh' trägt den Wert von selbst ein.");
        }
        else
        {
            details.Add(
                $"Ursache: Dem Benutzer fehlt die Gruppe '{access.RequiredGroupName}' "
                + $"({access.RequiredGid.ToString(CultureInfo.InvariantCulture)}) der Gerätedatei.");
            details.Add(
                $"Lösung: 'sudo usermod -aG {access.RequiredGroupName} {access.ProcessUser ?? "<benutzer>"}' "
                + "ausführen und den Gateway danach neu starten - Gruppen gelten nur für neue Sitzungen.");
        }

        details.Add(
            $"Prüfen auf dem Host: 'ls -l {port}' zeigt Besitzer und Gruppe, "
            + $"'getent group {access.RequiredGroupName}' die GID. "
            + "Das Skript 'serial-port-check.sh' prüft alles auf einmal.");

        return details;
    }

    /// <summary>
    /// Die Zeile zur Gerätedatei.
    /// </summary>
    private static string DescribeDevice(string port, SerialPortAccess access)
    {
        var text = $"Gerätedatei: {port}";

        if (access.ModeText is not null)
        {
            text += $" · {access.ModeText} · Modus {access.Mode}";
        }

        if (access.DeviceGid is not null)
        {
            var gid = access.DeviceGid.Value.ToString(CultureInfo.InvariantCulture);
            var group = access.DeviceGroupName is null ? gid : $"{access.DeviceGroupName} ({gid})";

            text += access.DeviceUid is null
                ? $" · Gruppe {group}"
                : $" · Besitzer uid {access.DeviceUid.Value.ToString(CultureInfo.InvariantCulture)} · Gruppe {group}";
        }

        return text;
    }

    /// <summary>
    /// Die Zeile zum Gateway-Prozess.
    /// </summary>
    private static string DescribeProcess(SerialPortAccess access)
    {
        var uid = access.Uid?.ToString(CultureInfo.InvariantCulture) ?? "?";
        var user = access.ProcessUser is null ? "Gateway-Prozess" : $"Gateway-Prozess: {access.ProcessUser}";

        if (access.ProcessGroups.Count == 0)
        {
            return $"{user} (uid {uid}) · Gruppen nicht lesbar";
        }

        var groups = string.Join(", ", access.ProcessGroups.Select(gid => gid.ToString(CultureInfo.InvariantCulture)));
        var required = access.RequiredGid.ToString(CultureInfo.InvariantCulture);

        return access.HasRequiredGroup
            ? $"{user} (uid {uid}) · Gruppen {groups} · Gruppe {required} ist dabei"
            : $"{user} (uid {uid}) · Gruppen {groups} · Gruppe {required} fehlt";
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
