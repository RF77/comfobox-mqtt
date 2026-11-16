using ComfoBox.Domain.Items;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Zuordnung zwischen den Items und den MQTT-Topics.
/// </summary>
/// <remarks>
/// <para>
/// Diese Klasse ist die **einzige** Stelle, an der Topic-Adressen gebildet
/// werden. Der Topic-Vertrag ist unverhandelbar (siehe
/// <c>docs/04-migration/mqtt-kompatibilitaet.md</c>):
/// </para>
/// <list type="bullet">
/// <item><c>{BaseTopic}/{Group}/{Name}</c> - der Wert</item>
/// <item><c>{BaseTopic}/{Group}/{Name}/Set</c> - der Schreibwert</item>
/// <item><c>{BaseTopic}/{Group}/{Name}/AsNumber</c> - der Enum-Wert als Zahl</item>
/// <item><c>{BaseTopic}/{Group}/{Name}/AsNumber/Set</c> - Schreibwert als Zahl</item>
/// </list>
/// <para>
/// Neue Topics gibt es ausschließlich unter <c>{BaseTopic}/Status/</c> und
/// <c>{BaseTopic}/Command/</c>.
/// </para>
/// <para>
/// <b>Experten-Modus zur Laufzeit (Aufgabe 10.13):</b> Die Zuordnung lebt in
/// einem unveränderlichen Schnappschuss, der bei
/// <see cref="UpdateExpertMode"/> **vollständig neu gebaut** und erst dann
/// veröffentlicht wird. Leser sehen deshalb immer einen ganzen Satz Topics -
/// nie eine halb aufgebaute Zuordnung. Die Instanz bleibt dieselbe: sie wird an
/// mehrere Singletons injiziert und darf nicht ersetzt werden.
/// </para>
/// </remarks>
public sealed class MqttTopicMap
{
    private readonly IItemCatalog _catalog;
    private readonly object _sync = new();

    private TopicSnapshot _current;

    /// <summary>
    /// Erzeugt die Zuordnung.
    /// </summary>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="baseTopic">Das Basis-Topic (Standard: <c>ComfoBox</c>).</param>
    /// <param name="expertModeEnabled">Der Startwert des Experten-Modus.</param>
    public MqttTopicMap(IItemCatalog catalog, string baseTopic, bool expertModeEnabled)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseTopic);

        _catalog = catalog;
        BaseTopic = baseTopic;
        _current = BuildSnapshot(expertModeEnabled);
    }

    /// <summary>
    /// Der Item-Katalog.
    /// </summary>
    public IItemCatalog Catalog => _catalog;

    /// <summary>
    /// Das Basis-Topic (Standard: <c>ComfoBox</c>).
    /// </summary>
    public string BaseTopic { get; }

    /// <summary>
    /// Ist der Experten-Modus aktiv?
    /// </summary>
    /// <remarks>
    /// Der Wert wird bei jedem Zugriff aus dem aktuellen Schnappschuss gelesen -
    /// nicht beim Erzeugen der Zuordnung festgeschrieben.
    /// </remarks>
    public bool ExpertModeEnabled => Snapshot.ExpertModeEnabled;

    /// <summary>
    /// Der aktuelle, unveränderliche Schnappschuss.
    /// </summary>
    private TopicSnapshot Snapshot => Volatile.Read(ref _current);

    /// <summary>
    /// Schaltet die Zuordnung auf einen anderen Experten-Modus um.
    /// </summary>
    /// <param name="expertModeEnabled">Ist der Experten-Modus ab jetzt aktiv?</param>
    /// <remarks>
    /// <para>
    /// Die Werte- und <c>/AsNumber</c>-Topics bleiben für **alle** Items
    /// bestehen - Lesen ist immer erlaubt. Nur die Schreib-Topics
    /// (<c>…/Set</c> und <c>…/AsNumber/Set</c>) kommen hinzu oder entfallen.
    /// </para>
    /// <para>
    /// Ein Aufruf mit dem bereits gültigen Wert tut nichts: ein Neuaufbau würde
    /// nur unnötig Abonnements und Discovery-Payloads auslösen.
    /// </para>
    /// </remarks>
    public void UpdateExpertMode(bool expertModeEnabled)
    {
        lock (_sync)
        {
            if (Snapshot.ExpertModeEnabled == expertModeEnabled)
            {
                return;
            }

            // Erst vollständig bauen, dann veröffentlichen: Volatile.Write gibt
            // die Schreibzugriffe frei, Volatile.Read in Snapshot nimmt sie
            // ohne Sperre auf.
            Volatile.Write(ref _current, BuildSnapshot(expertModeEnabled));
        }
    }

    /// <summary>
    /// Das Topic des Item-Werts.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Das Topic, z. B. <c>ComfoBox/Zone/SetPointNormal</c>.</returns>
    public string ItemTopic(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{BaseTopic}/{item.TopicPath}";
    }

    /// <summary>
    /// Das Schreib-Topic des Item-Werts.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Das Topic, z. B. <c>ComfoBox/Zone/SetPointNormal/Set</c>.</returns>
    public string ItemSetTopic(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{ItemTopic(item)}/Set";
    }

    /// <summary>
    /// Das Topic des Enum-Werts als Zahl.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Das Topic, z. B. <c>ComfoBox/Zone/OperationMode/AsNumber</c>.</returns>
    public string ItemAsNumberTopic(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{ItemTopic(item)}/AsNumber";
    }

    /// <summary>
    /// Das Schreib-Topic des Enum-Werts als Zahl.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Das Topic, z. B. <c>ComfoBox/Zone/OperationMode/AsNumber/Set</c>.</returns>
    public string ItemAsNumberSetTopic(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{ItemTopic(item)}/AsNumber/Set";
    }

    /// <summary>
    /// Das Topic des Verbindungsstatus (Altbestand - Kompatibilität!).
    /// </summary>
    public string ConnectedTopic => $"{BaseTopic}/Connected";

    /// <summary>
    /// Das Topic der Version (Altbestand - Kompatibilität!).
    /// </summary>
    public string VersionTopic => $"{BaseTopic}/Version";

    /// <summary>
    /// Das Topic des Schreibzählers (Altbestand - Kompatibilität!).
    /// </summary>
    public string NumberOfWritesTopic => $"{BaseTopic}/Special/NumberOfWritesPer24h";

    /// <summary>
    /// Das Topic des Health-Status (Availability in Home Assistant).
    /// </summary>
    public string HealthTopic => $"{BaseTopic}/Status/Health";

    /// <summary>
    /// Das Topic der Version ohne <c>Ver </c>-Präfix.
    /// </summary>
    public string StatusVersionTopic => $"{BaseTopic}/Status/Version";

    /// <summary>
    /// Das Topic der Laufzeit in Sekunden.
    /// </summary>
    public string UptimeTopic => $"{BaseTopic}/Status/Uptime";

    /// <summary>
    /// Das Topic des Startzeitpunkts (ISO 8601).
    /// </summary>
    public string StartedAtTopic => $"{BaseTopic}/Status/StartedAt";

    /// <summary>
    /// Das Topic des Schreibzählers (Anzahl im 24-h-Fenster).
    /// </summary>
    public string WritesTopic => $"{BaseTopic}/Status/Writes";

    /// <summary>
    /// Das Topic des Schreibzähler-Limits.
    /// </summary>
    public string WritesMaxTopic => $"{BaseTopic}/Status/WritesMax";

    /// <summary>
    /// Das Topic des BACnet-Status (JSON).
    /// </summary>
    public string BacnetTopic => $"{BaseTopic}/Status/Bacnet";

    /// <summary>
    /// Das Topic des MQTT-Status (JSON).
    /// </summary>
    public string MqttTopic => $"{BaseTopic}/Status/Mqtt";

    /// <summary>
    /// Das Topic der Verfügbarkeit der Wärmemengenmessung (DP 3340).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es ist **kein** Katalog-Item - es gibt kein BACnet-Objekt dazu. Der
    /// Wert wird aus <c>Status/Energy/MeasurementMode</c> gebildet:
    /// <c>1</c> = verfügbar, <c>0</c> = nicht verfügbar, leere Payload = noch
    /// nie gelesen. Das Topic ist die Availability der Energie-Entitäten in
    /// Home Assistant.
    /// </para>
    /// </remarks>
    public string EnergyAvailableTopic => $"{BaseTopic}/Status/Energy/Available";

    /// <summary>
    /// Das Topic des letzten Fehlers.
    /// </summary>
    public string LastErrorTopic => $"{BaseTopic}/Status/LastError";

    /// <summary>
    /// Das Topic der Fehlerhistorie (JSON-Array).
    /// </summary>
    public string ErrorsTopic => $"{BaseTopic}/Status/Errors";

    /// <summary>
    /// Das Topic des Neustart-Signals.
    /// </summary>
    public string RestartTopic => $"{BaseTopic}/Status/Restart";

    /// <summary>
    /// Das Topic des Neustart-Befehls (Subscribe).
    /// </summary>
    public string RestartCommandTopic => $"{BaseTopic}/Command/Restart";

    /// <summary>
    /// Die Topics, auf die geschrieben werden darf.
    /// </summary>
    /// <returns>
    /// Die Schreib-Topics in Katalog-Reihenfolge - also <c>…/Set</c> und
    /// <c>…/AsNumber/Set</c>.
    /// </returns>
    /// <remarks>
    /// Die Liste gehört zum veröffentlichten Schnappschuss und darf nicht
    /// verändert werden.
    /// </remarks>
    public IReadOnlyList<string> WriteTopics() => Snapshot.WriteTopics;

    /// <summary>
    /// Die Schreib-Topics in der Namensform (<c>…/Set</c>).
    /// </summary>
    /// <returns>Die Topics in Katalog-Reihenfolge.</returns>
    /// <remarks>
    /// Die Liste gehört zum veröffentlichten Schnappschuss und darf nicht
    /// verändert werden.
    /// </remarks>
    public IReadOnlyList<string> SetTopics() => Snapshot.SetTopics;

    /// <summary>
    /// Löst ein Topic auf.
    /// </summary>
    /// <param name="topic">Das Topic (mit Basis-Topic).</param>
    /// <param name="item">Das gefundene Item (oder <c>null</c>).</param>
    /// <param name="form">Die Form des Werts (Name oder Zahl).</param>
    /// <returns><c>true</c>, wenn das Topic bekannt ist.</returns>
    public bool TryResolve(string topic, out ItemDefinition? item, out MqttTopicForm form)
    {
        if (topic is not null && Snapshot.Bindings.TryGetValue(topic, out var binding))
        {
            item = binding.Item;
            form = binding.Form;
            return true;
        }

        item = null;
        form = MqttTopicForm.Value;
        return false;
    }

    /// <summary>
    /// Baut die Zuordnung für einen Experten-Modus.
    /// </summary>
    /// <param name="expertModeEnabled">Ist der Experten-Modus aktiv?</param>
    /// <returns>Der fertige, unveränderliche Schnappschuss.</returns>
    private TopicSnapshot BuildSnapshot(bool expertModeEnabled)
    {
        var bindings = new Dictionary<string, TopicBinding>(StringComparer.Ordinal);
        var writeTopics = new List<string>();
        var setTopics = new List<string>();

        foreach (var item in _catalog.All)
        {
            var topic = ItemTopic(item);

            bindings[topic] = new TopicBinding(item, MqttTopicForm.Value);
            bindings[ItemAsNumberTopic(item)] = new TopicBinding(item, MqttTopicForm.AsNumber);

            if (!item.IsWritableWithExpertMode(expertModeEnabled))
            {
                continue;
            }

            var setTopic = ItemSetTopic(item);
            var asNumberSetTopic = ItemAsNumberSetTopic(item);

            bindings[setTopic] = new TopicBinding(item, MqttTopicForm.Value);
            bindings[asNumberSetTopic] = new TopicBinding(item, MqttTopicForm.AsNumber);

            writeTopics.Add(setTopic);
            writeTopics.Add(asNumberSetTopic);
            setTopics.Add(setTopic);
        }

        return new TopicSnapshot(
            bindings,
            [.. writeTopics],
            [.. setTopics],
            expertModeEnabled);
    }

    /// <summary>
    /// Ein unveränderlicher Satz aus Zuordnung und Schreib-Topics.
    /// </summary>
    /// <param name="Bindings">Topic → Item.</param>
    /// <param name="WriteTopics">Alle Schreib-Topics in Katalog-Reihenfolge.</param>
    /// <param name="SetTopics">Die Schreib-Topics in Namensform.</param>
    /// <param name="ExpertModeEnabled">Der Modus, für den der Satz gebaut wurde.</param>
    private sealed record TopicSnapshot(
        Dictionary<string, TopicBinding> Bindings,
        string[] WriteTopics,
        string[] SetTopics,
        bool ExpertModeEnabled);
}

/// <summary>
/// Die Form eines Topic-Werts.
/// </summary>
public enum MqttTopicForm
{
    /// <summary>
    /// Der Wert als Name (Enum) oder als Zahl (Analog).
    /// </summary>
    Value,

    /// <summary>
    /// Der Enum-Wert als Zahl.
    /// </summary>
    AsNumber,
}

/// <summary>
/// Die Zuordnung eines Topics zu einem Item.
/// </summary>
/// <param name="Item">Das Item.</param>
/// <param name="Form">Die Form des Werts.</param>
public sealed record TopicBinding(ItemDefinition Item, MqttTopicForm Form);
