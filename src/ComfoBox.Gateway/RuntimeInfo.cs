namespace ComfoBox.Gateway;

/// <summary>
/// Die Laufzeitinformationen des Gateways (Startzeitpunkt, Uptime).
/// </summary>
public sealed class RuntimeInfo
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Erzeugt die Laufzeitinformationen.
    /// </summary>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    public RuntimeInfo(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        StartedAt = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Der Zeitpunkt des Starts.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Die Laufzeit in Sekunden.
    /// </summary>
    public long UptimeSeconds
    {
        get
        {
            var seconds = (_timeProvider.GetUtcNow() - StartedAt).TotalSeconds;
            return seconds <= 0 ? 0 : (long)seconds;
        }
    }
}
