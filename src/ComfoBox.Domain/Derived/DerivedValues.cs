using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Derived;

/// <summary>
/// Die abgeleiteten Werte der ComfoBox - der Gateway rechnet, nicht das Gerät.
/// </summary>
/// <remarks>
/// <para>
/// Die Formeln sind **rein**: keine Uhr, kein Cache, kein I/O, keine Kultur.
/// Die Zeit und der Wert-Cache bleiben im Gateway (<c>DerivedValueService</c>) -
/// dadurch ist jede Formel ohne Hardware, ohne Warten und ohne MQTT prüfbar.
/// </para>
/// <para>
/// Ein fehlender Eingang ergibt <c>null</c> (= „unbekannt"). Ein abgeleiteter
/// Wert ist **nie** <c>0</c>, nur weil ein Eingang fehlt - eine 0 wäre eine
/// Aussage, die niemand belegen kann.
/// </para>
/// <para>
/// Die Eingänge werden über die **BACnet-Objekt-Id** adressiert. Der Katalog
/// ist für diese Datenpunkte eindeutig (jeder kommt genau einmal vor); ein Test
/// wacht darüber.
/// </para>
/// </remarks>
public static class DerivedValues
{
    /// <summary>
    /// Die Id des Momentan-COP (<c>Status/Derived/CopInstant</c>).
    /// </summary>
    public const string CopInstantId = "derived.cop_instant";

    /// <summary>
    /// Das BACnet-Objekt des Wärmezählers (DP 3349, <c>ThermalEnergyTotal</c>).
    /// </summary>
    public const int ThermalEnergyTotalObjectId = 3349;

    /// <summary>
    /// Das BACnet-Objekt des Stromzählers (DP 3355, <c>ElectricalEnergyTotal</c>).
    /// </summary>
    public const int ElectricalEnergyTotalObjectId = 3355;

    // Die Eingänge der abgeleiteten Werte (Datenpunkt-Nummern des Handbuchs).
    private const int SuctionGasTemperatureObjectId = 641;
    private const int EvaporatorTemperatureObjectId = 660;
    private const int CurrentFlowTemperatureObjectId = 600;
    private const int ReturnTemperatureObjectId = 3468;
    private const int BufferTopTemperatureObjectId = 24;
    private const int BufferBottomTemperatureObjectId = 25;
    private const int HeatPumpOperatingHoursObjectId = 30;
    private const int Stage2OperatingHoursObjectId = 31;
    private const int HeatingOperatingHoursObjectId = 3060;
    private const int ServiceHoursObjectId = 3366;
    private const int ServiceIntervalObjectId = 3365;
    private const int HeatPumpStatusObjectId = 662;
    private const int CoolingFlowTemperatureObjectId = 3098;

    // Die Enum-Werte von DP 662, die „Kühlung läuft" bedeuten
    // (HeatPumpStatusEnums: FreecoolingActive, ActiveCooling,
    // ACtiveCoolingWithPassiveWarmWater).
    private const float FreecoolingActive = 9f;
    private const float ActiveCooling = 30f;
    private const float ActiveCoolingWithPassiveWarmWater = 31f;

    /// <summary>
    /// Die sieben abgeleiteten Werte.
    /// </summary>
    /// <remarks>
    /// Die Reihenfolge entspricht der Tabelle in
    /// <c>docs/06-aufgaben/phase-10-topics-v2.md</c> (Aufgabe 10.10) und ist
    /// damit zugleich die Reihenfolge der Home-Assistant-Discovery.
    /// </remarks>
    public static IReadOnlyList<DerivedValueDefinition> All { get; } =
    [
        new()
        {
            Id = "derived.superheat",
            TopicPath = "Derived/Superheat",
            Unit = "K",
            Formula =
                "DP 641 − DP 660 (Sauggas − Verdampfer). Dauerhaft unter 2 K: Expansionsventil zu weit "
                + "offen oder ein Fühler ist defekt. Über 15 K: Kältemittelmangel.",
            InputObjectIds = [SuctionGasTemperatureObjectId, EvaporatorTemperatureObjectId],
            Compute = static inputs => Difference(
                inputs, SuctionGasTemperatureObjectId, EvaporatorTemperatureObjectId),
        },
        new()
        {
            Id = "derived.heating_circuit_delta",
            TopicPath = "Derived/HeatingCircuitDelta",
            Unit = "K",
            Formula =
                "DP 600 − DP 3468 (Vorlauf − Rücklauf). Unter 3 K: Volumenstrom zu hoch (Pumpe "
                + "prüfen). Über 15 K: Volumenstrom zu niedrig.",
            InputObjectIds = [CurrentFlowTemperatureObjectId, ReturnTemperatureObjectId],
            Compute = static inputs => Difference(
                inputs, CurrentFlowTemperatureObjectId, ReturnTemperatureObjectId),
        },
        new()
        {
            Id = "derived.buffer_delta",
            TopicPath = "Derived/BufferDelta",
            Unit = "K",
            Formula =
                "DP 24 − DP 25 (Puffer oben − unten). Beim Laden muss die Schichtung steigen; "
                + "sonst ist ein Fühler oder die Pumpe defekt.",
            InputObjectIds = [BufferTopTemperatureObjectId, BufferBottomTemperatureObjectId],
            Compute = static inputs => Difference(
                inputs, BufferTopTemperatureObjectId, BufferBottomTemperatureObjectId),
        },
        new()
        {
            Id = "derived.runtime_ratio",
            TopicPath = "Derived/RuntimeRatio",
            Unit = "%",
            Formula =
                "(DP 30 + DP 31) / DP 3060 × 100. Taktverhalten: sehr hoch bedeutet "
                + "Überdimensionierung oder eine zu kleine Schaltdifferenz. Nenner 0 ⇒ unbekannt.",
            InputObjectIds =
            [
                HeatPumpOperatingHoursObjectId,
                Stage2OperatingHoursObjectId,
                HeatingOperatingHoursObjectId,
            ],
            Compute = static inputs => RuntimeRatio(
                inputs,
                HeatPumpOperatingHoursObjectId,
                Stage2OperatingHoursObjectId,
                HeatingOperatingHoursObjectId),
        },
        new()
        {
            Id = "derived.service_due",
            TopicPath = "Derived/ServiceDue",
            Formula =
                "DP 3366 ≥ DP 3365 (Service-Stunden ≥ Wartungsintervall) ⇒ 1, sonst 0. Dieselbe "
                + "Bedingung, die am Gerät „Service notwendig“ auslöst.",
            InputObjectIds = [ServiceHoursObjectId, ServiceIntervalObjectId],
            Compute = static inputs => ServiceDue(
                inputs, ServiceHoursObjectId, ServiceIntervalObjectId),
        },
        new()
        {
            Id = CopInstantId,
            TopicPath = "Derived/CopInstant",
            Formula =
                "Δ DP 3349 / Δ DP 3355 über mindestens 1 h. Zwei Proben sind nötig; beim Start "
                + "und nach einem Neustart ist der Wert unbekannt. Δelektrisch = 0 ⇒ unbekannt.",
            InputObjectIds = [ThermalEnergyTotalObjectId, ElectricalEnergyTotalObjectId],
            RequiresHistory = true,
        },
        new()
        {
            Id = "derived.cooling_active",
            TopicPath = "Derived/CoolingActive",
            Formula =
                "DP 662 (HeatPumpStatus) ∈ {9 FreecoolingActive, 30 ActiveCooling, "
                + "31 ACtiveCoolingWithPassiveWarmWater} oder DP 3098 (CoolingFlowTemperature) "
                + "> 0 ⇒ 1, sonst 0. Auslegung: das Gerät meldet 0 °C, solange es nicht kühlt.",
            InputObjectIds = [HeatPumpStatusObjectId, CoolingFlowTemperatureObjectId],
            Compute = static inputs => CoolingActive(
                inputs, HeatPumpStatusObjectId, CoolingFlowTemperatureObjectId),
        },
    ];

    /// <summary>
    /// Der Momentan-COP aus zwei Zählerdifferenzen.
    /// </summary>
    /// <param name="thermalDelta">Die Änderung des Wärmezählers (DP 3349).</param>
    /// <param name="electricalDelta">Die Änderung des Stromzählers (DP 3355).</param>
    /// <returns>Der COP oder <c>null</c>, wenn er nicht berechenbar ist.</returns>
    /// <remarks>
    /// <para>
    /// Ohne verbrauchten Strom gibt es keinen COP: <c>Δelektrisch = 0</c> ergibt
    /// <c>null</c> (= unbekannt), nicht <c>0</c>. Ein **negatives** Delta ist
    /// ebenfalls <c>null</c>: ein Zähler, der rückwärts läuft, bedeutet eine
    /// Rücksetzung des Geräts - über diesen Zeitraum ist keine Aussage möglich.
    /// </para>
    /// <para>
    /// Der Zählerstand ist monoton steigend; ein negatives Wärme-Delta bleibt
    /// dagegen erlaubt (Kühlbetrieb) und ergibt einen negativen Wert.
    /// </para>
    /// </remarks>
    public static float? CopInstant(double thermalDelta, double electricalDelta)
    {
        if (!double.IsFinite(thermalDelta) || !double.IsFinite(electricalDelta))
        {
            return null;
        }

        if (electricalDelta <= 0d)
        {
            return null;
        }

        return (float)(thermalDelta / electricalDelta);
    }

    /// <summary>
    /// Liest einen Eingang (fehlender Schlüssel oder <c>null</c> ⇒ unbekannt).
    /// </summary>
    private static float? Input(IReadOnlyDictionary<int, float?> inputs, int objectId)
        => inputs.TryGetValue(objectId, out var value) ? value : null;

    /// <summary>
    /// Die Differenz zweier Eingänge (z. B. Vorlauf − Rücklauf).
    /// </summary>
    private static float? Difference(
        IReadOnlyDictionary<int, float?> inputs,
        int minuend,
        int subtrahend)
    {
        var first = Input(inputs, minuend);
        var second = Input(inputs, subtrahend);

        return first is null || second is null ? null : first.Value - second.Value;
    }

    /// <summary>
    /// (Einschaltstunden Stufe 1 + Stufe 2) / Heizstunden × 100.
    /// </summary>
    private static float? RuntimeRatio(
        IReadOnlyDictionary<int, float?> inputs,
        int heatPumpHoursId,
        int stage2HoursId,
        int heatingHoursId)
    {
        var heatPumpHours = Input(inputs, heatPumpHoursId);
        var stage2Hours = Input(inputs, stage2HoursId);
        var heatingHours = Input(inputs, heatingHoursId);

        if (heatPumpHours is null || stage2Hours is null || heatingHours is null)
        {
            return null;
        }

        // Division nie ohne Nenner-Prüfung: 0 Stunden heißt „keine Aussage".
        if (heatingHours.Value == 0f)
        {
            return null;
        }

        return (heatPumpHours.Value + stage2Hours.Value) / heatingHours.Value * 100f;
    }

    /// <summary>
    /// Wartung fällig? (Service-Stunden ≥ Intervall)
    /// </summary>
    /// <remarks>
    /// Die Grenze gehört zum Wert: bei Gleichstand ist die Wartung **fällig**
    /// (genau wie am Gerät).
    /// </remarks>
    private static float? ServiceDue(
        IReadOnlyDictionary<int, float?> inputs,
        int serviceHoursId,
        int intervalId)
    {
        var serviceHours = Input(inputs, serviceHoursId);
        var interval = Input(inputs, intervalId);

        if (serviceHours is null || interval is null)
        {
            return null;
        }

        return serviceHours.Value >= interval.Value ? 1f : 0f;
    }

    /// <summary>
    /// Läuft die Kühlung? (Status-Enum oder Kühl-Vorlauf)
    /// </summary>
    /// <remarks>
    /// <para>
    /// DP 662 und DP 3098 sind **keine** booleschen Werte. Die Zuordnung steht
    /// deshalb genau hier - nicht verstreut im Gateway.
    /// </para>
    /// <para>
    /// DP 3098 ist die Vorlauftemperatur des Kühlkreises in °C. „Aktiv" ist als
    /// <b>größer 0</b> ausgelegt: das Gerät meldet 0 °C, solange es nicht kühlt.
    /// </para>
    /// </remarks>
    private static float? CoolingActive(
        IReadOnlyDictionary<int, float?> inputs,
        int statusId,
        int coolingFlowTemperatureId)
    {
        var status = Input(inputs, statusId);
        var flowTemperature = Input(inputs, coolingFlowTemperatureId);

        if (status is null || flowTemperature is null)
        {
            return null;
        }

        var coolingStatus = status.Value is FreecoolingActive
            or ActiveCooling
            or ActiveCoolingWithPassiveWarmWater;

        var coolingFlow = flowTemperature.Value > 0f;

        return coolingStatus || coolingFlow ? 1f : 0f;
    }
}
