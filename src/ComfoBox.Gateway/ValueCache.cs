using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ComfoBox.Domain.Values;

namespace ComfoBox.Gateway;

/// <summary>
/// Ein zwischengespeicherter Wert mit Zeitstempel.
/// </summary>
/// <param name="Id">Die Id des Items.</param>
/// <param name="Value">Der Wert (oder <c>null</c>).</param>
/// <param name="Timestamp">Der Zeitpunkt des letzten erfolgreichen Lesens.</param>
/// <param name="IsStale">Ist der Wert veraltet?</param>
public sealed record CachedValue(string Id, ItemValue? Value, DateTimeOffset Timestamp, bool IsStale);

/// <summary>
/// Der Cache zwischen BACnet, MQTT, REST und UI.
/// </summary>
/// <remarks>
/// Der Cache ist die Wahrheit: MQTT, REST und SSE lesen ausschließlich hier.
/// </remarks>
public interface IValueCache
{
    /// <summary>
    /// Liest einen Wert.
    /// </summary>
    /// <param name="id">Die Id des Items.</param>
    /// <param name="value">Der gefundene Wert.</param>
    /// <returns><c>false</c>, wenn noch kein Wert gelesen wurde.</returns>
    bool TryGet(string id, out CachedValue? value);

    /// <summary>
    /// Schreibt einen Wert (nach einem erfolgreichen BACnet-Read).
    /// </summary>
    /// <param name="id">Die Id des Items.</param>
    /// <param name="value">Der gelesene Wert.</param>
    void Update(string id, ItemValue? value);

    /// <summary>
    /// Markiert einen Wert als veraltet (der letzte Read ist fehlgeschlagen).
    /// </summary>
    /// <param name="id">Die Id des Items.</param>
    void MarkStale(string id);

    /// <summary>
    /// Ist der Wert veraltet?
    /// </summary>
    /// <param name="id">Die Id des Items.</param>
    bool IsStale(string id);

    /// <summary>
    /// Liefert alle gecachten Werte.
    /// </summary>
    CachedValue[] Snapshot();

    /// <summary>
    /// Abonniert die Wertänderungen (SSE).
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    IAsyncEnumerable<CachedValue> SubscribeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Der Wert-Cache des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Ein Wert gilt als <c>stale</c>, wenn der letzte Read fehlgeschlagen ist
/// (dann wird er markiert) oder wenn er älter als drei Poll-Zyklen ist.
/// </para>
/// <para>
/// Der Wert bleibt im Cache, wenn ein Read fehlschlägt - die UI zeigt so den
/// letzten bekannten Stand als „Wert veraltet".
/// </para>
/// </remarks>
public sealed class ValueCache : IValueCache
{
    private readonly ConcurrentDictionary<string, CachedValue> _values = new();
    private readonly List<Channel<CachedValue>> _subscribers = [];
    private readonly object _subscriberLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleAfter;

    /// <summary>
    /// Erzeugt den Cache.
    /// </summary>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    /// <param name="staleAfter">
    /// Die Zeitspanne, nach der ein Wert als veraltet gilt. Standard: 3 Poll-Zyklen.
    /// </param>
    public ValueCache(TimeProvider? timeProvider = null, TimeSpan? staleAfter = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _staleAfter = staleAfter ?? TimeSpan.FromMinutes(3);
    }

    /// <inheritdoc />
    public bool TryGet(string id, out CachedValue? value)
    {
        if (_values.TryGetValue(id, out var stored))
        {
            value = stored with { IsStale = stored.IsStale || IsExpired(stored.Timestamp) };
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc />
    public void Update(string id, ItemValue? value)
    {
        var cached = new CachedValue(id, value, _timeProvider.GetUtcNow(), IsStale: false);
        _values[id] = cached;
        Publish(cached);
    }

    /// <inheritdoc />
    public void MarkStale(string id)
    {
        if (_values.TryGetValue(id, out var stored))
        {
            _values[id] = stored with { IsStale = true };
        }
    }

    /// <inheritdoc />
    public bool IsStale(string id) => TryGet(id, out var value) && value!.IsStale;

    /// <inheritdoc />
    public CachedValue[] Snapshot() => [.. _values.Values];

    /// <inheritdoc />
    public IAsyncEnumerable<CachedValue> SubscribeAsync(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<CachedValue>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_subscriberLock)
        {
            _subscribers.Add(channel);
        }

        return ReadAsync(channel, cancellationToken);
    }

    /// <summary>
    /// Liest die Änderungen eines Abonnenten.
    /// </summary>
    private async IAsyncEnumerable<CachedValue> ReadAsync(
        Channel<CachedValue> channel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var value))
                {
                    yield return value;
                }
            }
        }
        finally
        {
            lock (_subscriberLock)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    /// <summary>
    /// Informiert alle Abonnenten über eine Änderung.
    /// </summary>
    private void Publish(CachedValue value)
    {
        lock (_subscriberLock)
        {
            foreach (var subscriber in _subscribers)
            {
                // Ein langsamer Client darf die others nicht blockieren -
                // der älteste Eintrag wird verworfen.
                subscriber.Writer.TryWrite(value);
            }
        }
    }

    /// <summary>
    /// Ist der Zeitstempel älter als die Schwelle?
    /// </summary>
    private bool IsExpired(DateTimeOffset timestamp)
        => _timeProvider.GetUtcNow() - timestamp > _staleAfter;
}
