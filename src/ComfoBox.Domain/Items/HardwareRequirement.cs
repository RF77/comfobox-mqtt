namespace ComfoBox.Domain.Items;

/// <summary>
/// Eine Baugruppe, deren Vorhandensein der Regler selbst kennt.
/// </summary>
/// <remarks>
/// <para>
/// Der Regler liefert für einen nicht vorhandenen Fühler den Sentinel
/// <c>32767</c>. Ob ein Fühler vorhanden ist, weiß er aber vorher schon: die
/// Typ-Parameter des Handbuchs (z. B. „Pufferspeichertyp", DP 805) sagen es
/// ihm. Diese Zuordnung macht daraus eine Angabe je Item.
/// </para>
/// <para>
/// Ein Item ohne Zuordnung ist **immer** verfügbar – es gibt keinen Grund,
/// es zu sperren. Die Zuordnung ist rein deklarativ: sie ändert weder Topic
/// noch Schreibbarkeit, sondern nur, ob ein Wert publiziert wird.
/// </para>
/// </remarks>
public sealed record HardwareRequirement
{
    /// <summary>
    /// Der stabile Schlüssel der Baugruppe, z. B. <c>buffer_bottom</c>.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Die Id des Katalog-Items, das die Baugruppe deklariert.
    /// </summary>
    public required string DetectorItemId { get; init; }

    /// <summary>
    /// Der Anzeigename des Typ-Parameters (für Meldungen), z. B. <c>Pufferspeichertyp</c>.
    /// </summary>
    public required string DetectorLabel { get; init; }

    /// <summary>
    /// Der Topic-Pfad der Verfügbarkeit ohne <c>BaseTopic</c> und ohne <c>Status/</c>,
    /// z. B. <c>Hardware/BufferBottom</c>.
    /// </summary>
    public required string TopicPath { get; init; }

    /// <summary>
    /// Der Name der Baugruppe in Meldungen, z. B. <c>Pufferspeicherfühler unten</c>.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Die Werte des Typ-Parameters, bei denen die Baugruppe vorhanden ist.
    /// </summary>
    public required IReadOnlyList<float> PresentValues { get; init; }

    /// <summary>
    /// Die Ids der Items, die von dieser Baugruppe abhängen.
    /// </summary>
    public required IReadOnlyList<string> ItemIds { get; init; }

    /// <summary>
    /// Der Hinweis für den Benutzer, wie die Baugruppe verfügbar wird (oder <c>null</c>).
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Ist die Baugruppe bei diesem Wert des Typ-Parameters vorhanden?
    /// </summary>
    /// <param name="detectorValue">Der gelesene Wert des Typ-Parameters.</param>
    /// <returns><c>true</c>, wenn die Baugruppe vorhanden ist.</returns>
    public bool IsPresent(float detectorValue)
        => PresentValues.Any(value => Math.Abs(value - detectorValue) < 0.001f);
}

/// <summary>
/// Die Zuordnung von Items zu den Baugruppen, die der Regler deklariert.
/// </summary>
/// <remarks>
/// <para>
/// Die Typ-Parameter stehen im Katalog (<c>Config/General/*</c> und
/// <c>Config/Warmwater/Equipment</c>) und werden stündlich gelesen
/// (<see cref="RefreshPriority.Slow"/>). Ihre Nummern stammen aus dem
/// <c>datenpunkt-register.md</c> des Technikerhandbuchs.
/// </para>
/// <para>
/// <b>Nur Einzelgeräte:</b> Die Zuordnung geht von einer Anlage mit einem
/// Pufferspeicher, einem Kühlpufferspeicher und einem Solarkreis aus – die
/// ComfoBox Series 5 hat genau einen Wärmeerzeuger und bis zu sieben Zonen.
/// </para>
/// </remarks>
public static class HardwareRequirements
{
    /// <summary>
    /// Der Wert, mit dem der Regler einen fehlenden Fühler meldet.
    /// </summary>
    /// <remarks>
    /// Das INT16-Maximum – kein Katalog-Item hat einen Bereich, der bis
    /// dorthin reicht. Das Handbuch dokumentiert den Wert nicht; er ist aus
    /// dem Verhalten abgeleitet.
    /// </remarks>
    public const float NoSensorValue = 32767f;

    /// <summary>
    /// Die Id des Typ-Parameters der Warmwasserbereitung (DP 117).
    /// </summary>
    public const string WarmWaterEquipmentId = "config.warmwater.equipment";

    /// <summary>
    /// Der Pufferspeicherfühler oben (DP 24).
    /// </summary>
    public static HardwareRequirement BufferTop { get; } = new()
    {
        Key = "buffer_top",
        DetectorItemId = "config.general.buffer_type",
        DetectorLabel = "Pufferspeichertyp (DP 805)",
        TopicPath = "Hardware/BufferTop",
        Label = "Pufferspeicherfühler oben",
        PresentValues = [1f, 11f],
        ItemIds = ["status.buffer.temperature_top"],
        Hint = "Am Regler den Pufferspeichertyp (DP 805) auf 1 oder 11 stellen.",
    };

    /// <summary>
    /// Der Pufferspeicherfühler unten (DP 25) – nur bei zwei Fühlern.
    /// </summary>
    public static HardwareRequirement BufferBottom { get; } = new()
    {
        Key = "buffer_bottom",
        DetectorItemId = "config.general.buffer_type",
        DetectorLabel = "Pufferspeichertyp (DP 805)",
        TopicPath = "Hardware/BufferBottom",
        Label = "Pufferspeicherfühler unten",
        PresentValues = [11f],
        ItemIds = ["status.buffer.temperature_bottom"],
        Hint = "Am Regler den Pufferspeichertyp (DP 805) auf 11 (zwei Fühler) stellen.",
    };

    /// <summary>
    /// Der Kühlpufferspeicherfühler oben (DP 3925).
    /// </summary>
    public static HardwareRequirement CoolingBufferTop { get; } = new()
    {
        Key = "cooling_buffer_top",
        DetectorItemId = "config.general.cooling_buffer_type",
        DetectorLabel = "Kühlpufferspeichertyp (DP 3918)",
        TopicPath = "Hardware/CoolingBufferTop",
        Label = "Kühlpufferspeicherfühler oben",
        PresentValues = [1f, 11f],
        ItemIds = ["status.buffer.cooling_temperature_top"],
        Hint = "Am Regler den Kühlpufferspeichertyp (DP 3918) auf 1 oder 11 stellen.",
    };

    /// <summary>
    /// Der Kühlpufferspeicherfühler unten (DP 3924) – nur bei zwei Fühlern.
    /// </summary>
    public static HardwareRequirement CoolingBufferBottom { get; } = new()
    {
        Key = "cooling_buffer_bottom",
        DetectorItemId = "config.general.cooling_buffer_type",
        DetectorLabel = "Kühlpufferspeichertyp (DP 3918)",
        TopicPath = "Hardware/CoolingBufferBottom",
        Label = "Kühlpufferspeicherfühler unten",
        PresentValues = [11f],
        ItemIds = ["status.buffer.cooling_temperature_bottom"],
        Hint = "Am Regler den Kühlpufferspeichertyp (DP 3918) auf 11 (zwei Fühler) stellen.",
    };

    /// <summary>
    /// Der Solarkreis (DP 28, 3903, 3911).
    /// </summary>
    public static HardwareRequirement Solar { get; } = new()
    {
        Key = "solar",
        DetectorItemId = "config.general.solar_hydraulics",
        DetectorLabel = "Sonnenkollektor Hydraulik (DP 807)",
        TopicPath = "Hardware/Solar",
        Label = "Solarkreis",
        PresentValues = [1f, 2f, 4f, 21f, 22f, 24f],
        ItemIds =
        [
            "status.solar.collector_temperature",
            "status.solar.collector_heat_exchanger_temperature",
            "status.solar.energy_today",
        ],
        Hint = "Am Regler die Sonnenkollektor-Hydraulik (DP 807) auf einen Wert ungleich 0 stellen.",
    };

    /// <summary>
    /// Die Fühler der Wärmepumpe (DP 21, 641, 660, 666, 3209, 3228, 3468, 3475, 3598).
    /// </summary>
    public static HardwareRequirement HeatPumpSensors { get; } = new()
    {
        Key = "heat_pump_sensors",
        DetectorItemId = "config.general.heat_pump_sensor",
        DetectorLabel = "Fühler zu WP-Regelung (DP 2800)",
        TopicPath = "Hardware/HeatPumpSensors",
        Label = "Fühler der Wärmepumpe",
        PresentValues = [1f],
        ItemIds =
        [
            "status.heat_pump.energy_producer_temperature",
            "status.heat_pump.return_temperature",
            "status.heat_pump.primary_flow_temperature",
            "status.heat_pump.evaporator_temperature",
            "status.heat_pump.suction_gas_temperature",
            "status.heat_pump.hot_gas_temperature",
            "status.heat_pump.condenser_temperature",
            "status.heat_pump.condensation_temperature",
            "status.heat_pump.evaporation_temperature",
        ],
        Hint = "Am Regler „Fühler zu WP-Regelung“ (DP 2800) auf 1 stellen, wenn die Fühler angeschlossen sind.",
    };

    /// <summary>
    /// Der zweite Warmwasserfühler (DP 4) – nur bei zwei Fühlern (DP 117 = 2).
    /// </summary>
    public static HardwareRequirement WarmWaterBottom { get; } = new()
    {
        Key = "warm_water_bottom",
        DetectorItemId = WarmWaterEquipmentId,
        DetectorLabel = "Warmwasserbereitung (DP 117)",
        TopicPath = "Hardware/WarmWaterBottom",
        Label = "unterer Warmwasserfühler",
        PresentValues = [2f],
        ItemIds = ["status.warm_water.temperature_bottom"],
        Hint = "Am Regler die Warmwasserbereitung (DP 117) auf 2 (zwei Fühler) stellen.",
    };

    /// <summary>
    /// Alle Baugruppen.
    /// </summary>
    public static IReadOnlyList<HardwareRequirement> All { get; } =
    [
        BufferTop,
        BufferBottom,
        CoolingBufferTop,
        CoolingBufferBottom,
        Solar,
        HeatPumpSensors,
        WarmWaterBottom,
    ];

    private static readonly Dictionary<string, HardwareRequirement> ByItemId = BuildItemMap();

    private static readonly HashSet<string> Detectors =
        [.. All.Select(static requirement => requirement.DetectorItemId)];

    /// <summary>
    /// Die Ids der Typ-Parameter (sie werden immer gelesen).
    /// </summary>
    public static IReadOnlyCollection<string> DetectorItemIds => Detectors;

    /// <summary>
    /// Sucht die Baugruppe eines Items.
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    /// <returns>Die Baugruppe oder <c>null</c>, wenn das Item immer verfügbar ist.</returns>
    public static HardwareRequirement? ForItem(string itemId)
        => ByItemId.TryGetValue(itemId, out var requirement) ? requirement : null;

    /// <summary>
    /// Ist das Item ein Typ-Parameter?
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    public static bool IsDetector(string itemId) => Detectors.Contains(itemId);

    private static Dictionary<string, HardwareRequirement> BuildItemMap()
    {
        var map = new Dictionary<string, HardwareRequirement>(StringComparer.Ordinal);

        foreach (var requirement in All)
        {
            foreach (var itemId in requirement.ItemIds)
            {
                map[itemId] = requirement;
            }
        }

        return map;
    }
}
