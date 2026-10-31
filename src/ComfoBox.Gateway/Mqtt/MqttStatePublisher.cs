using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Publiziert die Werte der Items auf ihre Topics.
/// </summary>
/// <remarks>
/// <para>
/// Der Publisher hört auf den Wert-Cache: jede Änderung wird auf
/// <c>{BaseTopic}/{Group}/{Name}</c> publiziert (Enum-Items zusätzlich auf
/// <c>…/AsNumber</c>).
/// </para>
/// <para>
/// Nach jedem Verbindungsaufbau werden **alle** bekannten Werte erneut
/// publiziert - so hat Home Assistant nach einem Neustart sofort den
/// aktuellen Stand.
/// </para>
/// </remarks>
public sealed class MqttStatePublisher : BackgroundService, IMqttConnectionHandler
{
    private readonly IValueCache _cache;
    private readonly IItemCatalog _catalog;
    private readonly MqttTopicMap _topics;
    private readonly IMqttPublisher _publisher;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttStatePublisher> _logger;

    /// <summary>
    /// Erzeugt den Publisher.
    /// </summary>
    /// <param name="cache">Der Wert-Cache.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="publisher">Der Publisher.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="logger">Logger.</param>
    public MqttStatePublisher(
        IValueCache cache,
        IItemCatalog catalog,
        MqttTopicMap topics,
        IMqttPublisher publisher,
        MqttOptions options,
        ILogger<MqttStatePublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _catalog = catalog;
        _topics = topics;
        _publisher = publisher;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var cached in _cache.SubscribeAsync(stoppingToken).ConfigureAwait(false))
            {
                await PublishAsync(cached, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Der Shutdown läuft.
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        await PublishAllAsync(client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Publiziert **alle** bekannten Werte auf einem Client.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    public async Task PublishAllAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var published = 0;

        foreach (var cached in _cache.Snapshot())
        {
            var item = _catalog.Find(cached.Id);
            if (item is null || cached.Value is null)
            {
                continue;
            }

            if (await PublishToClientAsync(client, item, cached, cancellationToken).ConfigureAwait(false))
            {
                published++;
            }
        }

        _logger.LogInformation("{Count} Werte nach dem Verbindungsaufbau publiziert.", published);
    }

    /// <summary>
    /// Publiziert einen geänderten Wert.
    /// </summary>
    /// <param name="cached">Der gecachte Wert.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private async Task PublishAsync(CachedValue cached, CancellationToken cancellationToken)
    {
        var item = _catalog.Find(cached.Id);
        if (item is null || cached.Value is null)
        {
            return;
        }

        var payload = MqttValueFormatter.StatePayload(item, cached.Value);

        await _publisher.PublishAsync(
            _topics.ItemTopic(item),
            payload,
            MqttQualityOfServiceLevel.AtMostOnce,
            _options.UseMqttRetain,
            cancellationToken).ConfigureAwait(false);

        var number = MqttValueFormatter.NumberPayload(item, cached.Value);
        if (number is null)
        {
            return;
        }

        await _publisher.PublishAsync(
            _topics.ItemAsNumberTopic(item),
            number,
            MqttQualityOfServiceLevel.AtMostOnce,
            _options.UseMqttRetain,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Publiziert einen Wert auf einen bestimmten Client.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="item">Das Item.</param>
    /// <param name="cached">Der gecachte Wert.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn der Wert publiziert wurde.</returns>
    private async Task<bool> PublishToClientAsync(
        IMqttClient client,
        ItemDefinition item,
        CachedValue cached,
        CancellationToken cancellationToken)
    {
        var payload = MqttValueFormatter.StatePayload(item, cached.Value);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_topics.ItemTopic(item))
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .WithRetainFlag(_options.UseMqttRetain)
            .Build();

        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        var number = MqttValueFormatter.NumberPayload(item, cached.Value);
        if (number is null)
        {
            return true;
        }

        var numberMessage = new MqttApplicationMessageBuilder()
            .WithTopic(_topics.ItemAsNumberTopic(item))
            .WithPayload(number)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .WithRetainFlag(_options.UseMqttRetain)
            .Build();

        await client.PublishAsync(numberMessage, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
