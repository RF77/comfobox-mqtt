using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Bacnet.Diagnostics;

/// <summary>
/// Protokoll-Fassade für den portierten BACnet-Codec.
/// </summary>
/// <remarks>
/// Der Codec ist Fremdcode und statisch verdrahtet – eine Logger-Injektion in
/// jede Methode wäre eine Änderung des Portierungs-Vertrags
/// ("1:1 portieren, keine Verbesserungen"). Stattdessen wird der Logger an
/// einer Stelle belegt: der Gateway setzt ihn beim Start.
/// </remarks>
public static class BacnetLog
{
    private static ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Der aktive Logger. Standard ist ein <see cref="NullLogger"/>.
    /// </summary>
    public static ILogger Logger
    {
        get => _logger;
        set => _logger = value ?? NullLogger.Instance;
    }
}
