using ComfoBox.Bacnet;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Der Polling-Zyklus des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Die Prioritäten des Katalogs werden hier endlich ausgewertet (Befund A15):
/// </para>
/// <list type="bullet">
/// <item><c>Once</c> - einmal beim Start (Gerätetyp, Softwareversion, …)</item>
/// <item><c>Primary</c> - in jedem Zyklus</item>
/// <item><c>Secondary</c> - alle <c>SecondaryPollFactor</c> Zyklen</item>
/// </list>
/// <para>
/// Innerhalb eines Zyklus werden die primären Items zuerst gelesen - der
/// Nutzer sieht die wichtigen Werte zuerst.
/// </para>
/// </remarks>
public sealed class BacnetPollingService : BackgroundService
{
    private readonly BacnetDevice _device;
    private readonly IBacnetClient _client;
    private readonly IItemCatalog _catalog;
    private readonly ILogger<BacnetPollingService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _secondaryPollFactor;

    /// <summary>
    /// Erzeugt den Polling-Dienst.
    /// </summary>
    /// <param name="device">Das BACnet-Gerät.</param>
    /// <param name="client">Der BACnet-Client.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="gatewayOptions">Die Grundeinstellungen.</param>
    /// <param name="logger">Logger.</param>
    public BacnetPollingService(
        BacnetDevice device,
        IBacnetClient client,
        IItemCatalog catalog,
        IOptions<GatewayOptions> gatewayOptions,
        ILogger<BacnetPollingService> logger)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _device = device;
        _client = client;
        _catalog = catalog;
        _logger = logger;

        _pollInterval = TimeSpan.FromSeconds(gatewayOptions.Value.PollIntervalSeconds);
        _secondaryPollFactor = gatewayOptions.Value.SecondaryPollFactor;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var once = ItemsWith(RefreshPriority.Once);
        var primary = ItemsWith(RefreshPriority.Primary);
        var secondary = ItemsWith(RefreshPriority.Secondary);
        var withoutPriority = ItemsWith(RefreshPriority.None);

        _logger.LogInformation(
            "Polling startet: {Once} einmalige, {Primary} primäre, {Secondary} sekundäre, {Other} Items ohne Priorität.",
            once.Count,
            primary.Count,
            secondary.Count,
            withoutPriority.Count);

        try
        {
            _client.Start();
            await _client.WhoIsAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Kein Port, kein Kabel, falscher Portname - der Gateway muss
            // trotzdem starten und weiter versuchen.
            _logger.LogError(ex, "Der BACnet-Client konnte nicht gestartet werden.");
        }

        var cycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (cycle == 0)
                {
                    await _device.ReadItemsAsync(once, stoppingToken).ConfigureAwait(false);
                }

                var items = PollPlan.ForCycle(primary, secondary, withoutPriority, cycle, _secondaryPollFactor);

                await _device.ReadItemsAsync(items, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Der Legacy hat hier den ganzen Gateway zerstört (Befund A7).
                _logger.LogError(ex, "Der Poll-Zyklus ist fehlgeschlagen - der Gateway läuft weiter.");
            }

            cycle++;

            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Polling beendet.");

        try
        {
            await _client.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Der BACnet-Client konnte nicht sauber beendet werden.");
        }
    }

    /// <summary>
    /// Liefert alle Items einer Priorität in Katalog-Reihenfolge.
    /// </summary>
    private List<ItemDefinition> ItemsWith(RefreshPriority priority)
        => [.. _catalog.All.Where(i => i.Priority == priority)];
}
