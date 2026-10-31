using System.Globalization;
using ComfoBox.Domain.Values;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Tests für die Werttypen der Domäne.
/// </summary>
/// <remarks>
/// Befund A5: Zahlen wurden im Altbestand kulturabhängig geparst und
/// formatiert - auf deutschen Systemen waren die Schreibvorgänge dadurch kaputt.
/// </remarks>
public sealed class ValueTests
{
    [Fact]
    public void AnalogValue_Liest_21_5_Auf_de_DE_Richtig()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.True(AnalogValue.TryParse("21.5", out var value));
            Assert.Equal(21.5f, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void AnalogValue_Interpretiert_23_Komma_5_Nicht_Als_235()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // "23,5" ist kein gültiger Payload - er darf nicht als 235 gelesen werden.
            Assert.False(AnalogValue.TryParse("23,5", out var value));
            Assert.NotEqual(235f, value);
            Assert.Null(AnalogValue.Parse("23,5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void AnalogValue_Formatiert_Kultur_Invariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var value = new AnalogValue(21.5f, 5f, 30f, "°C");

            Assert.Equal("21.5", value.ToInvariantString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void AnalogValue_Ohne_Wert_Formatiert_Zu_Leerem_String()
    {
        var value = new AnalogValue(null, 5f, 30f, "°C");

        Assert.Equal(string.Empty, value.ToInvariantString());
        Assert.Null(value.Raw);
    }

    [Fact]
    public void EnumValue_Liefert_Den_Namen_Des_Wertes()
    {
        var values = new Dictionary<int, string> { [1] = "Manual", [6] = "Normal" };
        var value = new EnumValue(6, values);

        Assert.Equal("Normal", value.Name);
        Assert.Equal("Normal", value.ToInvariantString());
        Assert.Equal(6, value.Raw);
    }

    [Fact]
    public void EnumValue_Mit_Unbekanntem_Wert_Hat_Keinen_Namen()
    {
        var values = new Dictionary<int, string> { [1] = "Manual" };
        var value = new EnumValue(99, values);

        Assert.Null(value.Name);
        Assert.Equal(string.Empty, value.ToInvariantString());
    }

    [Fact]
    public void EnumValue_Mit_Leerem_Wert_Hat_Keinen_Namen()
    {
        var value = new EnumValue(null, new Dictionary<int, string> { [1] = "Manual" });

        Assert.Null(value.Name);
        Assert.Null(value.Raw);
    }
}
