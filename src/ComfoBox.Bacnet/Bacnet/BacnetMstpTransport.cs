using System.Collections.Concurrent;
using ComfoBox.Bacnet.Serialize;
using ComfoBox.Bacnet.Serial;
using Microsoft.Extensions.Logging;

namespace ComfoBox.Bacnet;

/// <summary>
/// BACnet-Transport über eine serielle MS/TP-Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Der Transport besitzt den MS/TP-Zustandsautomaten
/// (<see cref="MstpStateMachine"/>) und verwaltet dessen Lebenszyklus:
/// Port öffnen, Zustandsautomat starten, bei Portfehler schließen und mit
/// exponentiellem Backoff neu verbinden.
/// </para>
/// <para>
/// Der Zustandsautomat läuft auf einem eigenen Thread mit
/// <see cref="TaskCreationOptions.LongRunning"/>. Das ist für MS/TP zwingend,
/// weil der Token-Ring unabhängig vom Anwendungs-Takt bedient werden muss.
/// Das Legacy nutzte dafür einen Thread mit
/// <c>ThreadPriority.Highest</c> - das ist hier bewusst nicht übernommen
/// (Befund A23): Eine hohe Priorität beeinträchtigt den Rest des Systems.
/// </para>
/// </remarks>
public sealed class BacnetMstpTransport : IBacnetTransport
{
    /// <summary>Fehlercode, wenn ein Frame nicht rechtzeitig übertragen wurde.</summary>
    public const int TimeoutErrorCode = 110;

    private readonly ILogger<BacnetMstpTransport> _logger;
    private readonly ISerialPortFactory _portFactory;
    private readonly BacnetMstpOptions _options;
    private readonly ConcurrentQueue<MstpOutboundFrame> _sendQueue = new();
    private readonly HashSet<MstpOutboundFrame> _outstanding = [];
    private readonly object _outstandingLock = new();

    /// <summary>
    /// Signalisiert dem Verbindungs-Thread, dass er den Wartezeitraum
    /// abbrechen und den Port **sofort** neu öffnen soll.
    /// </summary>
    /// <remarks>
    /// Ohne dieses Signal wartet der Thread nach einem Portfehler bis zu
    /// 30 Sekunden (Backoff), bevor er den Port neu öffnet. Nach einem
    /// Portwechsel aus der Oberfläche heraus soll das aber sofort passieren.
    /// </remarks>
    private readonly SemaphoreSlim _reconnectSignal = new(0, 1);

    private CancellationTokenSource? _shutdown;
    private Task? _runner;
    private ISerialPort? _port;

    /// <summary>
    /// Erzeugt den Transport.
    /// </summary>
    /// <param name="portFactory">Erzeugt die serielle Schnittstelle.</param>
    /// <param name="options">Konfiguration.</param>
    /// <param name="logger">Logger.</param>
    public BacnetMstpTransport(
        ISerialPortFactory portFactory,
        BacnetMstpOptions options,
        ILogger<BacnetMstpTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(portFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _portFactory = portFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Wird ausgelöst, wenn ein Frame von der Gegenseite empfangen wurde.
    /// </summary>
    /// <remarks>
    /// Der Puffer enthält die Nutzdaten ohne MS/TP-Header (NPDU beginnt bei
    /// Index 0). Der Puffer gehört dem Aufrufer und wird nicht wiederverwendet.
    /// </remarks>
    public event EventHandler<BacnetFrameReceivedEventArgs>? FrameReceived;

    /// <inheritdoc />
    public BacnetAddressTypes Type => BacnetAddressTypes.MSTP;

    /// <inheritdoc />
    public int HeaderLength => MSTP.MSTP_HEADER_LENGTH;

    /// <inheritdoc />
    public int MaxBufferLength => 502;

    /// <inheritdoc />
    public BacnetMaxAdpu MaxAdpuLength => MSTP.MSTP_MAX_APDU;

    /// <inheritdoc />
    public byte MaxInfoFrames
    {
        get => _options.MaxInfoFrames;
        set => throw new NotSupportedException(
            "MaxInfoFrames ist zur Konfigurationszeit festgelegt und kann zur Laufzeit nicht geändert werden.");
    }

    /// <summary>
    /// Ist der Port aktuell geöffnet?
    /// </summary>
    public bool IsConnected => _port is { IsOpen: true };

    /// <inheritdoc />
    public BacnetAddress GetBroadcastAddress() =>
        new(BacnetAddressTypes.MSTP, 0xFFFF, new byte[] { 0xFF });

    /// <inheritdoc />
    public void Start()
    {
        if (_runner is not null)
        {
            throw new InvalidOperationException("Der Transport läuft bereits.");
        }

        _shutdown = new CancellationTokenSource();
        _runner = Task.Factory.StartNew(
            () => RunAsync(_shutdown.Token),
            _shutdown.Token,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();

        _logger.LogInformation(
            "MS/TP-Transport gestartet: Station {ThisStation}",
            _options.ThisStation);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_shutdown is null)
        {
            return;
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);

        if (_runner is not null)
        {
            var finished = await Task.WhenAny(_runner, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (finished != _runner)
            {
                // Der Zustandsautomat blockiert noch im Lesevorgang - Port
                // schließen, damit der Lesevorgang abbrechen kann.
                ClosePort();

                await Task.WhenAny(_runner, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            }
        }

        _runner = null;
        _shutdown.Dispose();
        _shutdown = null;

        _logger.LogInformation("MS/TP-Transport gestoppt.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Der Port wird geschlossen - die Schleife <see cref="RunAsync"/> öffnet
    /// ihn neu. Das beendet den Zustandsautomaten: der nächste Lesevorgang
    /// schlägt fehl, der Automat meldet einen Portfehler.
    /// </para>
    /// <para>
    /// Die Wartezeit des Backoffs wird dabei **abgebrochen**: der Port wird
    /// sofort neu geöffnet. Das ist wichtig, wenn der Portname aus der
    /// Oberfläche heraus gewechselt wird - der Benutzer soll nicht 30
    /// Sekunden warten.
    /// </para>
    /// </remarks>
    public void RequestReconnect()
    {
        _logger.LogInformation("Ein Neuaufbau der seriellen Schnittstelle wurde angefordert.");

        ClosePort();

        // Der Verbindungs-Thread darf den Backoff abbrechen.
        SignalReconnect();
    }

    /// <inheritdoc />
    public async Task<int> SendAsync(
        byte[] buffer,
        int offset,
        int dataLength,
        BacnetAddress address,
        bool waitForTransmission,
        int timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(address);

        if (address.adr is null || address.adr.Length == 0)
        {
            throw new BacnetException("Die Zieladresse enthält keine MAC-Adresse.");
        }

        // Der Frame-Typ richtet sich danach, ob eine Antwort erwartet wird.
        var function = NPDU.DecodeFunction(buffer, offset);
        var frameType = (function & BacnetNpduControls.ExpectingReply) == BacnetNpduControls.ExpectingReply
            ? BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_EXPECTING_REPLY
            : BacnetMstpFrameTypes.FRAME_TYPE_BACNET_DATA_NOT_EXPECTING_REPLY;

        // Vor den Nutzdaten wird Platz für den MS/TP-Header reserviert
        // (8 Byte), dahinter folgen die Nutzdaten und die zwei CRC-Bytes.
        var frame = new byte[dataLength + MSTP.MSTP_HEADER_LENGTH + 2];
        Array.Copy(buffer, offset, frame, MSTP.MSTP_HEADER_LENGTH, dataLength);

        var outbound = new MstpOutboundFrame(frameType, address.adr[0], frame, dataLength);

        lock (_outstandingLock)
        {
            _outstanding.Add(outbound);
        }

        _sendQueue.Enqueue(outbound);

        if (!waitForTransmission)
        {
            return dataLength;
        }

        try
        {
            await outbound.Sent.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return -TimeoutErrorCode;
        }
        finally
        {
            lock (_outstandingLock)
            {
                _outstanding.Remove(outbound);
            }
        }

        return dataLength;
    }

    /// <inheritdoc />
    public async Task<bool> WaitForAllTransmitsAsync(int timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);

        while (true)
        {
            MstpOutboundFrame[] pending;
            lock (_outstandingLock)
            {
                pending = _outstanding.ToArray();
            }

            if (pending.Length == 0)
            {
                return true;
            }

            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
            {
                return false;
            }

            var allSent = Task.WhenAll(pending.Select(f => f.Sent.Task));
            var finished = await Task.WhenAny(
                allSent,
                Task.Delay(remaining, cancellationToken)).ConfigureAwait(false);

            if (finished != allSent)
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();

        lock (_outstandingLock)
        {
            _outstanding.Clear();
        }

        _sendQueue.Clear();
        _reconnectSignal.Dispose();
    }

    /// <summary>
    /// Führt den Zustandsautomaten aus und verbindet ihn bei Portfehlern neu.
    /// </summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoff = _options.ReconnectDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var port = _portFactory.Create();
                _port = port;

                port.Open();
                _logger.LogInformation(
                    "Serielle Schnittstelle geöffnet: {Port} @ {BaudRate}",
                    port.PortName,
                    _options.Serial.BaudRate);

                backoff = _options.ReconnectDelay;

                var machine = new MstpStateMachine(
                    port,
                    _options,
                    _sendQueue,
                    OnFrameReceived,
                    _logger);

                // Der Zustandsautomat blockiert - er läuft auf dem
                // Long-Running-Thread dieses Transports.
                var faulted = machine.Run(cancellationToken);

                if (!faulted)
                {
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogError(
                    "Der MS/TP-Zustandsautomat wurde wegen eines Portfehlers beendet - Reconnect in {Delay}",
                    backoff);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogError(ex, "Fehler beim Betrieb der seriellen Schnittstelle - Reconnect in {Delay}", backoff);
            }
            finally
            {
                ClosePort();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Exponentieller Backoff: 5 s, 10 s, 20 s, ... maximal 30 s.
            // Ein angeforderter Neuaufbau (z. B. nach einem Portwechsel) bricht
            // die Wartezeit sofort ab.
            if (!await WaitForReconnectAsync(backoff, cancellationToken).ConfigureAwait(false))
            {
                break;
            }

            var doubled = TimeSpan.FromTicks(backoff.Ticks * 2);
            backoff = doubled > _options.MaxReconnectDelay ? _options.MaxReconnectDelay : doubled;
        }
    }

    /// <summary>
    /// Wartet auf das Ende des Backoffs - oder auf einen angeforderten
    /// Neuaufbau.
    /// </summary>
    /// <param name="delay">Die Wartezeit des Backoffs.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>
    /// <c>true</c>, wenn weitergearbeitet werden soll; <c>false</c>, wenn der
    /// Transport beendet wird.
    /// </returns>
    private async Task<bool> WaitForReconnectAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(delay);

        try
        {
            await _reconnectSignal.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Der Timeout ist der normale Fall (Backoff). Nur eine echte
            // Abbruch-Anforderung beendet den Transport.
            return !cancellationToken.IsCancellationRequested;
        }

        return !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Signalisiert dem Verbindungs-Thread den sofortigen Neuaufbau.
    /// </summary>
    private void SignalReconnect()
    {
        try
        {
            if (_reconnectSignal.CurrentCount == 0)
            {
                _reconnectSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            // Ein Signal ist bereits gesetzt - das genügt.
        }
    }

    /// <summary>
    /// Schließt den Port und gibt die Schnittstelle frei.
    /// </summary>
    private void ClosePort()
    {
        var port = Interlocked.Exchange(ref _port, null);
        if (port is null)
        {
            return;
        }

        try
        {
            port.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fehler beim Schließen der seriellen Schnittstelle.");
        }
        finally
        {
            port.Dispose();
        }
    }

    /// <summary>
    /// Leitet einen empfangenen Frame an die höhere Schicht weiter.
    /// </summary>
    private void OnFrameReceived(byte[] payload, BacnetAddress remoteAddress)
    {
        FrameReceived?.Invoke(this, new BacnetFrameReceivedEventArgs(payload, remoteAddress));
    }
}
