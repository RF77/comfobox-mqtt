using System.Globalization;
using System.Text.Json;
using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serialize;
using ComfoBox.Domain.Items;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Ein BACnet-Client, der **ohne echte Hardware** auskommt.
/// </summary>
/// <remarks>
/// <para>
/// Der Client ist die Attrappe für den BACnet-Bus: Er beantwortet
/// <c>ReadProperty</c> mit Werten aus dem Item-Katalog. Damit lässt sich der
/// komplette Weg <em>Item → Wert → MQTT → Home Assistant</em> ohne ComfoBox
/// und ohne USB-Adapter prüfen - zum Beispiel für den Parallelvergleich aus
/// Phase 7 (zwei Gateways nebeneinander, siehe ADR-013).
/// </para>
/// <para>
/// <b>Wichtig:</b> Der BACnet-Transport (MSTP-Frame, CRC, Token-Passing) wird
/// <b>nicht</b> durchlaufen. Diese Schicht ist durch die Golden-Tests in
/// <c>ComfoBox.Bacnet.Tests</c> abgedeckt. Der simulierte Client prüft die
/// Schichten <em>oberhalb</em> des Busses.
/// </para>
/// <para>
/// Die Werte sind <b>deterministisch</b>: derselbe Topic-Pfad liefert immer
/// denselben Wert. Zwei Instanzen - auch auf zwei Rechnern - liefern damit
/// dieselben Werte, ohne dass ein Gerät beteiligt ist.
/// </para>
/// </remarks>
public sealed class SimulatedBacnetClient : IBacnetClient
{
    /// <summary>
    /// Die Geräte-Instanz des simulierten ComfoBox (ELESTA: 1).
    /// </summary>
    private const uint SimulatedDeviceId = 1;

    private readonly Dictionary<int, float> _values = [];
    private readonly ILogger<SimulatedBacnetClient> _logger;

    /// <summary>
    /// Erzeugt den Client.
    /// </summary>
    /// <param name="catalog">Der Item-Katalog (liefert die Objekt-Ids).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="valuesPath">Pfad zu einer JSON-Datei mit Wert-Overrides (optional).</param>
    public SimulatedBacnetClient(
        IItemCatalog catalog,
        ILogger<SimulatedBacnetClient> logger,
        string? valuesPath = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        foreach (var item in catalog.All)
        {
            if (item.Kind == ItemValueKind.Date)
            {
                // Befund A20: der Wert eines Legacy-DateValue bleibt leer.
                continue;
            }

            if (_values.ContainsKey(item.BacnetObjectId))
            {
                // Zwei Topics können auf **dasselbe** BACnet-Objekt zeigen
                // (im Legacy z. B. Objekt 864: KuehlraumsollAbstStandby und
                // RestartDelay). Am echten Geraet lesen beide denselben
                // Rohwert - die Simulation muss das abbilden.
                continue;
            }

            _values[item.BacnetObjectId] = GenerateValue(item);
        }

        _logger.LogInformation(
            "Simulierter BACnet-Client aktiv: {Count} Items mit deterministischen Werten.",
            _values.Count);

        ApplyOverrides(catalog, valuesPath);
    }

    /// <inheritdoc />
    public int Timeout { get; set; } = 3000;

    /// <inheritdoc />
    public int Retries { get; set; } = 3;

    /// <inheritdoc />
    public int TransmitTimeout { get; set; } = 3000;

    /// <inheritdoc />
    public uint WritePriority { get; set; } = 8;

    /// <inheritdoc />
    public event EventHandler<BacnetIamEventArgs>? IamReceived;

    /// <inheritdoc />
    public void Start()
    {
        _logger.LogInformation("Simulierter BACnet-Client gestartet (kein serieller Port geöffnet).");
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        _logger.LogInformation("Simulierter BACnet-Client gestoppt.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void RequestReconnect()
    {
        // Es gibt keine Leitung, die neu aufgebaut werden könnte.
    }

    /// <inheritdoc />
    public Task WhoIsAsync(int lowLimit = -1, int highLimit = -1, CancellationToken cancellationToken = default)
    {
        // Ein realer Client wartet hier auf die I-Am-Antwort des Geräts.
        // Die Simulation meldet das Gerät sofort - so verhält sich der
        // Polling-Dienst wie an einem echten Bus.
        IamReceived?.Invoke(
            this,
            new BacnetIamEventArgs(
                new BacnetAddress(BacnetAddressTypes.MSTP, 0, [(byte)SimulatedDeviceId]),
                SimulatedDeviceId,
                480,
                BacnetSegmentations.SEGMENTATION_BOTH,
                0));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IList<BacnetValue>> ReadPropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        uint arrayIndex = ASN1.BACNET_ARRAY_ALL,
        CancellationToken cancellationToken = default)
    {
        if (propertyId != BacnetPropertyIds.PROP_PRESENT_VALUE
            || !_values.TryGetValue((int)objectId.Instance, out var value))
        {
            // Ein echtes Gerät antwortet auf einen unbekannten Wert mit einem
            // Fehler. Der Client erwartet dann eine leere Liste.
            return Task.FromResult<IList<BacnetValue>>([]);
        }

        return Task.FromResult<IList<BacnetValue>>(
            [new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, value)]);
    }

    /// <inheritdoc />
    public Task WritePropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        IEnumerable<BacnetValue> values,
        CancellationToken cancellationToken = default)
    {
        if (values.FirstOrDefault() is { } value && value.Value is not null)
        {
            if (value.Value is float number)
            {
                _values[(int)objectId.Instance] = number;
            }
            else if (value.Value is int integer)
            {
                _values[(int)objectId.Instance] = integer;
            }
            else if (value.Value is double dbl)
            {
                _values[(int)objectId.Instance] = (float)dbl;
            }

            _logger.LogWarning(
                "Schreibvorgang auf Objekt {Instance} landet nur in der Simulation (kein Gerät angeschlossen).",
                objectId.Instance);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IamReceived = null;
    }

    /// <summary>
    /// Liefert der Client für dieses Objekt einen Wert?
    /// </summary>
    /// <param name="instance">Die Objekt-Instanz.</param>
    /// <returns><c>true</c>, wenn ein Wert vorhanden ist.</returns>
    public bool HasValue(int instance) => _values.ContainsKey(instance);

    /// <summary>
    /// Erzeugt den deterministischen Wert eines BACnet-Objekts.
    /// </summary>
    /// <param name="item">Das Item, das das Objekt beschreibt.</param>
    /// <returns>Der Wert.</returns>
    /// <remarks>
    /// Der Hash ist bewusst selbst gebildet (FNV-1a) und **nicht**
    /// <c>string.GetHashCode()</c> - der ist pro Prozess zufaellig und wuerde
    /// die Werte zwischen zwei Laeufen veraendern. Er bezieht sich auf die
    /// **Objekt-Id**, nicht auf das Topic: der Wert gehoert dem Objekt.
    /// </remarks>
    private static float GenerateValue(ItemDefinition item)
    {
        var hash = Fnv1a(item.BacnetObjectId.ToString(CultureInfo.InvariantCulture));

        if (item.Kind == ItemValueKind.Enum && item.EnumValues.Count > 0)
        {
            var values = item.EnumValues.Values.ToList();
            var name = values[(int)(hash % (uint)values.Count)];

            return item.TryResolveEnumValue(name, out var enumValue) ? enumValue : 0f;
        }

        var min = item.Min ?? 0f;
        var max = item.Max ?? 100f;

        if (max <= min)
        {
            return min;
        }

        // 0.01-Schritte innerhalb des Wertebereichs.
        var steps = (uint)Math.Round((max - min) * 100);
        return min + ((hash % Math.Max(steps, 1)) / 100f);
    }

    /// <summary>
    /// FNV-1a über die Zeichenkette (stabil über Prozesse und Rechner).
    /// </summary>
    /// <param name="value">Die Zeichenkette.</param>
    /// <returns>Der Hash.</returns>
    private static uint Fnv1a(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    /// <summary>
    /// Lädt die Wert-Overrides aus einer JSON-Datei.
    /// </summary>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="valuesPath">Der Pfad (oder <c>null</c>).</param>
    private void ApplyOverrides(IItemCatalog catalog, string? valuesPath)
    {
        if (string.IsNullOrWhiteSpace(valuesPath))
        {
            return;
        }

        if (!File.Exists(valuesPath))
        {
            _logger.LogWarning(
                "Die Wert-Datei '{Path}' existiert nicht - es werden die generierten Werte verwendet.",
                valuesPath);
            return;
        }

        try
        {
            using var stream = File.OpenRead(valuesPath);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("values", out var values)
                || values.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning(
                    "Die Wert-Datei '{Path}' enthält kein Objekt 'values' - sie wird ignoriert.",
                    valuesPath);
                return;
            }

            var applied = 0;
            foreach (var entry in values.EnumerateObject())
            {
                var item = catalog.FindByTopicPath(entry.Name);
                if (item is null)
                {
                    _logger.LogWarning(
                        "Die Wert-Datei '{Path}' nennt das unbekannte Item '{Item}' - es wird übersprungen.",
                        valuesPath,
                        entry.Name);
                    continue;
                }

                if (!TryResolveOverride(item, entry.Value, out var value))
                {
                    _logger.LogWarning(
                        "Der Wert für '{Item}' in '{Path}' ist unbrauchbar - er wird übersprungen.",
                        item.TopicPath,
                        valuesPath);
                    continue;
                }

                _values[item.BacnetObjectId] = value;
                applied++;
            }

            _logger.LogInformation(
                "Wert-Datei '{Path}' eingelesen: {Count} Werte überschrieben.",
                valuesPath,
                applied);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Die Wert-Datei '{Path}' konnte nicht gelesen werden.", valuesPath);
        }
    }

    /// <summary>
    /// Wandelt einen JSON-Wert in den Zahlenwert des Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="element">Der JSON-Wert.</param>
    /// <param name="value">Der Zahlenwert.</param>
    /// <returns><c>true</c>, wenn der Wert brauchbar war.</returns>
    private static bool TryResolveOverride(ItemDefinition item, JsonElement element, out float value)
    {
        value = 0f;

        if (item.Kind == ItemValueKind.Enum)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var name = element.GetString() ?? string.Empty;
            if (!item.TryResolveEnumValue(name, out var enumValue))
            {
                return false;
            }

            value = enumValue;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetSingle();
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return float.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }
}
