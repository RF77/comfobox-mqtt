namespace ComfoBox.Domain.Derived;

/// <summary>
/// Die Definition eines abgeleiteten Wertes - der Gateway rechnet, nicht das Gerät.
/// </summary>
/// <remarks>
/// <para>
/// Ein abgeleiteter Wert hat **kein** BACnet-Objekt. Er ist deshalb kein
/// <see cref="Items.ItemDefinition"/> und liegt nicht im Katalog
/// (<c>BacnetObjectId</c> ist dort Pflicht und muss größer als 0 sein). Er wird
/// aus den Werten mehrerer Items berechnet.
/// </para>
/// <para>
/// Abgeleitete Werte liegen immer unter dem reservierten Zweig
/// <c>{BaseTopic}/Status/</c> (Regel: neue Topics nur unter <c>Status/</c> oder
/// <c>Command/</c>). Das vollständige Topic ist deshalb
/// <c>{BaseTopic}/Status/{TopicPath}</c>.
/// </para>
/// <para>
/// Ein abgeleiteter Wert ist **nie** schreibbar: die Definition kennt bewusst
/// keine Schreibfelder, es gibt kein <c>/Set</c>- und kein <c>/AsNumber</c>-Topic.
/// </para>
/// </remarks>
public sealed record DerivedValueDefinition
{
    /// <summary>
    /// Die stabile Id des abgeleiteten Wertes, z. B. <c>derived.superheat</c>.
    /// </summary>
    /// <remarks>
    /// Die Id dient als Schlüssel für die Entprellung beim Publizieren und für
    /// die Home-Assistant-Discovery. Sie kollidiert mit keiner Katalog-Id.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Der Topic-Pfad **ohne** <c>Status/</c> und ohne <c>BaseTopic</c>,
    /// z. B. <c>Derived/Superheat</c>.
    /// </summary>
    /// <remarks>
    /// Das vollständige Topic ist <c>{BaseTopic}/Status/{TopicPath}</c> -
    /// also <c>ComfoBox/Status/Derived/Superheat</c>.
    /// </remarks>
    public required string TopicPath { get; init; }

    /// <summary>
    /// Die Einheit des Wertes (z. B. <c>K</c> oder <c>%</c>) oder <c>null</c>.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Die Formel in Klartext - für Tooltip, Topic-Dokument und Diagnoseseite.
    /// </summary>
    /// <remarks>
    /// Ohne die Formel ist ein abgeleiteter Wert nicht interpretierbar: die Zahl
    /// allein sagt nicht, aus welchen Datenpunkten sie entstanden ist.
    /// </remarks>
    public required string Formula { get; init; }

    /// <summary>
    /// Die BACnet-Objekt-Ids der Eingänge (Datenpunkte).
    /// </summary>
    /// <remarks>
    /// Der Katalog ist für diese Datenpunkte eindeutig - der Gateway löst die
    /// Ids deshalb direkt über <see cref="Items.ItemDefinition.BacnetObjectId"/>
    /// auf. Ein Eingang, der fehlt oder veraltet ist, macht den abgeleiteten
    /// Wert <c>unbekannt</c> (nie <c>0</c>).
    /// </remarks>
    public required IReadOnlyList<int> InputObjectIds { get; init; }

    /// <summary>
    /// Braucht der Wert einen Verlauf (zwei Zeitpunkte)?
    /// </summary>
    /// <remarks>
    /// Nur <c>Status/Derived/CopInstant</c> ist zustandsbehaftet: er vergleicht
    /// zwei Zählerstände, die mindestens eine Stunde auseinanderliegen. Ein
    /// solcher Wert hat keine reine Rechenfunktion - die Historie führt der
    /// <c>DerivedValueService</c> im Gateway.
    /// </remarks>
    public bool RequiresHistory { get; init; }

    /// <summary>
    /// Die reine Rechenfunktion (Eingänge nach Objekt-Id → Wert).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Funktion liefert <c>null</c>, wenn ein Eingang fehlt oder der Wert
    /// nicht berechenbar ist (z. B. Nenner 0). <c>null</c> bedeutet
    /// <c>unbekannt</c> und wird als <c>unknown</c> publiziert - **nie** als
    /// <c>0</c>.
    /// </para>
    /// <para>
    /// Bei <see cref="RequiresHistory"/> ist die Funktion <c>null</c>: ein
    /// zustandsbehafteter Wert kann aus einem einzelnen Satz Eingänge nicht
    /// berechnet werden.
    /// </para>
    /// </remarks>
    public Func<IReadOnlyDictionary<int, float?>, float?>? Compute { get; init; }
}
