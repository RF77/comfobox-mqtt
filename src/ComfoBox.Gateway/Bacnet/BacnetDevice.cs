using System.Globalization;
using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serialize;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Das Ergebnis eines Schreibvorgangs.
/// </summary>
public enum WriteOutcome
{
    /// <summary>Der Wert wurde geschrieben.</summary>
    Ok,

    /// <summary>Das Item ist nicht schreibbar.</summary>
    ReadOnly,

    /// <summary>Das Item erfordert den Expert-Modus.</summary>
    ExpertModeRequired,

    /// <summary>Der Wert liegt außerhalb der Grenzen.</summary>
    OutOfRange,

    /// <summary>Der Wert ist ungültig.</summary>
    InvalidType,

    /// <summary>Das 24-h-Limit ist erreicht.</summary>
    LimitExceeded,

    /// <summary>Der BACnet-Bus hat den Schreibvorgang abgelehnt.</summary>
    BusError,
}

/// <summary>
/// Der Zugriff auf die ComfoBox über BACnet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Serialisierung:</b> Der MS/TP-Bus kann nur eine Anfrage gleichzeitig
/// bedienen. Alle Lese- und Schreibzugriffe laufen deshalb über genau ein
/// <see cref="SemaphoreSlim"/>.
/// </para>
/// <para>
/// <b>Robustheit:</b> Ein Lesefehler beendet den Zyklus nicht - der letzte
/// bekannte Wert bleibt im Cache und wird als <c>stale</c> markiert.
/// </para>
/// <para>
/// <b>Schreibschutz:</b> Der 24-h-Zähler wird **hier** erhöht, nicht im
/// Aufrufer. Damit kann kein Pfad ihn umgehen.
/// </para>
/// </remarks>
public sealed class BacnetDevice : IDisposable
{
    private readonly IBacnetClient _client;
    private readonly IValueCache _cache;
    private readonly IErrorCollector _errors;
    private readonly IWriteCounter _writeCounter;
    private readonly ILogger<BacnetDevice> _logger;
    private readonly SemaphoreSlim _busLock = new(1, 1);
    private readonly BacnetRuntimeSettings _runtime;
    private readonly GatewayRuntimeSettings _gateway;

    /// <summary>
    /// Die Items, für die bereits ein Hinweis „Wert außerhalb der
    /// Katalog-Grenzen" geloggt wurde.
    /// </summary>
    /// <remarks>
    /// Der Hinweis erscheint **einmal je Item und Prozesslauf**: sonst
    /// entstünde bei jedem Poll-Zyklus eine neue Zeile und der Ringpuffer
    /// (1000 Einträge) wäre innerhalb von Minuten unbrauchbar.
    /// </remarks>
    private readonly HashSet<string> _rangeWarned = new(StringComparer.Ordinal);
    private readonly object _rangeWarnedLock = new();

    /// <summary>
    /// Erzeugt das Gerät.
    /// </summary>
    /// <param name="client">Der BACnet-Client.</param>
    /// <param name="cache">Der Wert-Cache.</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="writeCounter">Der Schreibzähler.</param>
    /// <param name="bacnetOptions">Die BACnet-Einstellungen.</param>
    /// <param name="gatewayOptions">Die Grundeinstellungen.</param>
    /// <param name="runtime">Die zur Laufzeit änderbaren BACnet-Parameter.</param>
    /// <param name="gatewayRuntime">Der zur Laufzeit umschaltbare Experten-Modus.</param>
    /// <param name="logger">Logger.</param>
    public BacnetDevice(
        IBacnetClient client,
        IValueCache cache,
        IErrorCollector errors,
        IWriteCounter writeCounter,
        IOptions<BacnetOptions> bacnetOptions,
        IOptions<GatewayOptions> gatewayOptions,
        BacnetRuntimeSettings runtime,
        GatewayRuntimeSettings gatewayRuntime,
        ILogger<BacnetDevice> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(writeCounter);
        ArgumentNullException.ThrowIfNull(bacnetOptions);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(gatewayRuntime);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _cache = cache;
        _errors = errors;
        _writeCounter = writeCounter;
        _runtime = runtime;
        _gateway = gatewayRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Die Adresse der ComfoBox - **bei jedem Zugriff neu** aus den
    /// Laufzeit-Einstellungen gelesen (eine geänderte Geräte-ID wirkt sofort).
    /// </summary>
    private BacnetAddress DeviceAddress => _runtime.DeviceAddress;

    /// <summary>
    /// Das Zeitfenster je Item (ebenfalls zur Laufzeit änderbar).
    /// </summary>
    private TimeSpan ItemTimeout => _runtime.ItemTimeout;

    /// <summary>
    /// Ist die ComfoBox erreichbar?
    /// </summary>
    /// <remarks>
    /// Der Zustand wird aus dem letzten Buszugriff abgeleitet: ein
    /// erfolgreicher Lese- oder Schreibvorgang setzt ihn auf <c>true</c>,
    /// ein Fehler auf <c>false</c>. Die API zeigt ihn in
    /// <c>/api/status</c> an.
    /// </remarks>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Der letzte Fehler (oder <c>null</c>).
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Liest die angegebenen Items - einer nach dem anderen.
    /// </summary>
    /// <param name="items">Die Items in Lesereihenfolge.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Die Items, die erfolgreich gelesen wurden.</returns>
    public async Task<IReadOnlyList<ItemDefinition>> ReadItemsAsync(
        IReadOnlyList<ItemDefinition> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        var read = new List<ItemDefinition>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await ReadItemAsync(item, cancellationToken).ConfigureAwait(false))
            {
                read.Add(item);
            }
        }

        return read;
    }

    /// <summary>
    /// Schreibt einen Wert.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="value">Der zu schreibende Wert.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Das Ergebnis des Schreibvorgangs.</returns>
    public async Task<WriteOutcome> WriteAsync(ItemDefinition item, double value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsReadOnly)
        {
            return WriteOutcome.ReadOnly;
        }

        if (item.RequiresExpertMode && !_gateway.ExpertMode)
        {
            _logger.LogWarning("Schreibvorgang für {Item} abgelehnt: Expert-Modus ist nicht aktiv.", item.Id);
            return WriteOutcome.ExpertModeRequired;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return WriteOutcome.InvalidType;
        }

        if (!item.IsInRange((float)value))
        {
            _logger.LogWarning(
                "Schreibvorgang für {Item} abgelehnt: Wert {Value} liegt außerhalb von [{Min}, {Max}].",
                item.Id,
                value,
                item.Min,
                item.Max);
            return WriteOutcome.OutOfRange;
        }

        if (!_writeCounter.TryAcquire())
        {
            _logger.LogWarning(
                "Schreibvorgang für {Item} abgelehnt: Limit von {Limit} Schreibvorgängen erreicht.",
                item.Id,
                _writeCounter.Limit);
            _errors.Report(
                "Write",
                "write_limit_exceeded",
                $"Schreibvorgang ({item.TopicPath}) abgelehnt: Limit von {_writeCounter.Limit} erreicht.",
                null,
                "warning");
            return WriteOutcome.LimitExceeded;
        }

        var acquired = false;
        try
        {
            await _busLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)item.BacnetObjectId);
            var payload = new BacnetValue((float)value);

            await _client.WritePropertyAsync(
                DeviceAddress,
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                [payload],
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Schreibvorgang {Item} = {Value} erfolgreich.", item.Id, value);
            MarkConnected();
            return WriteOutcome.Ok;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _writeCounter.Release();
            throw;
        }
        catch (BacnetTimeoutException ex)
        {
            // Befund A22: nur erfolgreiche Schreibvorgänge zählen.
            _writeCounter.Release();

            _logger.LogWarning(ex, "Schreibvorgang {Item} hat das Zeitfenster überschritten.", item.Id);
            _errors.Report(
                "Bacnet",
                "timeout",
                $"WriteProperty ({item.TopicPath}) timed out: {ex.Message}",
                ex,
                "warning");
            MarkFailed("timeout");
            return WriteOutcome.BusError;
        }
        catch (BacnetDeviceErrorException ex) when (ex.ErrorCode == BacnetErrorCodes.ERROR_CODE_WRITE_ACCESS_DENIED)
        {
            // Befund A22: nur erfolgreiche Schreibvorgänge zählen.
            _writeCounter.Release();

            // write-access-denied heißt fast immer: die Gegenstelle ist ein
            // **priorisierter** Analog-Value und verlangt eine Priorität.
            // Ohne diesen Hinweis bleibt nur "Schreiben geht nicht".
            const string Hint =
                "Die ComfoBox hat den Schreibvorgang abgelehnt (write-access-denied). "
                + "Meist verlangt sie eine Priorität - in der config.json "
                + "'Bacnet:WritePriority' auf 8 setzen und den Gateway neu starten.";

            _logger.LogError(ex, "Schreibvorgang {Item} wurde abgelehnt. {Hint}", item.Id, Hint);
            _errors.Report("Bacnet", "write_denied", $"WriteProperty ({item.TopicPath}): {Hint}", ex);
            MarkFailed("write_denied");
            return WriteOutcome.BusError;
        }
        catch (BacnetException ex)
        {
            // Befund A22: nur erfolgreiche Schreibvorgänge zählen.
            _writeCounter.Release();

            _logger.LogError(ex, "Schreibvorgang {Item} ist fehlgeschlagen.", item.Id);
            _errors.Report(
                "Bacnet",
                "write_failed",
                $"WriteProperty ({item.TopicPath}) ist fehlgeschlagen: {ex.Message}",
                ex);
            MarkFailed("write_failed");
            return WriteOutcome.BusError;
        }
        finally
        {
            if (acquired)
            {
                _busLock.Release();
            }
        }
    }

    /// <summary>
    /// Liest ein Item und schreibt den Wert in den Cache.
    /// </summary>
    /// <returns><c>false</c>, wenn das Item nicht gelesen werden konnte.</returns>
    private async Task<bool> ReadItemAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ItemTimeout);

        var acquired = false;
        try
        {
            await _busLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            acquired = true;

            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)item.BacnetObjectId);

            var values = await _client.ReadPropertyAsync(
                DeviceAddress,
                objectId,
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                ASN1.BACNET_ARRAY_ALL,
                timeout.Token).ConfigureAwait(false);

            var number = ToFloat(values.Count > 0 ? values[0].Value : null);
            if (number is null)
            {
                _cache.MarkStale(item.Id);
                _logger.LogWarning("ReadProperty {Item} hat keinen verwertbaren Wert geliefert.", item.Id);
                _errors.Report(
                    "Bacnet",
                    "no_value",
                    $"ReadProperty ({item.TopicPath}) hat keinen verwertbaren Wert geliefert.",
                    null,
                    "warning");
                MarkFailed("no_value");
                return false;
            }

            if (item.Kind == ItemValueKind.Date)
            {
                // Befund A20: der Wert eines Legacy-DateValue bleibt leer.
                _logger.LogDebug("Wert von {Item} wird nicht interpretiert (Legacy-DateValue).", item.Id);
                return true;
            }

            ItemValue value = item.Kind == ItemValueKind.Enum
                ? new EnumValue((int)number.Value, item.EnumValues)
                : new AnalogValue(number.Value, item.Min, item.Max, item.Unit);

            if (item.Kind == ItemValueKind.Analog)
            {
                WarnIfOutOfCatalogRange(item, number.Value);
            }

            _cache.Update(item.Id, value);
            MarkConnected();
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _cache.MarkStale(item.Id);
            _logger.LogWarning(
                "ReadProperty {Item} hat das Zeitfenster von {TimeoutMs} ms überschritten.",
                item.Id,
                ItemTimeout.TotalMilliseconds);
            _errors.Report(
                "Bacnet",
                "timeout",
                $"ReadProperty ({item.TopicPath}) timed out after {ItemTimeout.TotalMilliseconds:0} ms",
                null,
                "warning");
            MarkFailed("timeout");
            return false;
        }
        catch (BacnetTimeoutException ex)
        {
            _cache.MarkStale(item.Id);
            _logger.LogWarning(
                "ReadProperty {Item} hat das Zeitfenster von {TimeoutMs} ms überschritten.",
                item.Id,
                ItemTimeout.TotalMilliseconds);
            _errors.Report(
                "Bacnet",
                "timeout",
                $"ReadProperty ({item.TopicPath}) timed out after {ItemTimeout.TotalMilliseconds:0} ms",
                ex,
                "warning");
            MarkFailed("timeout");
            return false;
        }
        catch (BacnetException ex)
        {
            _cache.MarkStale(item.Id);
            _logger.LogError(ex, "ReadProperty {Item} ist fehlgeschlagen.", item.Id);
            _errors.Report(
                "Bacnet",
                "read_failed",
                $"ReadProperty ({item.TopicPath}) ist fehlgeschlagen: {ex.Message}",
                ex);
            MarkFailed("read_failed");
            return false;
        }
        finally
        {
            if (acquired)
            {
                _busLock.Release();
            }
        }
    }

    /// <summary>
    /// Meldet **einmalig** einen gelesenen Wert außerhalb der Katalog-Grenzen.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="value">Der gelesene Wert.</param>
    /// <remarks>
    /// <para>
    /// Das ist **kein** Fehler des Gateways: der Wert wird übernommen, wie er
    /// vom Gerät kommt. Die Meldung erklärt aber, warum Home Assistant bei
    /// einer Zahl-Entität „value is outside of the range" protokolliert.
    /// </para>
    /// <para>
    /// Die häufigste Ursache im Katalog ist ein **geteiltes BACnet-Objekt**:
    /// <c>KühlraumsollAbstStandby</c> und <c>RestartDelay</c> lesen beide
    /// Objekt 864 - der Rohwert kann nur zu einer der beiden Grenzen passen.
    /// </para>
    /// </remarks>
    private void WarnIfOutOfCatalogRange(ItemDefinition item, float value)
    {
        if (item.IsInRange(value))
        {
            return;
        }

        lock (_rangeWarnedLock)
        {
            if (!_rangeWarned.Add(item.Id))
            {
                return;
            }
        }

        _logger.LogWarning(
            "Der gelesene Wert von {Item} ({Value} {Unit}) liegt außerhalb der Katalog-Grenzen "
            + "[{Min} … {Max}]. Der Wert wird trotzdem übernommen. "
            + "Meist teilen sich zwei Items dasselbe BACnet-Objekt (Objekt-Id {ObjectId}) "
            + "oder die Katalog-Grenzen sind zu eng.",
            item.Id,
            value,
            item.Unit ?? string.Empty,
            item.Min,
            item.Max,
            item.BacnetObjectId);
    }

    /// <summary>
    /// Vermerkt einen erfolgreichen Buszugriff.
    /// </summary>
    private void MarkConnected()    {
        IsConnected = true;
        LastError = null;
    }

    /// <summary>
    /// Vermerkt einen fehlgeschlagenen Buszugriff.
    /// </summary>
    /// <param name="code">Der Fehlercode.</param>
    private void MarkFailed(string code)
    {
        IsConnected = false;
        LastError = code;
    }

    /// <summary>
    /// Wandelt einen BACnet-Wert kultur-invariant in eine Zahl.
    /// </summary>
    private static float? ToFloat(object? value) => value switch
    {
        float single => single,
        double dbl => (float)dbl,
        int integer => integer,
        long longValue => longValue,
        decimal dec => (float)dec,
        string text when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    /// <summary>
    /// Gibt die Bussperre frei.
    /// </summary>
    public void Dispose() => _busLock.Dispose();
}
