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
/// </remarks>
public sealed class MqttTopicMap
{
    private readonly Dictionary<string, TopicBinding> _bindings = new(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt die Zuordnung.
    /// </summary>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="baseTopic">Das Basis-Topic (Standard: <c>ComfoBox</c>).</param>
    /// <param name="expertModeEnabled">Ist der Experten-Modus aktiv?</param>
    public MqttTopicMap(IItemCatalog catalog, string baseTopic, bool expertModeEnabled)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseTopic);

        Catalog = catalog;
        BaseTopic = baseTopic;
        ExpertModeEnabled = expertModeEnabled;

        foreach (var item in catalog.All)
        {
            _bindings[ItemTopic(item)] = new TopicBinding(item, MqttTopicForm.Value);
            _bindings[ItemAsNumberTopic(item)] = new TopicBinding(item, MqttTopicForm.AsNumber);

            if (!item.IsWritableWithExpertMode(expertModeEnabled))
            {
                continue;
            }

            _bindings[ItemSetTopic(item)] = new TopicBinding(item, MqttTopicForm.Value);
            _bindings[ItemAsNumberSetTopic(item)] = new TopicBinding(item, MqttTopicForm.AsNumber);
        }
    }

    /// <summary>
    /// Der Item-Katalog.
    /// </summary>
    public IItemCatalog Catalog { get; }

    /// <summary>
    /// Das Basis-Topic (Standard: <c>ComfoBox</c>).
    /// </summary>
    public string BaseTopic { get; }

    /// <summary>
    /// Ist der Experten-Modus aktiv?
    /// </summary>
    public bool ExpertModeEnabled { get; }

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
    public IReadOnlyList<string> WriteTopics()
        => [.. _bindings.Keys.Where(t => t.EndsWith("/Set", StringComparison.Ordinal))];

    /// <summary>
    /// Die Schreib-Topics in der Namensform (<c>…/Set</c>).
    /// </summary>
    /// <returns>Die Topics in Katalog-Reihenfolge.</returns>
    public IReadOnlyList<string> SetTopics()
        => [.. _bindings.Keys.Where(t => t.EndsWith("/Set", StringComparison.Ordinal)
            && !t.EndsWith("/AsNumber/Set", StringComparison.Ordinal))];

    /// <summary>
    /// Löst ein Topic auf.
    /// </summary>
    /// <param name="topic">Das Topic (mit Basis-Topic).</param>
    /// <param name="item">Das gefundene Item (oder <c>null</c>).</param>
    /// <param name="form">Die Form des Werts (Name oder Zahl).</param>
    /// <returns><c>true</c>, wenn das Topic bekannt ist.</returns>
    public bool TryResolve(string topic, out ItemDefinition? item, out MqttTopicForm form)
    {
        if (topic is not null && _bindings.TryGetValue(topic, out var binding))
        {
            item = binding.Item;
            form = binding.Form;
            return true;
        }

        item = null;
        form = MqttTopicForm.Value;
        return false;
    }
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
