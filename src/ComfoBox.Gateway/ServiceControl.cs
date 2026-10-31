using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway;

/// <summary>
/// Die Steuerung des Gateway-Prozesses.
/// </summary>
public interface IRestartService
{
    /// <summary>
    /// Fordert den Neustart an.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die die Anforderung abschließt.</returns>
    Task RequestRestartAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Die Steuerung des Gateway-Prozesses.
/// </summary>
/// <remarks>
/// <para>
/// Der Neustart läuft **in-process**: <see cref="IHostApplicationLifetime.StopApplication"/>
/// beendet den Host kontrolliert. Der Container bzw. der Supervisor startet
/// ihn neu.
/// </para>
/// <para>
/// Warum nicht <c>docker restart</c>? Im Container ist kein Docker-Socket
/// vorhanden (Sicherheit) - und <c>systemctl</c> läuft dort ebenfalls nicht.
/// </para>
/// </remarks>
public sealed class ServiceControl : IRestartService
{
    /// <summary>
    /// Der Name der Marker-Datei, die einen Neustart anfordert.
    /// </summary>
    public const string RestartMarkerFileName = "restart.requested";

    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<GatewayOptions> _options;
    private readonly ILogger<ServiceControl> _logger;
    private readonly string _dataDirectory;

    /// <summary>
    /// Erzeugt die Steuerung.
    /// </summary>
    /// <param name="lifetime">Die Lebensdauer des Hosts.</param>
    /// <param name="options">Die Grundeinstellungen.</param>
    /// <param name="dataDirectory">Das Datenverzeichnis (für die Marker-Datei).</param>
    /// <param name="logger">Logger.</param>
    public ServiceControl(
        IHostApplicationLifetime lifetime,
        IOptions<GatewayOptions> options,
        string dataDirectory,
        ILogger<ServiceControl> logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _options = options;
        _dataDirectory = dataDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RequestRestartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.RestartEnabled)
        {
            _logger.LogWarning("Der Neustart ist deaktiviert (Gateway:RestartEnabled=false).");
            return Task.CompletedTask;
        }

        _logger.LogWarning("Neustart angefordert - der Host wird kontrolliert beendet.");

        WriteRestartMarker();

        _lifetime.StopApplication();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Schreibt die Marker-Datei - der Supervisor kann sie auswerten.
    /// </summary>
    private void WriteRestartMarker()
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            File.WriteAllText(
                Path.Combine(_dataDirectory, RestartMarkerFileName),
                DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Marker-Datei ist optional - ihr Fehler darf den Neustart
            // nicht verhindern.
            _logger.LogWarning(ex, "Die Restart-Marker-Datei konnte nicht geschrieben werden.");
        }
    }
}
