namespace ComfoBox.Domain.Items;

/// <summary>
/// Die Bedeutung eines Items - feiner als der reine Datentyp.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ItemValueKind"/> sagt nur, wie der Wert kodiert ist (Zahl, Enum,
/// Datum). Für die Darstellung ist entscheidend, **was** er bedeutet: ein
/// Temperaturmesswert, ein Sollwert, ein Schalter, ein Kodewert aus einer
/// festen Liste, ein Zähler, eine Geräteangabe oder ein Auslöser.
/// </para>
/// <para>
/// Die Bedeutung leitet sich aus den Katalogdaten ab (Einheit, Bereich, erlaubte
/// Werte, Priorität) - sie wird nirgends doppelt gepflegt. Sie ist die Grundlage
/// für die Zuordnung zu Home-Assistant-Plattformen und für die Entscheidung,
/// ob ein Wert eine Langzeitstatistik erzeugen darf.
/// </para>
/// </remarks>
public enum ItemSemantics
{
    /// <summary>
    /// Eine Messgröße mit Einheit (z. B. 21,5 °C) - erzeugt eine Statistik.
    /// </summary>
    Measurement,

    /// <summary>
    /// Ein schreibbarer Sollwert mit Einheit (z. B. Raumsollwert).
    /// </summary>
    Setpoint,

    /// <summary>
    /// Ein 0/1-Zustand: schreibbar ein Schalter, lesend eine Meldung.
    /// </summary>
    Flag,

    /// <summary>
    /// Ein Kodewert aus einer festen Liste (z. B. Kühlung 0/1/3).
    /// </summary>
    /// <remarks>
    /// Der Wert bleibt technisch eine Zahl - die erlaubten Werte stehen in
    /// <see cref="ItemDefinition.AllowedValues"/> bzw. in
    /// <see cref="ItemDefinition.Min"/>/<see cref="ItemDefinition.Max"/>.
    /// </remarks>
    Code,

    /// <summary>
    /// Ein echter Enum-Wert (<see cref="ItemValueKind.Enum"/>) mit Namen.
    /// </summary>
    Choice,

    /// <summary>
    /// Ein Zähler, der nur steigt (Betriebsstunden, Schaltspiele).
    /// </summary>
    Counter,

    /// <summary>
    /// Eine Geräteangabe ohne Messgröße (Reglertyp, Softwarestand, Rohzustand).
    /// </summary>
    /// <remarks>
    /// Solche Werte erzeugen **keine** Langzeitstatistik: eine „Messung" des
    /// Reglertyps wäre bedeutungslos und füllt nur die Datenbank.
    /// </remarks>
    Info,

    /// <summary>
    /// Ein Auslöser: wird nie gelesen, sondern nur geschrieben.
    /// </summary>
    Command,

    /// <summary>
    /// Ein Datumswert, den der Altbestand nicht interpretiert (toter Wert).
    /// </summary>
    Date,
}

/// <summary>
/// Leitet die <see cref="ItemSemantics"/> eines Items aus seinen Metadaten ab.
/// </summary>
/// <remarks>
/// Die Regeln sind bewusst einfach und in dieser Reihenfolge geprüft; jede
/// Regel ist durch einen Test gegen den echten Katalog abgesichert.
/// </remarks>
public static class ItemSemanticsRules
{
    /// <summary>
    /// Die Bedeutung des Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Die abgeleitete Bedeutung.</returns>
    public static ItemSemantics Of(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Kind == ItemValueKind.Date)
        {
            return ItemSemantics.Date;
        }

        if (item.Kind == ItemValueKind.Enum)
        {
            return ItemSemantics.Choice;
        }

        // Ein Command wird nie gelesen (Priority None): er löst eine Aktion aus.
        // Die Prüfung steht vor der Flag-Regel, weil
        // `Command/ResetHeatingCurveAdaption` einen 0..1-Bereich hat.
        if (item.Priority == RefreshPriority.None && item.IsWritable)
        {
            return ItemSemantics.Command;
        }

        if (IsFlag(item))
        {
            return ItemSemantics.Flag;
        }

        // Eine hinterlegte Werteliste macht die Zahl zur Auswahl - auch wenn
        // der Bereich größer ist (Cooling erlaubt 0..3, aber nur 0, 1 und 3).
        if (item.AllowedValues is not null)
        {
            return ItemSemantics.Code;
        }

        if (string.IsNullOrEmpty(item.Unit) && IsCodeRange(item))
        {
            return ItemSemantics.Code;
        }

        if (IsCounter(item))
        {
            return ItemSemantics.Counter;
        }

        if (IsStateGroup(item.Group))
        {
            // Ausgänge und Test-Punkte sind Zustände, keine Messgrößen -
            // auch wenn sie eine Einheit tragen (`States/Outputs/Y1` in %).
            return string.IsNullOrEmpty(item.Unit) ? ItemSemantics.Flag : ItemSemantics.Info;
        }

        if (string.IsNullOrEmpty(item.Unit))
        {
            return ItemSemantics.Info;
        }

        return item.IsWritable ? ItemSemantics.Setpoint : ItemSemantics.Measurement;
    }

    /// <summary>
    /// Ist das Item ein 0/1-Zustand?
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns><c>true</c> für einen Bereich 0…1 oder die Werteliste 0/1.</returns>
    public static bool IsFlag(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.AllowedValues is [0f, 1f])
        {
            return true;
        }

        return IsValue(item.Min, 0f) && IsValue(item.Max, 1f);
    }

    /// <summary>
    /// Ist der Bereich ein ganzzahliger Kodewert-Bereich (0…n)?
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns><c>true</c> für 0…2 bis 0…32 ohne Einheit.</returns>
    /// <remarks>
    /// Ein solcher Bereich ohne Einheit ist im Katalog immer eine Auswahl
    /// (Gebäudeträgheit 0…3, Legionellenschutz 0…8, Vorrang 0…2 …). Echte
    /// Zahlenwerte tragen dort eine Einheit oder sind Zähler.
    /// </remarks>
    public static bool IsCodeRange(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return IsValue(item.Min, 0f)
            && item.Max is not null
            && item.Max.Value >= 2f
            && item.Max.Value <= 32f;
    }

    /// <summary>
    /// Ist das Item ein monoton steigender Zähler?
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns><c>true</c> für die Zählergruppen und die beiden Reglerzähler.</returns>
    public static bool IsCounter(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Group.StartsWith("States/Counters", StringComparison.Ordinal)
            || item.Group.StartsWith("Status/Counters", StringComparison.Ordinal)
            || string.Equals(item.Name, "NumberOfStartUps", StringComparison.Ordinal)
            || string.Equals(item.Name, "NumberOfParameterChanges", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gehört die Gruppe zu den Zustands- und Testausgängen?
    /// </summary>
    /// <param name="group">Die Topic-Gruppe.</param>
    /// <returns><c>true</c> für <c>Tests</c> und <c>States/Outputs</c>.</returns>
    private static bool IsStateGroup(string group)
        => string.Equals(group, "Tests", StringComparison.Ordinal)
            || group.StartsWith("States/Outputs", StringComparison.Ordinal);

    /// <summary>
    /// Entspricht ein Wert (mit Toleranz) dem erwarteten?
    /// </summary>
    private static bool IsValue(float? value, float expected)
        => value is not null && Math.Abs(value.Value - expected) < 0.0001f;
}
