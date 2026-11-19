using System.Text.RegularExpressions;

namespace ComfoBox.Domain.Items;

/// <summary>
/// Liest die Beschriftungen der erlaubten Werte aus der Katalog-Beschreibung.
/// </summary>
/// <remarks>
/// <para>
/// Der Altbestand beschreibt Auswahlwerte in der Beschreibung, aber in **drei**
/// Formen:
/// </para>
/// <list type="bullet">
/// <item><c>* 0: deaktiviert</c> (Zahl zuerst)</item>
/// <item><c>* Leichte Bauweise = 1</c> (Name zuerst)</item>
/// <item><c>0 = keine Funktion, 1 = ein Fühler</c> (Liste in einer Zeile)</item>
/// </list>
/// <para>
/// Die Namen sind **Anzeigetext** und kein Vertrag: sie landen in der Oberfläche
/// und in Home Assistant, nie im MQTT-Payload. Findet der Parser nichts, bleibt
/// es bei der Zahl - ein misslungener Treffer darf nie einen Schreibvorgang
/// verhindern.
/// </para>
/// </remarks>
public static partial class ItemValueLabels
{
    /// <summary>
    /// Die längste akzeptierte Beschriftung (längere Treffer sind Fließtext).
    /// </summary>
    private const int MaxLabelLength = 60;

    /// <summary>
    /// Der größte akzeptierte Zahlenwert.
    /// </summary>
    private const int MaxValue = 99;

    private static readonly IReadOnlyDictionary<int, string> NoLabels =
        new Dictionary<int, string>();

    /// <summary>
    /// Zerlegt eine Beschreibung in „Zahl → Beschriftung".
    /// </summary>
    /// <param name="description">Die Katalog-Beschreibung (oder <c>null</c>).</param>
    /// <returns>Die gefundenen Paare (leer, wenn keines gefunden wurde).</returns>
    public static IReadOnlyDictionary<int, string> FromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return NoLabels;
        }

        var labels = new Dictionary<int, string>();
        var duplicates = new HashSet<int>();

        foreach (var line in SplitLines(description))
        {
            // Eine Zeile kann mehrere Paare enthalten (`0 = a, 1 = b`).
            var found = false;

            if (NumberFirstRegex().Match(line) is { Success: true } single)
            {
                found = Add(labels, duplicates, single);
            }
            else if (NameFirstRegex().Match(line) is { Success: true } named)
            {
                found = Add(labels, duplicates, named);
            }

            if (found)
            {
                continue;
            }

            foreach (Match pair in InlineRegex().Matches(line))
            {
                Add(labels, duplicates, pair);
            }
        }

        // Ein Wert, der zweimal mit verschiedenen Namen auftaucht, ist
        // mehrdeutig - dann bleibt es bei der Zahl.
        foreach (var number in duplicates)
        {
            labels.Remove(number);
        }

        return labels.Count == 0 ? NoLabels : labels;
    }

    /// <summary>
    /// Zerlegt den Text in bereinigte Zeilen.
    /// </summary>
    private static IEnumerable<string> SplitLines(string description)
    {
        var normalized = description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim().TrimStart('*', '-', '•', '·');
            if (line.Length > 0)
            {
                yield return line.Trim();
            }
        }
    }

    /// <summary>
    /// Übernimmt ein gefundenes Paar, wenn es plausibel ist.
    /// </summary>
    /// <returns><c>true</c>, wenn das Paar übernommen wurde.</returns>
    private static bool Add(
        Dictionary<int, string> labels,
        HashSet<int> duplicates,
        Match match)
    {
        if (!int.TryParse(match.Groups["num"].Value, out var number)
            || number < 0
            || number > MaxValue)
        {
            return false;
        }

        var label = match.Groups["name"].Value.Trim().TrimEnd(',', ';', '.');
        if (label.Length is 0 or > MaxLabelLength || !HasLetter(label))
        {
            return false;
        }

        if (labels.TryGetValue(number, out var existing))
        {
            if (!string.Equals(existing, label, StringComparison.Ordinal))
            {
                duplicates.Add(number);
            }

            return true;
        }

        labels[number] = label;
        return true;
    }

    /// <summary>
    /// Enthält der Text mindestens einen Buchstaben?
    /// </summary>
    /// <remarks>
    /// Schützt vor Treffern wie „1 = 2": eine Beschriftung ohne Buchstaben ist
    /// keine Beschriftung.
    /// </remarks>
    private static bool HasLetter(string text)
    {
        foreach (var character in text)
        {
            if (char.IsLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Die Form <c>0: deaktiviert</c> bzw. <c>0 = deaktiviert</c>.
    /// </summary>
    [GeneratedRegex(@"^(?<num>-?\d{1,3})\s*[:=]\s*(?<name>\D.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberFirstRegex();

    /// <summary>
    /// Die Form <c>Leichte Bauweise = 1</c>.
    /// </summary>
    [GeneratedRegex(@"^(?<name>[^=]+?)\s*=\s*(?<num>\d{1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex NameFirstRegex();

    /// <summary>
    /// Die Form <c>0 = keine Funktion, 1 = ein Fühler</c>.
    /// </summary>
    [GeneratedRegex(@"(?<num>\d{1,3})\s*=\s*(?<name>[^,=]+?)\s*(?=,|$)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineRegex();
}
