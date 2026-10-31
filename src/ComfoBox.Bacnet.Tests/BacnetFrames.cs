using System.Text.Json;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Lädt die Golden-Fixture <c>Fixtures/bacnet-frames.json</c>.
/// </summary>
/// <remarks>
/// Die Fixture stammt aus dem **Legacy-Codec** (<c>src_old/Bacnet/BACnetBase.cs</c>).
/// Sie wurde mit einem Referenz-Oracle erzeugt, das genau diesen Codec
/// kompiliert und seine Ausgabebytes protokolliert.
/// <para>
/// Wichtig: Die Fixture wird <b>nicht</b> verändert. Ein Test, der nach der
/// Portierung fehlschlägt, ist ein Zeichen für eine Verhaltensänderung.
/// </para>
/// </remarks>
public static class BacnetFrames
{
    private static readonly Lazy<JsonDocument> LazyDocument = new(Load);

    /// <summary>
    /// Die geladene Fixture.
    /// </summary>
    public static JsonDocument Document => LazyDocument.Value;

    /// <summary>
    /// Alle Frames der Fixture.
    /// </summary>
    public static IEnumerable<JsonElement> All => Document.RootElement.GetProperty("frames").EnumerateArray();

    /// <summary>
    /// Wandelt eine Hex-Darstellung ("0C 00 80") in Bytes.
    /// </summary>
    public static byte[] ParseHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var parts = hex.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            result[i] = Convert.ToByte(parts[i], 16);
        }

        return result;
    }

    private static JsonDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "bacnet-frames.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Die Golden-Fixture 'bacnet-frames.json' wurde nicht gefunden. " +
                "Sie muss als Datei in die Ausgabe kopiert werden.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }
}
