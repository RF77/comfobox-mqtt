using ComfoBox.Bacnet;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Die Bus-Analyse: sie beobachtet den MS/TP-Token-Umlauf, **ohne** die
/// ComfoBox zu verändern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum das nicht im Zustandsautomaten passiert:</b> der MS/TP-Token hat
/// nur wenige Millisekunden Zeit (95 ms bis zur Übernahme). Ein Log-Eintrag
/// an dieser Stelle kostet I/O - der Token ginge verloren, der Master suchte
/// einen Nachfolger und der Bus käme zum Erliegen. Deshalb gilt:
/// </para>
/// <list type="bullet">
/// <item>Der Zustandsautomat **sammelt** die Ereignisse in einem
/// allokationsfreien Ringpuffer (<see cref="MstpEventRecorder"/>).</item>
/// <item>Dieser Dienst **wertet** sie aus - einmal pro Sekunde, auf einem
/// eigenen Thread. Erst hier wird geloggt.</item>
/// </list>
/// <para>
/// <b>Was die Analyse tut:</b> sie liest. Sie stellt keine Anfragen an die
/// ComfoBox und schreibt keine Werte. Einzige Ausnahme ist das normale
/// MS/TP-Protokollverhalten: hat der Master den Token verloren, erzeugt er
/// selbst einen - das ist Protokoll, kein Gerätezugriff.
/// </para>
/// </remarks>
public sealed class BacnetBusAnalyzer : BackgroundService
{
    /// <summary>
    /// Die kürzeste sinnvolle Beobachtungsdauer in Sekunden.
    /// </summary>
    public const int MinimumDurationSeconds = 3;

    /// <summary>
    /// Die längste zulässige Beobachtungsdauer in Sekunden.
    /// </summary>
    /// <remarks>
    /// Die Beobachtung darf den Bus nicht blockieren: sie läuft im
    /// Hintergrund, aber ein offener Beobachtungsmodus über Stunden wäre
    /// eine Fehlbedienung.
    /// </remarks>
    public const int MaximumDurationSeconds = 300;

    private readonly MstpEventRecorder _recorder;
    private readonly BacnetPollingService? _polling;
    private readonly SerialPortSettings _settings;
    private readonly BacnetOptions _options;
    private readonly ILogger<BacnetBusAnalyzer> _logger;

    private DateTimeOffset _observationEndsAt;
    private long _lastLoggedTotal;

    /// <summary>
    /// Erzeugt den Analyzer.
    /// </summary>
    /// <param name="recorder">Der Ringpuffer der Bus-Ereignisse.</param>
    /// <param name="polling">Der Polling-Dienst (kann <c>null</c> sein).</param>
    /// <param name="settings">Die aktuellen Parameter der Schnittstelle.</param>
    /// <param name="options">Die BACnet-Einstellungen.</param>
    /// <param name="logger">Logger.</param>
    public BacnetBusAnalyzer(
        MstpEventRecorder recorder,
        BacnetPollingService? polling,
        SerialPortSettings settings,
        IOptions<BacnetOptions> options,
        ILogger<BacnetBusAnalyzer> logger)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _recorder = recorder;
        _polling = polling;
        _settings = settings;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Läuft eine Beobachtung?
    /// </summary>
    public bool IsObserving => DateTimeOffset.UtcNow < _observationEndsAt;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DrainEvents();
                EndExpiredObservation();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Die Analyse darf den Gateway nie beeinträchtigen.
                _logger.LogWarning(ex, "Die Bus-Analyse ist fehlgeschlagen.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Startet eine neue Beobachtung.
    /// </summary>
    /// <param name="durationSeconds">Die Dauer in Sekunden.</param>
    /// <param name="pausePolling">Soll das Polling während der Beobachtung pausieren?</param>
    /// <exception cref="ArgumentOutOfRangeException">Die Dauer liegt außerhalb der Grenzen.</exception>
    public void Start(int durationSeconds, bool pausePolling)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationSeconds, MinimumDurationSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(durationSeconds, MaximumDurationSeconds);

        _recorder.Reset();
        _lastLoggedTotal = 0;

        _observationEndsAt = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

        if (pausePolling)
        {
            _polling?.SetPaused(true);

            _logger.LogInformation(
                "Bus-Analyse gestartet ({Duration} s). Das Polling pausiert - der Token läuft ungestört.",
                durationSeconds);
        }
        else
        {
            _logger.LogInformation(
                "Bus-Analyse gestartet ({Duration} s). Das Polling läuft weiter.",
                durationSeconds);
        }
    }

    /// <summary>
    /// Beendet die Beobachtung (das Polling läuft wieder an).
    /// </summary>
    public void Stop()
    {
        _observationEndsAt = DateTimeOffset.MinValue;
        _polling?.SetPaused(false);
    }

    /// <summary>
    /// Liefert die aktuelle Auswertung.
    /// </summary>
    /// <returns>Die Auswertung.</returns>
    public BacnetAnalysis Analysis()
    {
        var snapshot = _recorder.Snapshot();

        return new BacnetAnalysis(
            snapshot.StartedAt,
            snapshot.DurationSeconds,
            IsObserving,
            _polling?.IsPaused ?? false,
            _settings.PortName,
            _settings.BaudRate,
            _options.ThisStation,
            _settings.PortName.Length > 0,
            snapshot.TotalEvents,
            snapshot.Counters,
            snapshot.StationsSeen,
            snapshot.Events,
            Verdict(snapshot));
    }

    /// <summary>
    /// Formuliert das Ergebnis in einem Satz.
    /// </summary>
    /// <param name="snapshot">Die Beobachtung.</param>
    /// <returns>Die Bewertung (deutsch).</returns>
    private static string Verdict(MstpEventSnapshot snapshot)
    {
        var counters = snapshot.Counters;

        var tokensReceived = counters.GetValueOrDefault(MstpEventKind.TokenReceived);
        var tokensPassed = counters.GetValueOrDefault(MstpEventKind.TokenPassed);
        var tokensUsed = counters.GetValueOrDefault(MstpEventKind.TokenUsed);
        var dataReceived = counters.GetValueOrDefault(MstpEventKind.DataReceived);
        var soleMaster = counters.GetValueOrDefault(MstpEventKind.SoleMaster);
        var framesReceived = counters.GetValueOrDefault(MstpEventKind.FrameReceived);

        if (snapshot.TotalEvents == 0)
        {
            return "Der Gateway hat auf dem Bus nichts empfangen. "
                   + "Prüfen Sie Port, Kabel und Baudrate (die ComfoBox muss auf 38400 stehen).";
        }

        if (framesReceived == 0)
        {
            return "Der Gateway sendet, erhält aber keine Antwort - die ComfoBox antwortet nicht.";
        }

        if (soleMaster > 0 && tokensReceived == 0)
        {
            return "Der Gateway hält den Token allein (Sole Master): "
                   + "kein weiterer Knoten hat sich auf dem Bus gemeldet.";
        }

        if (tokensReceived > 0 && tokensPassed > 0)
        {
            return $"Der Token läuft im Umlauf ({tokensReceived} Übernahmen, {tokensPassed} Weitergaben). "
                   + "Der MS/TP-Ring funktioniert.";
        }

        if (tokensUsed > 0)
        {
            return $"Der Gateway hat den Token {tokensUsed}-mal für eigene Abfragen benutzt. "
                   + "Ein Token-Umlauf zwischen mehreren Stationen wurde nicht beobachtet.";
        }

        if (dataReceived > 0)
        {
            return $"Es sind {dataReceived} BACnet-Nutzdaten angekommen, aber kein Token-Umlauf.";
        }

        return "Es kommen Frames an, aber kein Token wird übergeben - der Ring läuft nicht.";
    }

    /// <summary>
    /// Schreibt die neuen Ereignisse ins Log - **außerhalb** des Hot-Paths.
    /// </summary>
    private void DrainEvents()
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            // Ohne Debug-Level gibt es nichts zu schreiben. Der Zähler
            // wird trotzdem nachgezogen, damit kein Ereignis nachkommt.
            _lastLoggedTotal = _recorder.Snapshot(1).TotalEvents;
            return;
        }

        var snapshot = _recorder.Snapshot(200);
        var first = snapshot.TotalEvents - snapshot.Events.Count;

        for (var i = 0; i < snapshot.Events.Count; i++)
        {
            var index = first + i;

            if (index < _lastLoggedTotal)
            {
                continue;
            }

            var evt = snapshot.Events[i];

            _logger.LogDebug(
                "MS/TP: {Kind} von/zu Station {Station} (Frame {FrameType}) um {Timestamp:HH:mm:ss.fff}",
                evt.Kind,
                evt.Station,
                evt.FrameType,
                evt.TimestampUtc.ToLocalTime());
        }

        _lastLoggedTotal = snapshot.TotalEvents;
    }

    /// <summary>
    /// Beendet eine abgelaufene Beobachtung.
    /// </summary>
    private void EndExpiredObservation()
    {
        if (_observationEndsAt == DateTimeOffset.MinValue || IsObserving)
        {
            return;
        }

        Stop();

        _logger.LogInformation("Die Bus-Analyse ist beendet - das Polling läuft wieder.");
    }
}

/// <summary>
/// Die Auswertung einer Bus-Analyse.
/// </summary>
/// <param name="StartedAt">Der Beginn der Beobachtung.</param>
/// <param name="DurationSeconds">Die Dauer der Beobachtung in Sekunden.</param>
/// <param name="Observing">Läuft gerade eine Beobachtung?</param>
/// <param name="PollingPaused">Ist das Polling pausiert?</param>
/// <param name="Port">Der konfigurierte serielle Port.</param>
/// <param name="Baudrate">Die eingestellte Baudrate.</param>
/// <param name="ThisStation">Die MSTP-Adresse des Gateways.</param>
/// <param name="PortConfigured">Ist überhaupt ein Port eingestellt?</param>
/// <param name="TotalEvents">Die Anzahl der Ereignisse.</param>
/// <param name="Counters">Die Zähler je Ereignisart.</param>
/// <param name="StationsSeen">Die gesehenen Stationen.</param>
/// <param name="Events">Die Ereignisse (älteste zuerst).</param>
/// <param name="Verdict">Die Bewertung in einem Satz.</param>
public sealed record BacnetAnalysis(
    DateTimeOffset StartedAt,
    double DurationSeconds,
    bool Observing,
    bool PollingPaused,
    string Port,
    int Baudrate,
    int ThisStation,
    bool PortConfigured,
    long TotalEvents,
    IReadOnlyDictionary<MstpEventKind, long> Counters,
    IReadOnlyList<byte> StationsSeen,
    IReadOnlyList<MstpEvent> Events,
    string Verdict);
