using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using MQTTnet;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Bringt einen Wechsel des Experten-Modus zur Laufzeit auf MQTT und in die
/// Home-Assistant-Discovery.
/// </summary>
/// <remarks>
/// <para>
/// Bis Aufgabe 10.13 lasen <see cref="MqttTopicMap"/> und
/// <see cref="HomeAssistantDiscovery"/> den Experten-Modus **beim Start**. Wer
/// den Schalter in der Oberfläche umlegte, sah unter MQTT und in Home Assistant
/// keine Änderung - die <c>/Set</c>-Topics der Experten-Items fehlten bis zum
/// Neustart.
/// </para>
/// <para>
/// Der Koordinator hört auf <see cref="GatewayRuntimeSettings.ExpertModeChanged"/>
/// und arbeitet die drei betroffenen Stellen in dieser Reihenfolge ab:
/// </para>
/// <list type="number">
/// <item>Topic-Zuordnung (<see cref="MqttTopicMap.UpdateExpertMode"/>)</item>
/// <item>Abonnements (<see cref="MqttSubscriber.RefreshSubscriptionsAsync"/>)</item>
/// <item>Discovery (<see cref="HomeAssistantDiscovery.RepublishAsync"/>)</item>
/// </list>
/// <para>
/// Jeder Schritt läuft in einem eigenen <c>try/catch</c>: ein Fehler beim
/// Abbestellen darf die Discovery nicht verhindern. Gemeldet wird über
/// <see cref="IErrorCollector"/> (Quelle <c>ExpertMode</c>, Code
/// <c>expert_mode_switch_failed</c>) - damit landet der Fehler wie jeder andere
/// im Log, unter <c>ComfoBox/Status/Errors</c> und in der Oberfläche.
/// </para>
/// <para>
/// <b>Ohne MQTT-Verbindung</b> passiert nichts Aufwendiges: die Topic-Zuordnung
/// wird trotzdem nachgezogen, damit der nächste Verbindungsaufbau den richtigen
/// Satz abonniert. Ein Fehler entsteht dabei nicht - „keine Verbindung" ist ein
/// gültiger Zustand (<c>Mqtt:Enabled=false</c>, Broker offline).
/// </para>
/// <para>
/// Der Typ ist ein <see cref="IHostedService"/> ohne eigene Arbeit: der Host
/// erzeugt ihn beim Start, damit das Ereignis überhaupt abonniert ist. Ein
/// reines Singleton würde erst entstehen, wenn jemand es anfordert - und das
/// tut niemand.
/// </para>
/// </remarks>
public sealed class ExpertModeCoordinator : IHostedService, IDisposable
{
    /// <summary>
    /// Die Quelle eines Fehlers beim Umschalten.
    /// </summary>
    public const string ErrorSource = "ExpertMode";

    /// <summary>
    /// Der Code eines fehlgeschlagenen Umschaltens.
    /// </summary>
    public const string ErrorCode = "expert_mode_switch_failed";

    private readonly GatewayRuntimeSettings _settings;
    private readonly MqttTopicMap _topics;
    private readonly MqttSubscriber _subscriber;
    private readonly HomeAssistantDiscovery _discovery;
    private readonly IMqttClientProvider _clients;
    private readonly IErrorCollector _errors;
    private readonly ILogger<ExpertModeCoordinator> _logger;

    // Ein Wechsel darf nicht mit dem nächsten verschränkt laufen: sonst könnte
    // ein älterer Lauf die Zuordnung auf den vorherigen Modus zurückdrehen.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();

    private int _disposed;

    /// <summary>
    /// Erzeugt den Koordinator und abonniert den Wechsel.
    /// </summary>
    /// <param name="settings">Die Laufzeit-Einstellungen (Quelle des Ereignisses).</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="subscriber">Der Subscriber der Schreib-Topics.</param>
    /// <param name="discovery">Die Home-Assistant-Discovery.</param>
    /// <param name="clients">Die verbundenen MQTT-Clients.</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="logger">Logger.</param>
    public ExpertModeCoordinator(
        GatewayRuntimeSettings settings,
        MqttTopicMap topics,
        MqttSubscriber subscriber,
        HomeAssistantDiscovery discovery,
        IMqttClientProvider clients,
        IErrorCollector errors,
        ILogger<ExpertModeCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings;
        _topics = topics;
        _subscriber = subscriber;
        _discovery = discovery;
        _clients = clients;
        _errors = errors;
        _logger = logger;

        _settings.ExpertModeChanged += OnExpertModeChanged;
    }

    /// <summary>
    /// Der Koordinator hat keine eigene Arbeit - das Abonnement entsteht im
    /// Konstruktor.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein abgeschlossener Task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Beendet den Koordinator nicht - das Ereignis bleibt bis
    /// <see cref="Dispose"/> abonniert.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein abgeschlossener Task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Wendet den **aktuellen** Experten-Modus auf Topic-Zuordnung,
    /// Abonnements und Discovery an.
    /// </summary>
    /// <returns>Ein Task, der alle Schritte abgeschlossen hat.</returns>
    /// <remarks>
    /// <para>
    /// Der Wert wird aus <see cref="GatewayRuntimeSettings.ExpertMode"/> gelesen -
    /// nicht als Parameter übergeben. So kann ein schnell zweimal umgelegter
    /// Schalter nicht in der falschen Reihenfolge ankommen: beide Läufe sehen
    /// denselben, bereits gesetzten Wert.
    /// </para>
    /// <para>
    /// Die Methode wirft nicht: jeder Schritt fängt seine Ausnahme selbst und
    /// meldet sie über <see cref="IErrorCollector"/>. Dadurch kann sie
    /// gefahrlos aus einem Ereignis heraus als Task gestartet werden.
    /// </para>
    /// </remarks>
    public async Task ApplyAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            await _gate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            await ApplyStepsAsync(_settings.ExpertMode).ConfigureAwait(false);
        }
        finally
        {
            ReleaseGate();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _settings.ExpertModeChanged -= OnExpertModeChanged;

        _shutdown.Cancel();
        _shutdown.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// Führt die drei Schritte aus - jeder für sich abgesichert.
    /// </summary>
    /// <param name="expertModeEnabled">Ist der Experten-Modus aktiv?</param>
    /// <returns>Ein Task, der alle Schritte abgeschlossen hat.</returns>
    private async Task ApplyStepsAsync(bool expertModeEnabled)
    {
        // 1. Die Zuordnung gilt auch ohne Verbindung: der nächste Connect
        //    abonniert daraus.
        await RunStepAsync(
            "Topic-Zuordnung",
            expertModeEnabled,
            _ =>
            {
                _topics.UpdateExpertMode(expertModeEnabled);
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        var clients = _clients.ConnectedClients;
        if (clients.Count == 0)
        {
            _logger.LogInformation(
                "Experten-Modus ist jetzt {State}; keine MQTT-Verbindung - Abonnements und Discovery folgen beim nächsten Verbindungsaufbau.",
                expertModeEnabled ? "an" : "aus");
            return;
        }

        // 2. Erst ab- und wieder anbestellen, damit die /Set-Topics der
        //    Experten-Items wirklich (nicht) mehr ankommen.
        await RunStepAsync(
            "Abonnements",
            expertModeEnabled,
            ct => _subscriber.RefreshSubscriptionsAsync(clients, ct)).ConfigureAwait(false);

        // 3. Danach die Discovery: Home Assistant bekommt number/select statt
        //    sensor (oder umgekehrt).
        await RunStepAsync(
            "Home-Assistant-Discovery",
            expertModeEnabled,
            ct => _discovery.RepublishAsync(clients, ct)).ConfigureAwait(false);
    }

    /// <summary>
    /// Führt einen Schritt aus und meldet einen Fehler, ohne zu werfen.
    /// </summary>
    /// <param name="step">Der Name des Schritts (für Log und Fehlermeldung).</param>
    /// <param name="expertModeEnabled">Der Zielzustand (für die Meldung).</param>
    /// <param name="action">Die Arbeit.</param>
    /// <returns>Ein Task, der den Schritt abgeschlossen hat.</returns>
    private async Task RunStepAsync(
        string step,
        bool expertModeEnabled,
        Func<CancellationToken, Task> action)
    {
        try
        {
            await action(_shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Der Gateway fährt herunter - der nächste Start liest den
            // gespeicherten Modus ohnehin aus der Konfiguration.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Der Wechsel des Experten-Modus ist bei {Step} fehlgeschlagen.",
                step);

            _errors.Report(
                ErrorSource,
                ErrorCode,
                $"Der Wechsel des Experten-Modus ({(expertModeEnabled ? "an" : "aus")}) "
                + $"ist bei \"{step}\" fehlgeschlagen: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gibt die Sperre frei - auch wenn der Koordinator gerade entsorgt wird.
    /// </summary>
    private void ReleaseGate()
    {
        try
        {
            _gate.Release();
        }
        catch (ObjectDisposedException)
        {
            // Der Gateway fährt herunter.
        }
    }

    /// <summary>
    /// Reagiert auf den Wechsel - ohne <c>async void</c>.
    /// </summary>
    /// <param name="sender">Die Einstellungen.</param>
    /// <param name="expertModeEnabled">Der neue Zustand.</param>
    /// <remarks>
    /// Das Ereignis kommt aus dem Setter, also aus dem HTTP-Request der
    /// Oberfläche. Es darf dort nicht warten - die Discovery für 184 Items
    /// würde die Antwort um Sekunden verzögern. <see cref="ApplyAsync"/> fängt
    /// seine Fehler selbst, der Task kann deshalb verworfen werden.
    /// </remarks>
    private void OnExpertModeChanged(object? sender, bool expertModeEnabled)
        => _ = ApplyAsync();
}
